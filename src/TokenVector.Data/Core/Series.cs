using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using TokenVector.Data.Common;
using TokenVector.Data.Compute;

namespace TokenVector.Data.Core;

/// <summary>
/// 1D Named Column container with vectorized SIMD mathematics, window transformations, and statistical aggregations.
/// </summary>
public sealed class Series : IEnumerable<object?>, IDisposable
{
    private string _name;
    private IColumn _column;
    private bool _isDisposed;

    public string Name => _name;
    public IColumn Column => _column;
    public DataType DataType => _column.DataType;
    public int Length => _column.Length;
    public int NullCount => _column.NullCount;
    public BitmapMask? NullMask => _column.NullMask;

    #region Constructors & Factory Methods

    public Series(string name, IColumn column)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(column);
        _name = name;
        _column = column;
    }

    public static Series FromValues<T>(string name, T[] values, BitmapMask? mask = null) where T : unmanaged
    {
        var col = new Column<T>(values, mask);
        return new Series(name, col);
    }

    public static Series FromValues<T>(string name, ReadOnlySpan<T> values, BitmapMask? mask = null) where T : unmanaged
    {
        var col = new Column<T>(values, mask);
        return new Series(name, col);
    }

    public static Series FromStrings(string name, IReadOnlyList<string?> strings)
    {
        var col = StringColumn.FromStrings(strings);
        return new Series(name, col);
    }

    public static Series FromStrings(string name, params string?[] strings) =>
        FromStrings(name, (IReadOnlyList<string?>)strings);

    #endregion

    #region Accessors

    public object? this[int index] => _column.GetBoxed(index);

    public bool IsNull(int index) => _column.IsNull(index);

    public T? GetValue<T>(int index) where T : unmanaged
    {
        if (_column is Column<T> typed)
        {
            return typed.GetValue(index);
        }
        object? boxed = _column.GetBoxed(index);
        if (boxed is null) return null;
        return (T)Convert.ChangeType(boxed, typeof(T), CultureInfo.InvariantCulture);
    }

    public string? GetString(int index)
    {
        if (_column is StringColumn strCol)
        {
            return strCol.GetString(index);
        }
        return _column.GetBoxed(index)?.ToString();
    }

    #endregion

    #region Slicing & Transforms

    public Series Slice(int offset, int length) =>
        new(_name, _column.Slice(offset, length));

    public Series Take(ReadOnlySpan<int> indices) =>
        new(_name, _column.Take(indices));

    public Series Filter(BitmapMask mask) =>
        new(_name, _column.Filter(mask));

    public Series Clone() =>
        new(_name, _column.Clone());

    public Series Rename(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        return new Series(newName, _column.Clone());
    }

    public Series Cast(DataType targetType) =>
        new(_name, _column.Cast(targetType));

    public Series DropNull()
    {
        if (_column.NullMask is null) return Clone();
        return Filter(_column.NullMask);
    }

    public Series FillNull(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        int len = Length;
        var mask = NullMask;
        if (mask is null || mask.All()) return Clone();

        if (DataType == DataType.String)
        {
            string fillStr = value.ToString() ?? "";
            var strCol = (StringColumn)_column;
            var list = new string?[len];
            for (int i = 0; i < len; i++)
            {
                list[i] = strCol.IsNull(i) ? fillStr : strCol.GetString(i);
            }
            return FromStrings(_name, list);
        }

        return DataType switch
        {
            DataType.Float64 => FillNullTyped<double>(Convert.ToDouble(value)),
            DataType.Float32 => FillNullTyped<float>(Convert.ToSingle(value)),
            DataType.Int32 => FillNullTyped<int>(Convert.ToInt32(value)),
            DataType.Int64 => FillNullTyped<long>(Convert.ToInt64(value)),
            _ => Clone()
        };
    }

    private Series FillNullTyped<T>(T fillVal) where T : unmanaged
    {
        var col = (Column<T>)_column;
        var data = (T[])col.RawData.Clone();
        for (int i = 0; i < col.Length; i++)
        {
            if (col.IsNull(i))
            {
                data[i] = fillVal;
            }
        }
        return new Series(_name, new Column<T>(data, null));
    }

    #endregion

    #region Statistical Aggregations

    public double Sum() => Aggregations.Sum(_column);
    public double Mean() => Aggregations.Mean(_column);
    public double Min() => Aggregations.Min(_column);
    public double Max() => Aggregations.Max(_column);
    public double Median() => Aggregations.Median(_column);
    public double Std(int ddof = 1) => Aggregations.Std(_column, ddof);
    public double Variance(int ddof = 1) => Aggregations.Variance(_column, ddof);
    public double Quantile(double q) => Aggregations.Quantile(_column, q);
    public int Count() => Length;
    public int NonNullCount() => Length - NullCount;

    #endregion

    #region Window & Analytical Functions

    public Series RollingMean(int windowSize, int minPeriods = 1) =>
        new($"{_name}_rolling_mean", WindowFunctions.RollingMean(_column, windowSize, minPeriods));

    public Series RollingSum(int windowSize, int minPeriods = 1) =>
        new($"{_name}_rolling_sum", WindowFunctions.RollingSum(_column, windowSize, minPeriods));

    public Series RollingStd(int windowSize, int minPeriods = 2) =>
        new($"{_name}_rolling_std", WindowFunctions.RollingStd(_column, windowSize, minPeriods));

    public Series Shift(int periods) =>
        new($"{_name}_shift_{periods}", WindowFunctions.Shift(_column, periods));

    public Series Diff(int periods = 1) =>
        new($"{_name}_diff_{periods}", WindowFunctions.Diff(_column, periods));

    public Series CumSum() =>
        new($"{_name}_cumsum", WindowFunctions.CumSum(_column));

    public Series Rank(bool ascending = true) =>
        new($"{_name}_rank", WindowFunctions.Rank(_column, ascending));

    #endregion

    #region Mathematical Operations

    public Series Abs()
    {
        return DataType switch
        {
            DataType.Float64 => new Series(_name, VectorMath.Abs((Column<double>)_column)),
            DataType.Float32 => new Series(_name, VectorMath.Abs((Column<float>)_column)),
            DataType.Int32 => new Series(_name, VectorMath.Abs((Column<int>)_column)),
            DataType.Int64 => new Series(_name, VectorMath.Abs((Column<long>)_column)),
            _ => throw new NotSupportedException($"Abs is not supported on {DataType}")
        };
    }

    public Series Sqrt()
    {
        return DataType switch
        {
            DataType.Float64 => new Series(_name, VectorMath.Sqrt((Column<double>)_column)),
            DataType.Float32 => new Series(_name, VectorMath.Sqrt((Column<float>)_column)),
            DataType.Int32 => new Series(_name, VectorMath.Sqrt((Column<int>)_column)),
            DataType.Int64 => new Series(_name, VectorMath.Sqrt((Column<long>)_column)),
            _ => throw new NotSupportedException($"Sqrt is not supported on {DataType}")
        };
    }

    public Series Exp()
    {
        return DataType switch
        {
            DataType.Float64 => new Series(_name, VectorMath.Exp((Column<double>)_column)),
            DataType.Float32 => new Series(_name, VectorMath.Exp((Column<float>)_column)),
            DataType.Int32 => new Series(_name, VectorMath.Exp((Column<int>)_column)),
            DataType.Int64 => new Series(_name, VectorMath.Exp((Column<long>)_column)),
            _ => throw new NotSupportedException($"Exp is not supported on {DataType}")
        };
    }

    public Series Log()
    {
        return DataType switch
        {
            DataType.Float64 => new Series(_name, VectorMath.Log((Column<double>)_column)),
            DataType.Float32 => new Series(_name, VectorMath.Log((Column<float>)_column)),
            DataType.Int32 => new Series(_name, VectorMath.Log((Column<int>)_column)),
            DataType.Int64 => new Series(_name, VectorMath.Log((Column<long>)_column)),
            _ => throw new NotSupportedException($"Log is not supported on {DataType}")
        };
    }

    public Series Pow(double exponent)
    {
        return DataType switch
        {
            DataType.Float64 => new Series(_name, VectorMath.Pow((Column<double>)_column, exponent)),
            DataType.Float32 => new Series(_name, VectorMath.Pow((Column<float>)_column, exponent)),
            DataType.Int32 => new Series(_name, VectorMath.Pow((Column<int>)_column, exponent)),
            DataType.Int64 => new Series(_name, VectorMath.Pow((Column<long>)_column, exponent)),
            _ => throw new NotSupportedException($"Pow is not supported on {DataType}")
        };
    }

    #endregion

    #region Operators

    public static Series operator +(Series a, Series b)
    {
        if (a.DataType != b.DataType) throw new InvalidOperationException($"Type mismatch: {a.DataType} and {b.DataType}");
        return a.DataType switch
        {
            DataType.Float64 => new Series(a.Name, VectorMath.Add((Column<double>)a.Column, (Column<double>)b.Column)),
            DataType.Float32 => new Series(a.Name, VectorMath.Add((Column<float>)a.Column, (Column<float>)b.Column)),
            DataType.Int32 => new Series(a.Name, VectorMath.Add((Column<int>)a.Column, (Column<int>)b.Column)),
            DataType.Int64 => new Series(a.Name, VectorMath.Add((Column<long>)a.Column, (Column<long>)b.Column)),
            _ => throw new NotSupportedException($"Addition not supported for {a.DataType}")
        };
    }

    public static Series operator -(Series a, Series b)
    {
        if (a.DataType != b.DataType) throw new InvalidOperationException($"Type mismatch: {a.DataType} and {b.DataType}");
        return a.DataType switch
        {
            DataType.Float64 => new Series(a.Name, VectorMath.Subtract((Column<double>)a.Column, (Column<double>)b.Column)),
            DataType.Float32 => new Series(a.Name, VectorMath.Subtract((Column<float>)a.Column, (Column<float>)b.Column)),
            DataType.Int32 => new Series(a.Name, VectorMath.Subtract((Column<int>)a.Column, (Column<int>)b.Column)),
            DataType.Int64 => new Series(a.Name, VectorMath.Subtract((Column<long>)a.Column, (Column<long>)b.Column)),
            _ => throw new NotSupportedException($"Subtraction not supported for {a.DataType}")
        };
    }

    public static Series operator *(Series a, Series b)
    {
        if (a.DataType != b.DataType) throw new InvalidOperationException($"Type mismatch: {a.DataType} and {b.DataType}");
        return a.DataType switch
        {
            DataType.Float64 => new Series(a.Name, VectorMath.Multiply((Column<double>)a.Column, (Column<double>)b.Column)),
            DataType.Float32 => new Series(a.Name, VectorMath.Multiply((Column<float>)a.Column, (Column<float>)b.Column)),
            DataType.Int32 => new Series(a.Name, VectorMath.Multiply((Column<int>)a.Column, (Column<int>)b.Column)),
            DataType.Int64 => new Series(a.Name, VectorMath.Multiply((Column<long>)a.Column, (Column<long>)b.Column)),
            _ => throw new NotSupportedException($"Multiplication not supported for {a.DataType}")
        };
    }

    public static Series operator /(Series a, Series b)
    {
        if (a.DataType != b.DataType) throw new InvalidOperationException($"Type mismatch: {a.DataType} and {b.DataType}");
        return a.DataType switch
        {
            DataType.Float64 => new Series(a.Name, VectorMath.Divide((Column<double>)a.Column, (Column<double>)b.Column)),
            DataType.Float32 => new Series(a.Name, VectorMath.Divide((Column<float>)a.Column, (Column<float>)b.Column)),
            DataType.Int32 => new Series(a.Name, VectorMath.Divide((Column<int>)a.Column, (Column<int>)b.Column)),
            DataType.Int64 => new Series(a.Name, VectorMath.Divide((Column<long>)a.Column, (Column<long>)b.Column)),
            _ => throw new NotSupportedException($"Division not supported for {a.DataType}")
        };
    }

    public static Series operator +(Series a, double scalar) =>
        new(a.Name, VectorMath.AddScalar((Column<double>)a.Cast(DataType.Float64).Column, scalar));

    public static Series operator *(Series a, double scalar) =>
        new(a.Name, VectorMath.MultiplyScalar((Column<double>)a.Cast(DataType.Float64).Column, scalar));

    public static BitmapMask operator >(Series a, double scalar) =>
        VectorMath.GreaterThanScalar((Column<double>)a.Cast(DataType.Float64).Column, scalar);

    public static BitmapMask operator >=(Series a, double scalar) =>
        VectorMath.GreaterThanOrEqualScalar((Column<double>)a.Cast(DataType.Float64).Column, scalar);

    public static BitmapMask operator <(Series a, double scalar) =>
        VectorMath.LessThanScalar((Column<double>)a.Cast(DataType.Float64).Column, scalar);

    public static BitmapMask operator <=(Series a, double scalar) =>
        VectorMath.LessThanOrEqualScalar((Column<double>)a.Cast(DataType.Float64).Column, scalar);

    public static BitmapMask operator ==(Series a, double scalar) =>
        VectorMath.EqualScalar((Column<double>)a.Cast(DataType.Float64).Column, scalar);

    public static BitmapMask operator !=(Series a, double scalar) =>
        VectorMath.NotEqualScalar((Column<double>)a.Cast(DataType.Float64).Column, scalar);

    #endregion

    public IEnumerator<object?> GetEnumerator()
    {
        for (int i = 0; i < Length; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString()
    {
        var sb = new StringBuilder($"Series '{_name}' ({DataType}, Length: {Length}, Nulls: {NullCount}) [\n");
        int previewCount = Math.Min(5, Length);
        for (int i = 0; i < previewCount; i++)
        {
            sb.AppendLine($"  [{i}] => {this[i] ?? "null"}");
        }
        if (Length > previewCount)
        {
            sb.AppendLine($"  ... ({Length - previewCount} more rows)");
        }
        sb.Append(']');
        return sb.ToString();
    }

    public override bool Equals(object? obj) => base.Equals(obj);
    public override int GetHashCode() => base.GetHashCode();

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _column.Dispose();
        }
    }
}
