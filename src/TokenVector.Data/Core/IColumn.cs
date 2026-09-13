using System;
using TokenVector.Data.Common;

namespace TokenVector.Data.Core;

/// <summary>
/// Core abstraction for high-performance columnar data storage.
/// </summary>
public interface IColumn : IDisposable
{
    /// <summary>Physical data type stored in this column.</summary>
    DataType DataType { get; }

    /// <summary>Total number of elements/rows in this column.</summary>
    int Length { get; }

    /// <summary>Total count of null entries.</summary>
    int NullCount { get; }

    /// <summary>Bitboard null mask where true indicates valid (non-null) or false indicates null.</summary>
    BitmapMask? NullMask { get; }

    /// <summary>Checks if element at given index is null.</summary>
    bool IsNull(int index);

    /// <summary>Gets boxed value for generic access.</summary>
    object? GetBoxed(int index);

    /// <summary>Slices column to sub-range with zero-copy or minimal copy.</summary>
    IColumn Slice(int offset, int length);

    /// <summary>Gathers rows by an array of indices.</summary>
    IColumn Take(ReadOnlySpan<int> indices);

    /// <summary>Filters rows where mask bit is true.</summary>
    IColumn Filter(BitmapMask mask);

    /// <summary>Creates a deep clone of this column.</summary>
    IColumn Clone();

    /// <summary>Casts column to another compatible DataType.</summary>
    IColumn Cast(DataType targetType);
}
