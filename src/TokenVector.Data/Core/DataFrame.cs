using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TokenVector.Data.Common;
using TokenVector.Data.Compute;
using TokenVector.Data.Relational;

namespace TokenVector.Data.Core;

/// <summary>
/// Ultra-high-performance 2D Columnar Tabular container aligned with Apache Arrow memory layout.
/// Provides vectorized math, parallel filtering, grouped aggregations, relational joins, and zero-copy tensor bridge.
/// </summary>
public sealed class DataFrame : IEnumerable<Series>, IDisposable
{
    private readonly Dictionary<string, int> _columnLookup;
    private readonly List<Series> _columns;
    private readonly Schema _schema;
    private int _rowCount;
    private bool _isDisposed;

    public int RowCount => _rowCount;
    public int ColumnCount => _columns.Count;
    public (int Rows, int Columns) Shape => (_rowCount, _columns.Count);
    public Schema Schema => _schema;
    public IReadOnlyList<Series> Columns => _columns;
    public IReadOnlyList<string> ColumnNames => _schema.Names;

    #region Constructors & Factory Methods

    public DataFrame(IEnumerable<Series> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns.ToList();
        if (_columns.Count == 0)
        {
            _rowCount = 0;
            _schema = new Schema(Array.Empty<Field>());
            _columnLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        _rowCount = _columns[0].Length;
        var fields = new Field[_columns.Count];
        _columnLookup = new Dictionary<string, int>(_columns.Count, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < _columns.Count; i++)
        {
            var col = _columns[i];
            if (col.Length != _rowCount)
            {
                throw new ArgumentException($"Column '{col.Name}' length {col.Length} does not match DataFrame row count {_rowCount}.");
            }
            fields[i] = new Field(col.Name, col.DataType, col.NullCount > 0);
            if (!_columnLookup.TryAdd(col.Name, i))
            {
                throw new ArgumentException($"Duplicate column name '{col.Name}' in DataFrame.");
            }
        }

        _schema = new Schema(fields);
    }

    public DataFrame(params Series[] columns) : this((IEnumerable<Series>)columns) { }

    public static DataFrame Empty => new(Array.Empty<Series>());

    #endregion

    #region Indexers & Slicing

    public Series this[string columnName]
    {
        get
        {
            if (!_columnLookup.TryGetValue(columnName, out int idx))
            {
                throw new KeyNotFoundException($"Column '{columnName}' not found. Available: [{string.Join(", ", ColumnNames)}]");
            }
            return _columns[idx];
        }
    }

    public Series this[int columnIndex]
    {
        get
        {
            if ((uint)columnIndex >= (uint)_columns.Count)
                throw new ArgumentOutOfRangeException(nameof(columnIndex));
            return _columns[columnIndex];
        }
    }

    public object? this[int row, string columnName] => this[columnName][row];
    public object? this[int row, int columnIndex] => this[columnIndex][row];

    public DataFrame this[BitmapMask mask] => Filter(mask);

    public DataFrame this[Range rowRange]
    {
        get
        {
            var (offset, length) = rowRange.GetOffsetAndLength(_rowCount);
            return Slice(offset, length);
        }
    }

    public DataFrame this[Range rowRange, params string[] columnNames]
    {
        get
        {
            var (offset, length) = rowRange.GetOffsetAndLength(_rowCount);
            var selected = Select(columnNames);
            return selected.Slice(offset, length);
        }
    }

    #endregion

    #region Query & Selection

    public DataFrame Select(params string[] columnNames)
    {
        ArgumentNullException.ThrowIfNull(columnNames);
        var seriesList = new List<Series>(columnNames.Length);
        foreach (var name in columnNames)
        {
            seriesList.Add(this[name].Clone());
        }
        return new DataFrame(seriesList);
    }

    public DataFrame Drop(params string[] columnNames)
    {
        ArgumentNullException.ThrowIfNull(columnNames);
        var dropSet = new HashSet<string>(columnNames, StringComparer.OrdinalIgnoreCase);
        var keepSeries = _columns.Where(c => !dropSet.Contains(c.Name)).Select(c => c.Clone());
        return new DataFrame(keepSeries);
    }

    public DataFrame WithColumn(string name, Series series)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(series);

        if (series.Length != _rowCount && _rowCount != 0)
        {
            throw new ArgumentException($"Series length {series.Length} does not match DataFrame row count {_rowCount}.");
        }

        var newCols = new List<Series>(_columns.Count + 1);
        bool replaced = false;

        foreach (var c in _columns)
        {
            if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                newCols.Add(series.Rename(name));
                replaced = true;
            }
            else
            {
                newCols.Add(c.Clone());
            }
        }

        if (!replaced)
        {
            newCols.Add(series.Rename(name));
        }

        return new DataFrame(newCols);
    }

    public DataFrame WithColumnRenamed(string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        var newCols = _columns.Select(c =>
            string.Equals(c.Name, oldName, StringComparison.OrdinalIgnoreCase) ? c.Rename(newName) : c.Clone());

        return new DataFrame(newCols);
    }

    public DataFrame Filter(BitmapMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        var indices = mask.ToIndices();
        var filtered = _columns.Select(c => c.Take(indices));
        return new DataFrame(filtered);
    }

    public DataFrame Filter(Func<int, bool> predicate)
    {
        var mask = FilterEngine.EvaluatePredicate(_rowCount, predicate);
        return Filter(mask);
    }

    public DataFrame Slice(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > _rowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Invalid slice range.");
        }
        var sliced = _columns.Select(c => c.Slice(offset, length));
        return new DataFrame(sliced);
    }

    public DataFrame Head(int n = 5) => Slice(0, Math.Min(n, _rowCount));
    public DataFrame Tail(int n = 5)
    {
        int count = Math.Min(n, _rowCount);
        return Slice(_rowCount - count, count);
    }

    public DataFrame Clone() => new(_columns.Select(c => c.Clone()));

    #endregion

    #region Sorting & Null Handling

    public DataFrame SortBy(string columnName, bool ascending = true)
    {
        var sortCol = this[columnName];
        var rowIndices = Enumerable.Range(0, _rowCount).ToArray();

        Array.Sort(rowIndices, (i, j) =>
        {
            bool nullI = sortCol.IsNull(i);
            bool nullJ = sortCol.IsNull(j);
            if (nullI && nullJ) return 0;
            if (nullI) return 1;
            if (nullJ) return -1;

            object? valI = sortCol[i];
            object? valJ = sortCol[j];
            int cmp = Comparer<object>.Default.Compare(valI, valJ);
            return ascending ? cmp : -cmp;
        });

        var sortedCols = _columns.Select(c => c.Take(rowIndices));
        return new DataFrame(sortedCols);
    }

    public DataFrame DropNull(params string[]? subset)
    {
        var colsToCheck = (subset is not null && subset.Length > 0)
            ? subset.Select(s => this[s]).ToArray()
            : _columns.ToArray();

        var mask = new BitmapMask(_rowCount, initialValue: true);
        for (int i = 0; i < _rowCount; i++)
        {
            for (int c = 0; c < colsToCheck.Length; c++)
            {
                if (colsToCheck[c].IsNull(i))
                {
                    mask.Set(i, false);
                    break;
                }
            }
        }

        return Filter(mask);
    }

    public DataFrame FillNull(object value, params string[]? subset)
    {
        var targetSet = subset is not null && subset.Length > 0
            ? new HashSet<string>(subset, StringComparer.OrdinalIgnoreCase)
            : null;

        var newCols = _columns.Select(c =>
            (targetSet is null || targetSet.Contains(c.Name)) ? c.FillNull(value) : c.Clone());

        return new DataFrame(newCols);
    }

    #endregion

    #region Relational & Reshaping

    public GroupByContext GroupBy(params string[] keyColumns) =>
        new(this, keyColumns);

    public DataFrame Join(
        DataFrame right,
        string leftOn,
        string rightOn,
        JoinType joinType = JoinType.Inner,
        string leftSuffix = "_left",
        string rightSuffix = "_right") =>
        JoinEngine.Join(this, right, leftOn, rightOn, joinType, leftSuffix, rightSuffix);

    public DataFrame AsOfJoin(
        DataFrame right,
        string leftOn,
        string rightOn,
        string? by = null,
        AsOfDirection direction = AsOfDirection.Backward,
        double? tolerance = null,
        string leftSuffix = "_left",
        string rightSuffix = "_right") =>
        JoinEngine.AsOfJoin(this, right, leftOn, rightOn, by, direction, tolerance, leftSuffix, rightSuffix);

    public DataFrame Pivot(string index, string columns, string values) =>
        ReshapeEngine.Pivot(this, index, columns, values);

    public DataFrame Melt(string[] idVars, string[] valueVars, string varName = "variable", string valueName = "value") =>
        ReshapeEngine.Melt(this, idVars, valueVars, varName, valueName);

    #endregion

    #region Statistical Summary

    public DataFrame Describe()
    {
        var numericCols = _columns.Where(c => DataTypeHelper.IsNumeric(c.DataType)).ToList();
        var statsNames = new[] { "count", "mean", "std", "min", "25%", "50%", "75%", "max" };
        var resultCols = new List<Series> { Series.FromStrings("statistic", statsNames) };

        foreach (var col in numericCols)
        {
            var values = new double[8];
            values[0] = col.NonNullCount();
            values[1] = col.Mean();
            values[2] = col.Std();
            values[3] = col.Min();
            values[4] = col.Quantile(0.25);
            values[5] = col.Median();
            values[6] = col.Quantile(0.75);
            values[7] = col.Max();

            resultCols.Add(Series.FromValues(col.Name, values));
        }

        return new DataFrame(resultCols);
    }

    #endregion

    public IEnumerator<Series> GetEnumerator() => _columns.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"DataFrame [{RowCount} rows x {ColumnCount} columns]");
        sb.AppendLine(string.Join(" | ", _columns.Select(c => $"{c.Name} ({c.DataType})")));
        sb.AppendLine(new string('-', Math.Max(20, ColumnCount * 15)));

        int previewRows = Math.Min(10, RowCount);
        for (int r = 0; r < previewRows; r++)
        {
            var rowVals = _columns.Select(c => c[r]?.ToString() ?? "null");
            sb.AppendLine(string.Join(" | ", rowVals));
        }

        if (RowCount > previewRows)
        {
            sb.AppendLine($"... ({RowCount - previewRows} more rows)");
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            foreach (var col in _columns)
            {
                col.Dispose();
            }
        }
    }
}
