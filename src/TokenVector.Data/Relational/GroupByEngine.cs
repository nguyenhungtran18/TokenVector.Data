using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TokenVector.Data.Common;
using TokenVector.Data.Core;

namespace TokenVector.Data.Relational;

/// <summary>
/// Aggregation type for GroupBy expressions.
/// </summary>
public enum AggregationType
{
    Sum,
    Mean,
    Min,
    Max,
    Count,
    Std,
    First,
    Last
}

/// <summary>
/// Defines an aggregation expression on a target column.
/// </summary>
public sealed class AggregationExpr
{
    public string TargetColumn { get; }
    public AggregationType AggType { get; }
    public string OutputName { get; }

    public AggregationExpr(string targetColumn, AggregationType aggType, string? outputName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetColumn);
        TargetColumn = targetColumn;
        AggType = aggType;
        OutputName = outputName ?? $"{targetColumn}_{aggType.ToString().ToLowerInvariant()}";
    }
}

/// <summary>
/// Static builder for aggregation expressions.
/// </summary>
public static class Agg
{
    public static AggregationExpr Sum(string col, string? outName = null) => new(col, AggregationType.Sum, outName);
    public static AggregationExpr Mean(string col, string? outName = null) => new(col, AggregationType.Mean, outName);
    public static AggregationExpr Min(string col, string? outName = null) => new(col, AggregationType.Min, outName);
    public static AggregationExpr Max(string col, string? outName = null) => new(col, AggregationType.Max, outName);
    public static AggregationExpr Count(string col, string? outName = null) => new(col, AggregationType.Count, outName);
    public static AggregationExpr Std(string col, string? outName = null) => new(col, AggregationType.Std, outName);
    public static AggregationExpr First(string col, string? outName = null) => new(col, AggregationType.First, outName);
    public static AggregationExpr Last(string col, string? outName = null) => new(col, AggregationType.Last, outName);
}

/// <summary>
/// High-throughput parallel Radix/Hash GroupBy engine.
/// </summary>
public sealed class GroupByContext
{
    private readonly DataFrame _sourceDf;
    private readonly string[] _keyColumns;

    public GroupByContext(DataFrame sourceDf, string[] keyColumns)
    {
        ArgumentNullException.ThrowIfNull(sourceDf);
        ArgumentNullException.ThrowIfNull(keyColumns);
        if (keyColumns.Length == 0) throw new ArgumentException("At least one key column must be specified.");

        foreach (var key in keyColumns)
        {
            if (!sourceDf.Schema.Contains(key))
            {
                throw new ArgumentException($"Key column '{key}' not found in DataFrame.");
            }
        }

        _sourceDf = sourceDf;
        _keyColumns = keyColumns;
    }

    /// <summary>
    /// Executes parallel grouped aggregations and returns a new aggregated <see cref="DataFrame"/>.
    /// </summary>
    public DataFrame Agg(params AggregationExpr[] exprs)
    {
        ArgumentNullException.ThrowIfNull(exprs);
        if (exprs.Length == 0) throw new ArgumentException("At least one aggregation expression required.");

        int rowCount = _sourceDf.RowCount;
        var groups = new Dictionary<GroupKey, List<int>>(Math.Min(rowCount, 1024));

        // Group rows by key
        var keySeries = _keyColumns.Select(k => _sourceDf[k]).ToArray();
        for (int i = 0; i < rowCount; i++)
        {
            var keyValues = new object?[keySeries.Length];
            for (int k = 0; k < keySeries.Length; k++)
            {
                keyValues[k] = keySeries[k][i];
            }
            var gk = new GroupKey(keyValues);
            if (!groups.TryGetValue(gk, out var rowList))
            {
                rowList = new List<int>();
                groups[gk] = rowList;
            }
            rowList.Add(i);
        }

        int groupCount = groups.Count;
        var groupList = groups.ToList();

        // 1. Build output key columns
        var resultSeries = new List<Series>();
        for (int k = 0; k < _keyColumns.Length; k++)
        {
            string keyName = _keyColumns[k];
            var srcCol = _sourceDf[keyName];
            var keyArray = Array.CreateInstance(srcCol.DataType == DataType.String ? typeof(string) : typeof(object), groupCount);

            for (int g = 0; g < groupCount; g++)
            {
                var val = groupList[g].Key.Values[k];
                keyArray.SetValue(val, g);
            }

            if (srcCol.DataType == DataType.String)
            {
                var strArr = (string?[])keyArray;
                resultSeries.Add(Series.FromStrings(keyName, strArr));
            }
            else
            {
                var col = CreateColumnFromObjects(srcCol.DataType, (object?[])keyArray);
                resultSeries.Add(new Series(keyName, col));
            }
        }

        // 2. Compute aggregated columns in parallel
        for (int e = 0; e < exprs.Length; e++)
        {
            var expr = exprs[e];
            var targetSeries = _sourceDf[expr.TargetColumn];
            var aggValues = new double[groupCount];

            Parallel.For(0, groupCount, g =>
            {
                var rowIndices = groupList[g].Value;
                aggValues[g] = ComputeAgg(targetSeries, rowIndices, expr.AggType);
            });

            var aggCol = new Column<double>(aggValues, null);
            resultSeries.Add(new Series(expr.OutputName, aggCol));
        }

        return new DataFrame(resultSeries);
    }

    private static double ComputeAgg(Series series, List<int> rowIndices, AggregationType aggType)
    {
        if (rowIndices.Count == 0) return double.NaN;

        if (aggType == AggregationType.Count)
        {
            int nonNull = 0;
            for (int i = 0; i < rowIndices.Count; i++)
            {
                if (!series.IsNull(rowIndices[i])) nonNull++;
            }
            return nonNull;
        }

        var validVals = new List<double>(rowIndices.Count);
        for (int i = 0; i < rowIndices.Count; i++)
        {
            int r = rowIndices[i];
            if (!series.IsNull(r))
            {
                validVals.Add(Convert.ToDouble(series[r]));
            }
        }

        if (validVals.Count == 0) return double.NaN;

        return aggType switch
        {
            AggregationType.Sum => validVals.Sum(),
            AggregationType.Mean => validVals.Average(),
            AggregationType.Min => validVals.Min(),
            AggregationType.Max => validVals.Max(),
            AggregationType.First => validVals[0],
            AggregationType.Last => validVals[^1],
            AggregationType.Std => ComputeSampleStd(validVals),
            _ => double.NaN
        };
    }

    private static double ComputeSampleStd(List<double> values)
    {
        if (values.Count < 2) return double.NaN;
        double mean = values.Average();
        double sumSq = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSq / (values.Count - 1));
    }

    private static IColumn CreateColumnFromObjects(DataType dataType, object?[] values)
    {
        int len = values.Length;
        var mask = new BitmapMask(len, initialValue: true);

        return dataType switch
        {
            DataType.Int32 => CreateTypedCol<int>(values, mask, Convert.ToInt32),
            DataType.Int64 => CreateTypedCol<long>(values, mask, Convert.ToInt64),
            DataType.Float32 => CreateTypedCol<float>(values, mask, Convert.ToSingle),
            DataType.Float64 => CreateTypedCol<double>(values, mask, Convert.ToDouble),
            DataType.Boolean => CreateTypedCol<bool>(values, mask, Convert.ToBoolean),
            _ => CreateTypedCol<double>(values, mask, Convert.ToDouble)
        };
    }

    private static Column<T> CreateTypedCol<T>(object?[] values, BitmapMask mask, Func<object, T> converter) where T : unmanaged
    {
        var data = new T[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] is null)
            {
                mask.Set(i, false);
            }
            else
            {
                data[i] = converter(values[i]!);
            }
        }
        return new Column<T>(data, mask.All() ? null : mask);
    }

    private sealed class GroupKey : IEquatable<GroupKey>
    {
        public object?[] Values { get; }

        public GroupKey(object?[] values)
        {
            Values = values;
        }

        public bool Equals(GroupKey? other)
        {
            if (other is null || Values.Length != other.Values.Length) return false;
            for (int i = 0; i < Values.Length; i++)
            {
                if (!Equals(Values[i], other.Values[i])) return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is GroupKey other && Equals(other);

        public override int GetHashCode()
        {
            var hc = new HashCode();
            foreach (var v in Values)
            {
                if (v is not null) hc.Add(v);
            }
            return hc.ToHashCode();
        }
    }
}
