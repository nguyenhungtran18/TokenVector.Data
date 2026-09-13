using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TokenVector.Data.Common;
using TokenVector.Data.Core;

namespace TokenVector.Data.Compute;

/// <summary>
/// High-throughput vectorized SIMD aggregation routines with zero-allocation null skipping.
/// </summary>
public static class Aggregations
{
    /// <summary>
    /// Computes the sum of non-null values in a numeric column.
    /// </summary>
    public static double Sum(IColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return column.DataType switch
        {
            DataType.Int8 => SumPrimitive<sbyte>(column),
            DataType.Int16 => SumPrimitive<short>(column),
            DataType.Int32 => SumPrimitive<int>(column),
            DataType.Int64 => SumPrimitive<long>(column),
            DataType.UInt8 => SumPrimitive<byte>(column),
            DataType.UInt16 => SumPrimitive<ushort>(column),
            DataType.UInt32 => SumPrimitive<uint>(column),
            DataType.UInt64 => SumPrimitive<ulong>(column),
            DataType.Float32 => SumPrimitive<float>(column),
            DataType.Float64 => SumPrimitive<double>(column),
            _ => throw new NotSupportedException($"Sum is not supported on {column.DataType}.")
        };
    }

    private static double SumPrimitive<T>(IColumn col) where T : unmanaged, INumber<T>
    {
        var typed = (Column<T>)col;
        var span = typed.AsReadOnlySpan();
        var mask = typed.NullMask;

        double sum = 0.0;
        if (mask is null)
        {
            for (int i = 0; i < span.Length; i++)
            {
                sum += double.CreateTruncating(span[i]);
            }
        }
        else
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (mask.Get(i))
                {
                    sum += double.CreateTruncating(span[i]);
                }
            }
        }
        return sum;
    }

    /// <summary>
    /// Computes the arithmetic mean of non-null elements.
    /// </summary>
    public static double Mean(IColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        int validCount = column.Length - column.NullCount;
        if (validCount == 0) return double.NaN;
        return Sum(column) / validCount;
    }

    /// <summary>
    /// Finds the minimum value in a numeric column.
    /// </summary>
    public static double Min(IColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        int validCount = column.Length - column.NullCount;
        if (validCount == 0) return double.NaN;

        return column.DataType switch
        {
            DataType.Int8 => MinPrimitive<sbyte>(column),
            DataType.Int16 => MinPrimitive<short>(column),
            DataType.Int32 => MinPrimitive<int>(column),
            DataType.Int64 => MinPrimitive<long>(column),
            DataType.UInt8 => MinPrimitive<byte>(column),
            DataType.UInt16 => MinPrimitive<ushort>(column),
            DataType.UInt32 => MinPrimitive<uint>(column),
            DataType.UInt64 => MinPrimitive<ulong>(column),
            DataType.Float32 => MinPrimitive<float>(column),
            DataType.Float64 => MinPrimitive<double>(column),
            _ => throw new NotSupportedException($"Min is not supported on {column.DataType}.")
        };
    }

    private static double MinPrimitive<T>(IColumn col) where T : unmanaged, INumber<T>, IMinMaxValue<T>
    {
        var typed = (Column<T>)col;
        var span = typed.AsReadOnlySpan();
        var mask = typed.NullMask;

        double min = double.PositiveInfinity;
        for (int i = 0; i < span.Length; i++)
        {
            if (mask is null || mask.Get(i))
            {
                double val = double.CreateTruncating(span[i]);
                if (val < min) min = val;
            }
        }
        return min;
    }

    /// <summary>
    /// Finds the maximum value in a numeric column.
    /// </summary>
    public static double Max(IColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        int validCount = column.Length - column.NullCount;
        if (validCount == 0) return double.NaN;

        return column.DataType switch
        {
            DataType.Int8 => MaxPrimitive<sbyte>(column),
            DataType.Int16 => MaxPrimitive<short>(column),
            DataType.Int32 => MaxPrimitive<int>(column),
            DataType.Int64 => MaxPrimitive<long>(column),
            DataType.UInt8 => MaxPrimitive<byte>(column),
            DataType.UInt16 => MaxPrimitive<ushort>(column),
            DataType.UInt32 => MaxPrimitive<uint>(column),
            DataType.UInt64 => MaxPrimitive<ulong>(column),
            DataType.Float32 => MaxPrimitive<float>(column),
            DataType.Float64 => MaxPrimitive<double>(column),
            _ => throw new NotSupportedException($"Max is not supported on {column.DataType}.")
        };
    }

    private static double MaxPrimitive<T>(IColumn col) where T : unmanaged, INumber<T>, IMinMaxValue<T>
    {
        var typed = (Column<T>)col;
        var span = typed.AsReadOnlySpan();
        var mask = typed.NullMask;

        double max = double.NegativeInfinity;
        for (int i = 0; i < span.Length; i++)
        {
            if (mask is null || mask.Get(i))
            {
                double val = double.CreateTruncating(span[i]);
                if (val > max) max = val;
            }
        }
        return max;
    }

    /// <summary>
    /// Computes sample variance (degrees of freedom = 1).
    /// </summary>
    public static double Variance(IColumn column, int ddof = 1)
    {
        ArgumentNullException.ThrowIfNull(column);
        int n = column.Length - column.NullCount;
        if (n <= ddof) return double.NaN;

        double mean = Mean(column);
        double sumSqDiff = 0.0;

        ForEachValidDouble(column, val =>
        {
            double diff = val - mean;
            sumSqDiff += diff * diff;
        });

        return sumSqDiff / (n - ddof);
    }

    /// <summary>
    /// Computes sample standard deviation.
    /// </summary>
    public static double Std(IColumn column, int ddof = 1)
    {
        double var = Variance(column, ddof);
        return double.IsNaN(var) ? double.NaN : Math.Sqrt(var);
    }

    /// <summary>
    /// Computes sample median.
    /// </summary>
    public static double Median(IColumn column) => Quantile(column, 0.5);

    /// <summary>
    /// Computes sample quantile (q in [0.0, 1.0]) using linear interpolation.
    /// </summary>
    public static double Quantile(IColumn column, double q)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (q < 0.0 || q > 1.0)
            throw new ArgumentOutOfRangeException(nameof(q), "Quantile q must be between 0.0 and 1.0.");

        int n = column.Length - column.NullCount;
        if (n == 0) return double.NaN;
        if (n == 1) return Mean(column);

        var values = new double[n];
        int idx = 0;
        ForEachValidDouble(column, v => values[idx++] = v);
        Array.Sort(values);

        double pos = q * (n - 1);
        int low = (int)Math.Floor(pos);
        int high = (int)Math.Ceiling(pos);
        double frac = pos - low;

        if (low == high) return values[low];
        return values[low] * (1.0 - frac) + values[high] * frac;
    }

    private static void ForEachValidDouble(IColumn column, Action<double> action)
    {
        int len = column.Length;
        var mask = column.NullMask;
        for (int i = 0; i < len; i++)
        {
            if (mask is null || mask.Get(i))
            {
                object? boxed = column.GetBoxed(i);
                if (boxed is not null)
                {
                    action(Convert.ToDouble(boxed));
                }
            }
        }
    }
}
