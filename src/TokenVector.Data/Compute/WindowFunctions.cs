using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using TokenVector.Data.Common;
using TokenVector.Data.Core;

namespace TokenVector.Data.Compute;

/// <summary>
/// Vectorized window and analytical functions including Rolling statistics, Lag, Lead, Diff, CumSum, and Rank.
/// </summary>
public static class WindowFunctions
{
    /// <summary>
    /// Computes moving (rolling) arithmetic mean over a sliding window in O(N).
    /// </summary>
    public static Column<double> RollingMean(IColumn column, int windowSize, int minPeriods = 1)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (windowSize <= 0) throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be > 0.");

        int len = column.Length;
        var result = new double[len];
        var mask = new BitmapMask(len);

        double runningSum = 0.0;
        int validCount = 0;

        for (int i = 0; i < len; i++)
        {
            // Add new element entering window
            if (!column.IsNull(i))
            {
                runningSum += Convert.ToDouble(column.GetBoxed(i));
                validCount++;
            }

            // Subtract element leaving window
            int leavingIdx = i - windowSize;
            if (leavingIdx >= 0 && !column.IsNull(leavingIdx))
            {
                runningSum -= Convert.ToDouble(column.GetBoxed(leavingIdx));
                validCount--;
            }

            if (validCount >= minPeriods)
            {
                result[i] = runningSum / validCount;
                mask.Set(i, true);
            }
        }

        return new Column<double>(result, mask);
    }

    /// <summary>
    /// Computes moving (rolling) sum over a sliding window in O(N).
    /// </summary>
    public static Column<double> RollingSum(IColumn column, int windowSize, int minPeriods = 1)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (windowSize <= 0) throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be > 0.");

        int len = column.Length;
        var result = new double[len];
        var mask = new BitmapMask(len);

        double runningSum = 0.0;
        int validCount = 0;

        for (int i = 0; i < len; i++)
        {
            // Add new element entering window
            if (!column.IsNull(i))
            {
                runningSum += Convert.ToDouble(column.GetBoxed(i));
                validCount++;
            }

            // Subtract element leaving window
            int leavingIdx = i - windowSize;
            if (leavingIdx >= 0 && !column.IsNull(leavingIdx))
            {
                runningSum -= Convert.ToDouble(column.GetBoxed(leavingIdx));
                validCount--;
            }

            if (validCount >= minPeriods)
            {
                result[i] = runningSum;
                mask.Set(i, true);
            }
        }

        return new Column<double>(result, mask);
    }

    /// <summary>
    /// Computes moving (rolling) sample standard deviation over a sliding window.
    /// </summary>
    public static Column<double> RollingStd(IColumn column, int windowSize, int minPeriods = 2)
    {
        ArgumentNullException.ThrowIfNull(column);
        if (windowSize <= 1) throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size for std must be > 1.");

        int len = column.Length;
        var result = new double[len];
        var mask = new BitmapMask(len);

        for (int i = 0; i < len; i++)
        {
            int start = Math.Max(0, i - windowSize + 1);
            int count = 0;
            double sum = 0.0;

            for (int j = start; j <= i; j++)
            {
                if (!column.IsNull(j))
                {
                    sum += Convert.ToDouble(column.GetBoxed(j));
                    count++;
                }
            }

            if (count >= minPeriods && count > 1)
            {
                double mean = sum / count;
                double sumSq = 0.0;
                for (int j = start; j <= i; j++)
                {
                    if (!column.IsNull(j))
                    {
                        double diff = Convert.ToDouble(column.GetBoxed(j)) - mean;
                        sumSq += diff * diff;
                    }
                }
                result[i] = Math.Sqrt(sumSq / (count - 1));
                mask.Set(i, true);
            }
        }

        return new Column<double>(result, mask);
    }

    /// <summary>
    /// Shifts values by given periods (positive shifts forward = Lag, negative shifts backward = Lead).
    /// </summary>
    public static IColumn Shift(IColumn column, int periods)
    {
        ArgumentNullException.ThrowIfNull(column);
        int len = column.Length;

        if (column.DataType == DataType.String)
        {
            var strCol = (StringColumn)column;
            var newStrings = new string?[len];
            for (int i = 0; i < len; i++)
            {
                int srcIdx = i - periods;
                if (srcIdx >= 0 && srcIdx < len)
                {
                    newStrings[i] = strCol.GetString(srcIdx);
                }
                else
                {
                    newStrings[i] = null;
                }
            }
            return StringColumn.FromStrings(newStrings);
        }

        return column.DataType switch
        {
            DataType.Int8 => ShiftTyped<sbyte>(column, periods),
            DataType.Int16 => ShiftTyped<short>(column, periods),
            DataType.Int32 => ShiftTyped<int>(column, periods),
            DataType.Int64 => ShiftTyped<long>(column, periods),
            DataType.UInt8 => ShiftTyped<byte>(column, periods),
            DataType.UInt16 => ShiftTyped<ushort>(column, periods),
            DataType.UInt32 => ShiftTyped<uint>(column, periods),
            DataType.UInt64 => ShiftTyped<ulong>(column, periods),
            DataType.Float32 => ShiftTyped<float>(column, periods),
            DataType.Float64 => ShiftTyped<double>(column, periods),
            DataType.Boolean => ShiftTyped<bool>(column, periods),
            DataType.DateTime64 => ShiftTyped<long>(column, periods),
            _ => throw new NotSupportedException($"Shift not supported on {column.DataType}.")
        };
    }

    private static Column<T> ShiftTyped<T>(IColumn col, int periods) where T : unmanaged
    {
        var typed = (Column<T>)col;
        int len = typed.Length;
        var resultData = new T[len];
        var mask = new BitmapMask(len);

        for (int i = 0; i < len; i++)
        {
            int srcIdx = i - periods;
            if (srcIdx >= 0 && srcIdx < len)
            {
                if (!typed.IsNull(srcIdx))
                {
                    resultData[i] = typed[srcIdx];
                    mask.Set(i, true);
                }
            }
        }

        return new Column<T>(resultData, mask);
    }

    /// <summary>
    /// Computes first or n-th order discrete differences: $y_t = x_t - x_{t - periods}$.
    /// </summary>
    public static Column<double> Diff(IColumn column, int periods = 1)
    {
        ArgumentNullException.ThrowIfNull(column);
        int len = column.Length;
        var result = new double[len];
        var mask = new BitmapMask(len);

        for (int i = 0; i < len; i++)
        {
            int prevIdx = i - periods;
            if (prevIdx >= 0 && prevIdx < len)
            {
                if (!column.IsNull(i) && !column.IsNull(prevIdx))
                {
                    double curr = Convert.ToDouble(column.GetBoxed(i));
                    double prev = Convert.ToDouble(column.GetBoxed(prevIdx));
                    result[i] = curr - prev;
                    mask.Set(i, true);
                }
            }
        }

        return new Column<double>(result, mask);
    }

    /// <summary>
    /// Computes cumulative sum over the column.
    /// </summary>
    public static Column<double> CumSum(IColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        int len = column.Length;
        var result = new double[len];
        var mask = new BitmapMask(len);

        double runningSum = 0.0;
        for (int i = 0; i < len; i++)
        {
            if (!column.IsNull(i))
            {
                runningSum += Convert.ToDouble(column.GetBoxed(i));
                result[i] = runningSum;
                mask.Set(i, true);
            }
        }

        return new Column<double>(result, mask);
    }

    /// <summary>
    /// Computes numerical ranks of elements (1-based ranking).
    /// </summary>
    public static Column<double> Rank(IColumn column, bool ascending = true)
    {
        ArgumentNullException.ThrowIfNull(column);
        int len = column.Length;
        var list = new List<(double val, int originalIndex)>(len);

        for (int i = 0; i < len; i++)
        {
            if (!column.IsNull(i))
            {
                list.Add((Convert.ToDouble(column.GetBoxed(i)), i));
            }
        }

        if (ascending)
            list.Sort((a, b) => a.val.CompareTo(b.val));
        else
            list.Sort((a, b) => b.val.CompareTo(a.val));

        var ranks = new double[len];
        var mask = new BitmapMask(len);

        for (int i = 0; i < list.Count; i++)
        {
            ranks[list[i].originalIndex] = i + 1.0;
            mask.Set(list[i].originalIndex, true);
        }

        return new Column<double>(ranks, mask);
    }
}
