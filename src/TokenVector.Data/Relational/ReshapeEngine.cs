using System;
using System.Collections.Generic;
using System.Linq;
using TokenVector.Data.Common;
using TokenVector.Data.Core;

namespace TokenVector.Data.Relational;

/// <summary>
/// Tabular reshaping engine providing Pivot, Melt, Stack, Unstack, and Concat operations.
/// </summary>
public static class ReshapeEngine
{
    /// <summary>
    /// Concatenates multiple DataFrames vertically (row-wise).
    /// </summary>
    public static DataFrame ConcatVertical(params DataFrame[] dfs)
    {
        ArgumentNullException.ThrowIfNull(dfs);
        if (dfs.Length == 0) throw new ArgumentException("At least one DataFrame is required for vertical concatenation.");
        if (dfs.Length == 1) return dfs[0].Clone();

        var first = dfs[0];
        var schema = first.Schema;
        int totalRows = dfs.Sum(d => d.RowCount);

        var resultSeries = new List<Series>();

        foreach (var field in schema)
        {
            var chunks = new List<IColumn>(dfs.Length);
            foreach (var df in dfs)
            {
                if (!df.Schema.Contains(field.Name))
                {
                    throw new ArgumentException($"Schema mismatch: DataFrame missing column '{field.Name}'.");
                }
                chunks.Add(df[field.Name].Column);
            }
            var chunked = new ChunkedArray(chunks);
            var flattened = chunked.Flatten();
            resultSeries.Add(new Series(field.Name, flattened));
        }

        return new DataFrame(resultSeries);
    }

    /// <summary>
    /// Concatenates multiple DataFrames horizontally (column-wise).
    /// </summary>
    public static DataFrame ConcatHorizontal(params DataFrame[] dfs)
    {
        ArgumentNullException.ThrowIfNull(dfs);
        if (dfs.Length == 0) throw new ArgumentException("At least one DataFrame is required for horizontal concatenation.");
        if (dfs.Length == 1) return dfs[0].Clone();

        int expectedRows = dfs[0].RowCount;
        var allSeries = new List<Series>();

        foreach (var df in dfs)
        {
            if (df.RowCount != expectedRows)
            {
                throw new ArgumentException($"Row count mismatch in horizontal concat: {expectedRows} vs {df.RowCount}");
            }
            foreach (var s in df.Columns)
            {
                allSeries.Add(s.Clone());
            }
        }

        return new DataFrame(allSeries);
    }

    /// <summary>
    /// Melts (unpivots) a DataFrame from wide format to long format.
    /// </summary>
    public static DataFrame Melt(
        DataFrame df,
        string[] idVars,
        string[] valueVars,
        string varName = "variable",
        string valueName = "value")
    {
        ArgumentNullException.ThrowIfNull(df);
        ArgumentNullException.ThrowIfNull(idVars);
        ArgumentNullException.ThrowIfNull(valueVars);

        int rowCount = df.RowCount;
        int numValues = valueVars.Length;
        int outRowCount = rowCount * numValues;

        var resultSeries = new List<Series>();

        // 1. Repeat idVars
        foreach (var id in idVars)
        {
            var src = df[id];
            var repeated = RepeatSeries(src, numValues, outRowCount);
            resultSeries.Add(repeated);
        }

        // 2. Variable column (names of valueVars)
        var varStrings = new string?[outRowCount];
        int idx = 0;
        for (int v = 0; v < numValues; v++)
        {
            string colName = valueVars[v];
            for (int r = 0; r < rowCount; r++)
            {
                varStrings[idx++] = colName;
            }
        }
        resultSeries.Add(Series.FromStrings(varName, varStrings));

        // 3. Value column
        var valStrings = new string?[outRowCount];
        idx = 0;
        for (int v = 0; v < numValues; v++)
        {
            var valCol = df[valueVars[v]];
            for (int r = 0; r < rowCount; r++)
            {
                valStrings[idx++] = valCol[r]?.ToString();
            }
        }
        resultSeries.Add(Series.FromStrings(valueName, valStrings));

        return new DataFrame(resultSeries);
    }

    /// <summary>
    /// Pivots a DataFrame from long format to wide format.
    /// </summary>
    public static DataFrame Pivot(
        DataFrame df,
        string index,
        string columns,
        string values)
    {
        ArgumentNullException.ThrowIfNull(df);
        var idxSeries = df[index];
        var pivSeries = df[columns];
        var valSeries = df[values];

        // Unique index values
        var uniqueIndices = new List<object?>();
        var seenIdx = new HashSet<object?>();
        for (int i = 0; i < df.RowCount; i++)
        {
            var val = idxSeries[i];
            if (seenIdx.Add(val)) uniqueIndices.Add(val);
        }

        // Unique pivot column values
        var uniquePivots = new List<string>();
        var seenPiv = new HashSet<string>();
        for (int i = 0; i < df.RowCount; i++)
        {
            var pVal = pivSeries[i]?.ToString() ?? "null";
            if (seenPiv.Add(pVal)) uniquePivots.Add(pVal);
        }

        // Mapping (index, pivot) -> value
        var cellMap = new Dictionary<(object?, string), object?>();
        for (int i = 0; i < df.RowCount; i++)
        {
            var iVal = idxSeries[i];
            var pVal = pivSeries[i]?.ToString() ?? "null";
            cellMap[(iVal, pVal)] = valSeries[i];
        }

        var resultSeries = new List<Series>();

        // Index column
        if (idxSeries.DataType == DataType.String)
        {
            resultSeries.Add(Series.FromStrings(index, uniqueIndices.Select(x => x?.ToString()).ToArray()));
        }
        else
        {
            var doubleArr = uniqueIndices.Select(x => x is not null ? Convert.ToDouble(x) : 0.0).ToArray();
            resultSeries.Add(Series.FromValues(index, doubleArr));
        }

        // Pivot columns
        foreach (var p in uniquePivots)
        {
            var colVals = new double?[uniqueIndices.Count];
            var mask = new BitmapMask(uniqueIndices.Count);
            for (int i = 0; i < uniqueIndices.Count; i++)
            {
                if (cellMap.TryGetValue((uniqueIndices[i], p), out var rawVal) && rawVal is not null)
                {
                    colVals[i] = Convert.ToDouble(rawVal);
                    mask.Set(i, true);
                }
            }
            var dArr = colVals.Select(v => v ?? 0.0).ToArray();
            resultSeries.Add(new Series(p, new Column<double>(dArr, mask)));
        }

        return new DataFrame(resultSeries);
    }

    private static Series RepeatSeries(Series src, int numRepeats, int outCount)
    {
        if (src.DataType == DataType.String)
        {
            var strCol = (StringColumn)src.Column;
            var strings = new string?[outCount];
            int idx = 0;
            for (int rep = 0; rep < numRepeats; rep++)
            {
                for (int r = 0; r < src.Length; r++)
                {
                    strings[idx++] = strCol.GetString(r);
                }
            }
            return Series.FromStrings(src.Name, strings);
        }

        var data = new double[outCount];
        var mask = new BitmapMask(outCount);
        int outIdx = 0;
        for (int rep = 0; rep < numRepeats; rep++)
        {
            for (int r = 0; r < src.Length; r++)
            {
                if (!src.IsNull(r))
                {
                    data[outIdx] = Convert.ToDouble(src[r]);
                    mask.Set(outIdx, true);
                }
                outIdx++;
            }
        }
        return new Series(src.Name, new Column<double>(data, mask));
    }
}
