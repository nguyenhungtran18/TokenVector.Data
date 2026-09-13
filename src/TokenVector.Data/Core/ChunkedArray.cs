using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TokenVector.Data.Common;

namespace TokenVector.Data.Core;

/// <summary>
/// Multi-chunk columnar container for streaming data ingestion, out-of-core batches, and zero-allocation chunk concatenation.
/// </summary>
public sealed class ChunkedArray : IReadOnlyList<IColumn>, IDisposable
{
    private readonly IColumn[] _chunks;
    private readonly int[] _chunkOffsets;
    private readonly int _totalLength;
    private bool _isDisposed;

    public DataType DataType { get; }
    public int Length => _totalLength;
    public int ChunkCount => _chunks.Length;
    public int Count => _chunks.Length;

    public IColumn this[int chunkIndex] => _chunks[chunkIndex];

    public int NullCount => _chunks.Sum(c => c.NullCount);

    public ChunkedArray(IEnumerable<IColumn> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        _chunks = chunks.ToArray();
        if (_chunks.Length == 0)
        {
            throw new ArgumentException("ChunkedArray requires at least one chunk.");
        }

        DataType = _chunks[0].DataType;
        for (int i = 1; i < _chunks.Length; i++)
        {
            if (_chunks[i].DataType != DataType)
            {
                throw new ArgumentException($"Mismatched chunk data types: {DataType} vs {_chunks[i].DataType} at index {i}.");
            }
        }

        _chunkOffsets = new int[_chunks.Length + 1];
        int sum = 0;
        _chunkOffsets[0] = 0;
        for (int i = 0; i < _chunks.Length; i++)
        {
            sum += _chunks[i].Length;
            _chunkOffsets[i + 1] = sum;
        }
        _totalLength = sum;
    }

    public ChunkedArray(params IColumn[] chunks) : this((IEnumerable<IColumn>)chunks) { }

    /// <summary>
    /// Gets element at the given global linear index.
    /// </summary>
    public object? GetBoxed(int globalIndex)
    {
        if ((uint)globalIndex >= (uint)_totalLength)
        {
            throw new ArgumentOutOfRangeException(nameof(globalIndex), globalIndex, $"Index must be within [0, {_totalLength - 1}].");
        }

        int chunkIdx = FindChunkIndex(globalIndex);
        int localIdx = globalIndex - _chunkOffsets[chunkIdx];
        return _chunks[chunkIdx].GetBoxed(localIdx);
    }

    /// <summary>
    /// Flattens all chunks into a single contiguous <see cref="IColumn"/>.
    /// </summary>
    public IColumn Flatten()
    {
        if (_chunks.Length == 1)
        {
            return _chunks[0].Clone();
        }

        if (DataType == DataType.String)
        {
            var allStrings = new string?[_totalLength];
            int outIdx = 0;
            foreach (var chunk in _chunks)
            {
                var strCol = (StringColumn)chunk;
                for (int i = 0; i < strCol.Length; i++)
                {
                    allStrings[outIdx++] = strCol.GetString(i);
                }
            }
            return StringColumn.FromStrings(allStrings);
        }

        return DataType switch
        {
            DataType.Int8 => FlattenTyped<sbyte>(),
            DataType.Int16 => FlattenTyped<short>(),
            DataType.Int32 => FlattenTyped<int>(),
            DataType.Int64 => FlattenTyped<long>(),
            DataType.UInt8 => FlattenTyped<byte>(),
            DataType.UInt16 => FlattenTyped<ushort>(),
            DataType.UInt32 => FlattenTyped<uint>(),
            DataType.UInt64 => FlattenTyped<ulong>(),
            DataType.Float32 => FlattenTyped<float>(),
            DataType.Float64 => FlattenTyped<double>(),
            DataType.Boolean => FlattenTyped<bool>(),
            DataType.DateTime64 => FlattenTyped<long>(),
            _ => throw new NotSupportedException($"Flatten not supported for {DataType}")
        };
    }

    private Column<T> FlattenTyped<T>() where T : unmanaged
    {
        var data = new T[_totalLength];
        bool hasMask = _chunks.Any(c => c.NullMask is not null);
        BitmapMask? mask = hasMask ? new BitmapMask(_totalLength, initialValue: true) : null;

        int currOffset = 0;
        foreach (var chunk in _chunks)
        {
            var col = (Column<T>)chunk;
            col.AsReadOnlySpan().CopyTo(data.AsSpan(currOffset));
            if (mask is not null)
            {
                if (col.NullMask is not null)
                {
                    for (int i = 0; i < col.Length; i++)
                    {
                        mask.Set(currOffset + i, col.NullMask.Get(i));
                    }
                }
            }
            currOffset += col.Length;
        }

        return new Column<T>(data, mask);
    }

    private int FindChunkIndex(int globalIndex)
    {
        int low = 0;
        int high = _chunks.Length - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            if (_chunkOffsets[mid + 1] <= globalIndex)
            {
                low = mid + 1;
            }
            else if (_chunkOffsets[mid] > globalIndex)
            {
                high = mid - 1;
            }
            else
            {
                return mid;
            }
        }
        return low;
    }

    public IEnumerator<IColumn> GetEnumerator() => ((IEnumerable<IColumn>)_chunks).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _chunks.GetEnumerator();

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            foreach (var chunk in _chunks)
            {
                chunk.Dispose();
            }
        }
    }
}
