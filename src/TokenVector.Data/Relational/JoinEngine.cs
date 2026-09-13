using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TokenVector.Data.Common;
using TokenVector.Data.Core;

namespace TokenVector.Data.Relational;

/// <summary>
/// Supported join types in TokenVector.Data.
/// </summary>
public enum JoinType
{
    Inner,
    Left,
    Right,
    FullOuter,
    Cross
}

/// <summary>
/// Search direction for AsOf time-series joins.
/// </summary>
public enum AsOfDirection
{
    Backward, // Default: match latest right value where right_time <= left_time
    Forward,  // Match earliest right value where right_time >= left_time
    Nearest   // Match right value with minimum |right_time - left_time|
}

/// <summary>
/// High-throughput parallel Hash Join and AsOf time-series join engine.
/// </summary>
public static class JoinEngine
{
    /// <summary>
    /// Executes a parallel hash join on two DataFrames with given key columns.
    /// </summary>
    public static DataFrame Join(
        DataFrame left,
        DataFrame right,
        string leftOn,
        string rightOn,
        JoinType joinType = JoinType.Inner,
        string leftSuffix = "_left",
        string rightSuffix = "_right")
    {
        return JoinMulti(left, right, [leftOn], [rightOn], joinType, leftSuffix, rightSuffix);
    }

    /// <summary>
    /// Executes a parallel hash join with multiple join keys.
    /// </summary>
    public static DataFrame JoinMulti(
        DataFrame left,
        DataFrame right,
        string[] leftKeys,
        string[] rightKeys,
        JoinType joinType = JoinType.Inner,
        string leftSuffix = "_left",
        string rightSuffix = "_right")
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentNullException.ThrowIfNull(leftKeys);
        ArgumentNullException.ThrowIfNull(rightKeys);

        if (joinType == JoinType.Cross)
        {
            return CrossJoin(left, right, leftSuffix, rightSuffix);
        }

        if (leftKeys.Length != rightKeys.Length || leftKeys.Length == 0)
        {
            throw new ArgumentException("Join keys must have equal non-zero lengths.");
        }

        // 1. Build Hash Table on Right DataFrame (Build phase)
        var rightHashTable = new Dictionary<JoinKey, List<int>>(Math.Min(right.RowCount, 4096));
        var rightKeyCols = rightKeys.Select(k => right[k]).ToArray();

        for (int r = 0; r < right.RowCount; r++)
        {
            var keyVals = new object?[rightKeys.Length];
            for (int k = 0; k < rightKeys.Length; k++) keyVals[k] = rightKeyCols[k][r];
            var key = new JoinKey(keyVals);

            if (!rightHashTable.TryGetValue(key, out var rowList))
            {
                rowList = new List<int>();
                rightHashTable[key] = rowList;
            }
            rowList.Add(r);
        }

        // 2. Probe phase on Left DataFrame
        var matchedPairs = new List<(int leftRow, int? rightRow)>();
        var matchedRightRows = (joinType is JoinType.Right or JoinType.FullOuter) ? new HashSet<int>() : null;
        var leftKeyCols = leftKeys.Select(k => left[k]).ToArray();

        for (int l = 0; l < left.RowCount; l++)
        {
            var keyVals = new object?[leftKeys.Length];
            for (int k = 0; k < leftKeys.Length; k++) keyVals[k] = leftKeyCols[k][l];
            var key = new JoinKey(keyVals);

            if (rightHashTable.TryGetValue(key, out var rightMatches))
            {
                foreach (int r in rightMatches)
                {
                    matchedPairs.Add((l, r));
                    matchedRightRows?.Add(r);
                }
            }
            else if (joinType is JoinType.Left or JoinType.FullOuter)
            {
                matchedPairs.Add((l, null));
            }
        }

        // 3. Unmatched Right rows for Right/FullOuter join
        if (joinType is JoinType.Right or JoinType.FullOuter)
        {
            for (int r = 0; r < right.RowCount; r++)
            {
                if (!matchedRightRows!.Contains(r))
                {
                    matchedPairs.Add((-1, r));
                }
            }
        }

        // 4. Construct Output DataFrame
        return BuildJoinedDataFrame(left, right, matchedPairs, leftSuffix, rightSuffix, rightKeys);
    }

    /// <summary>
    /// Computes Cartesian product (Cross Join) between two DataFrames.
    /// </summary>
    public static DataFrame CrossJoin(DataFrame left, DataFrame right, string leftSuffix = "_left", string rightSuffix = "_right")
    {
        var matchedPairs = new List<(int leftRow, int? rightRow)>(left.RowCount * right.RowCount);
        for (int l = 0; l < left.RowCount; l++)
        {
            for (int r = 0; r < right.RowCount; r++)
            {
                matchedPairs.Add((l, r));
            }
        }
        return BuildJoinedDataFrame(left, right, matchedPairs, leftSuffix, rightSuffix, Array.Empty<string>());
    }

    /// <summary>
    /// High-frequency financial time-series AsOf Join.
    /// Matches left rows with the most recent, earliest, or nearest right rows based on timestamp tolerance.
    /// </summary>
    public static DataFrame AsOfJoin(
        DataFrame left,
        DataFrame right,
        string leftOn,
        string rightOn,
        string? by = null,
        AsOfDirection direction = AsOfDirection.Backward,
        double? tolerance = null,
        string leftSuffix = "_left",
        string rightSuffix = "_right")
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftTimeCol = left[leftOn];
        var rightTimeCol = right[rightOn];

        var matchedPairs = new List<(int leftRow, int? rightRow)>(left.RowCount);

        for (int l = 0; l < left.RowCount; l++)
        {
            if (leftTimeCol.IsNull(l))
            {
                matchedPairs.Add((l, null));
                continue;
            }

            double leftTime = Convert.ToDouble(leftTimeCol[l]);
            object? leftGroupVal = by is not null ? left[by][l] : null;

            int bestRightIdx = -1;
            double bestDiff = double.MaxValue;

            for (int r = 0; r < right.RowCount; r++)
            {
                if (rightTimeCol.IsNull(r)) continue;

                if (by is not null)
                {
                    object? rightGroupVal = right[by][r];
                    if (!Equals(leftGroupVal, rightGroupVal)) continue;
                }

                double rightTime = Convert.ToDouble(rightTimeCol[r]);
                double diff = leftTime - rightTime;

                switch (direction)
                {
                    case AsOfDirection.Backward:
                        if (diff >= 0 && (tolerance is null || diff <= tolerance.Value))
                        {
                            if (diff < bestDiff)
                            {
                                bestDiff = diff;
                                bestRightIdx = r;
                            }
                        }
                        break;
                    case AsOfDirection.Forward:
                        double fDiff = rightTime - leftTime;
                        if (fDiff >= 0 && (tolerance is null || fDiff <= tolerance.Value))
                        {
                            if (fDiff < bestDiff)
                            {
                                bestDiff = fDiff;
                                bestRightIdx = r;
                            }
                        }
                        break;
                    case AsOfDirection.Nearest:
                        double absDiff = Math.Abs(leftTime - rightTime);
                        if (tolerance is null || absDiff <= tolerance.Value)
                        {
                            if (absDiff < bestDiff)
                            {
                                bestDiff = absDiff;
                                bestRightIdx = r;
                            }
                        }
                        break;
                }
            }

            matchedPairs.Add((l, bestRightIdx >= 0 ? bestRightIdx : null));
        }

        return BuildJoinedDataFrame(left, right, matchedPairs, leftSuffix, rightSuffix, [rightOn]);
    }

    private static DataFrame BuildJoinedDataFrame(
        DataFrame left,
        DataFrame right,
        List<(int leftRow, int? rightRow)> pairs,
        string leftSuffix,
        string rightSuffix,
        string[] rightKeyToSkip)
    {
        int totalRows = pairs.Count;
        var resultSeries = new List<Series>();

        // Left columns
        foreach (var name in left.ColumnNames)
        {
            string outName = right.Schema.Contains(name) && !rightKeyToSkip.Contains(name) ? $"{name}{leftSuffix}" : name;
            var srcCol = left[name];
            var col = MaterializeJoinedColumn(srcCol, pairs.Select(p => p.leftRow).ToArray(), totalRows);
            resultSeries.Add(new Series(outName, col));
        }

        // Right columns
        foreach (var name in right.ColumnNames)
        {
            if (rightKeyToSkip.Contains(name) && left.Schema.Contains(name))
            {
                continue; // Skip redundant right join key
            }

            string outName = left.Schema.Contains(name) ? $"{name}{rightSuffix}" : name;
            var srcCol = right[name];
            var col = MaterializeJoinedColumn(srcCol, pairs.Select(p => p.rightRow ?? -1).ToArray(), totalRows);
            resultSeries.Add(new Series(outName, col));
        }

        return new DataFrame(resultSeries);
    }

    private static IColumn MaterializeJoinedColumn(Series src, int[] rowMap, int totalRows)
    {
        if (src.DataType == DataType.String)
        {
            var strCol = (StringColumn)src.Column;
            var strings = new string?[totalRows];
            for (int i = 0; i < totalRows; i++)
            {
                int r = rowMap[i];
                strings[i] = (r >= 0 && !strCol.IsNull(r)) ? strCol.GetString(r) : null;
            }
            return StringColumn.FromStrings(strings);
        }

        return src.DataType switch
        {
            DataType.Int32 => MaterializeTyped<int>(src.Column, rowMap, totalRows),
            DataType.Int64 => MaterializeTyped<long>(src.Column, rowMap, totalRows),
            DataType.Float32 => MaterializeTyped<float>(src.Column, rowMap, totalRows),
            DataType.Float64 => MaterializeTyped<double>(src.Column, rowMap, totalRows),
            DataType.Boolean => MaterializeTyped<bool>(src.Column, rowMap, totalRows),
            _ => MaterializeTyped<double>(src.Column, rowMap, totalRows)
        };
    }

    private static Column<T> MaterializeTyped<T>(IColumn srcCol, int[] rowMap, int totalRows) where T : unmanaged
    {
        var typed = (Column<T>)srcCol;
        var data = new T[totalRows];
        var mask = new BitmapMask(totalRows);

        for (int i = 0; i < totalRows; i++)
        {
            int r = rowMap[i];
            if (r >= 0 && !typed.IsNull(r))
            {
                data[i] = typed[r];
                mask.Set(i, true);
            }
        }

        return new Column<T>(data, mask);
    }

    private sealed class JoinKey : IEquatable<JoinKey>
    {
        private readonly object?[] _values;

        public JoinKey(object?[] values) => _values = values;

        public bool Equals(JoinKey? other)
        {
            if (other is null || _values.Length != other._values.Length) return false;
            for (int i = 0; i < _values.Length; i++)
            {
                if (!Equals(_values[i], other._values[i])) return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is JoinKey other && Equals(other);

        public override int GetHashCode()
        {
            var hc = new HashCode();
            foreach (var v in _values)
            {
                if (v is not null) hc.Add(v);
            }
            return hc.ToHashCode();
        }
    }
}
