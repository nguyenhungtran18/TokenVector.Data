using System;
using System.Threading.Tasks;
using TokenVector.Data.Common;
using TokenVector.Data.Core;

namespace TokenVector.Data.Compute;

/// <summary>
/// Multi-threaded predicate filtering engine returning bitboard masks.
/// </summary>
public static class FilterEngine
{
    /// <summary>
    /// Evaluates a row predicate function in parallel across all rows.
    /// </summary>
    public static BitmapMask EvaluatePredicate(int rowCount, Func<int, bool> predicate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        ArgumentNullException.ThrowIfNull(predicate);

        var mask = new BitmapMask(rowCount);
        if (rowCount == 0) return mask;

        // Multi-threaded partition
        int partitionSize = Math.Max(1024, rowCount / Environment.ProcessorCount);
        int numPartitions = (rowCount + partitionSize - 1) / partitionSize;

        Parallel.For(0, numPartitions, p =>
        {
            int start = p * partitionSize;
            int end = Math.Min(rowCount, start + partitionSize);

            for (int i = start; i < end; i++)
            {
                if (predicate(i))
                {
                    lock (mask)
                    {
                        mask.Set(i, true);
                    }
                }
            }
        });

        return mask;
    }

    /// <summary>
    /// Combines multiple filter masks with logical AND.
    /// </summary>
    public static BitmapMask And(params BitmapMask[] masks)
    {
        if (masks.Length == 0) throw new ArgumentException("At least one mask required.");
        var result = masks[0].Clone();
        for (int i = 1; i < masks.Length; i++)
        {
            result = result.BitwiseAnd(masks[i]);
        }
        return result;
    }

    /// <summary>
    /// Combines multiple filter masks with logical OR.
    /// </summary>
    public static BitmapMask Or(params BitmapMask[] masks)
    {
        if (masks.Length == 0) throw new ArgumentException("At least one mask required.");
        var result = masks[0].Clone();
        for (int i = 1; i < masks.Length; i++)
        {
            result = result.BitwiseOr(masks[i]);
        }
        return result;
    }
}
