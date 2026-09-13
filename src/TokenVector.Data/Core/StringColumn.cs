using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using TokenVector.Data.Common;

namespace TokenVector.Data.Core;

/// <summary>
/// Apache Arrow-aligned Variable-Length UTF-8 binary string column.
/// Combines a contiguous byte buffer with a monotonically increasing int32 offset array.
/// Eliminates GC heap fragmentation and provides fast vector search.
/// </summary>
public sealed class StringColumn : IColumn, IEnumerable<string?>
{
    private byte[] _buffer;
    private int[] _offsets;
    private BitmapMask? _validityMask;
    private bool _isDisposed;

    public DataType DataType => DataType.String;
    public int Length => _offsets.Length - 1;
    public BitmapMask? NullMask => _validityMask;

    public int NullCount
    {
        get
        {
            if (_validityMask is null) return 0;
            return Length - _validityMask.PopCount();
        }
    }

    /// <summary>Direct access to contiguous UTF-8 data bytes.</summary>
    public ReadOnlySpan<byte> BufferSpan => _buffer;

    /// <summary>Direct access to Arrow offset vector.</summary>
    public ReadOnlySpan<int> OffsetsSpan => _offsets;

    #region Constructors

    public StringColumn(byte[] buffer, int[] offsets, BitmapMask? validityMask = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(offsets);
        if (offsets.Length == 0)
        {
            throw new ArgumentException("Offsets array must contain at least 1 element (0).");
        }
        if (offsets[0] != 0)
        {
            throw new ArgumentException("First offset must be 0.");
        }

        _buffer = buffer;
        _offsets = offsets;
        _validityMask = validityMask;
    }

    public static StringColumn FromStrings(IReadOnlyList<string?> strings)
    {
        ArgumentNullException.ThrowIfNull(strings);
        int count = strings.Count;
        int[] offsets = new int[count + 1];
        var mask = new BitmapMask(count, initialValue: true);

        // First pass: compute exact byte size
        int totalBytes = 0;
        bool hasNull = false;
        for (int i = 0; i < count; i++)
        {
            string? s = strings[i];
            if (s is null)
            {
                mask.Set(i, false);
                hasNull = true;
            }
            else
            {
                totalBytes += Encoding.UTF8.GetByteCount(s);
            }
        }

        byte[] buffer = new byte[totalBytes];
        int currentOffset = 0;
        offsets[0] = 0;

        for (int i = 0; i < count; i++)
        {
            string? s = strings[i];
            if (s is not null)
            {
                int written = Encoding.UTF8.GetBytes(s.AsSpan(), buffer.AsSpan(currentOffset));
                currentOffset += written;
            }
            offsets[i + 1] = currentOffset;
        }

        return new StringColumn(buffer, offsets, hasNull ? mask : null);
    }

    public static StringColumn FromStrings(params string?[] strings) =>
        FromStrings((IReadOnlyList<string?>)strings);

    #endregion

    #region Accessors

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNull(int index)
    {
        if (_validityMask is null) return false;
        return !_validityMask.Get(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetBytes(int index)
    {
        if ((uint)index >= (uint)Length)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be within [0, {Length - 1}].");

        if (IsNull(index)) return ReadOnlySpan<byte>.Empty;

        int start = _offsets[index];
        int end = _offsets[index + 1];
        return _buffer.AsSpan(start, end - start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetString(int index)
    {
        if (IsNull(index)) return null;

        int start = _offsets[index];
        int end = _offsets[index + 1];
        int len = end - start;
        if (len == 0) return string.Empty;

        return Encoding.UTF8.GetString(_buffer.AsSpan(start, len));
    }

    public string? this[int index] => GetString(index);

    public object? GetBoxed(int index) => GetString(index);

    #endregion

    #region Operations

    public IColumn Slice(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Invalid slice parameters.");
        }

        int byteStart = _offsets[offset];
        int byteEnd = _offsets[offset + length];
        int byteLen = byteEnd - byteStart;

        byte[] newBuffer = new byte[byteLen];
        Array.Copy(_buffer, byteStart, newBuffer, 0, byteLen);

        int[] newOffsets = new int[length + 1];
        for (int i = 0; i <= length; i++)
        {
            newOffsets[i] = _offsets[offset + i] - byteStart;
        }

        BitmapMask? newMask = _validityMask?.Slice(offset, length);
        return new StringColumn(newBuffer, newOffsets, newMask);
    }

    public IColumn Take(ReadOnlySpan<int> indices)
    {
        int count = indices.Length;
        int totalBytes = 0;
        bool hasNull = false;

        for (int i = 0; i < count; i++)
        {
            int srcIdx = indices[i];
            if (IsNull(srcIdx))
            {
                hasNull = true;
            }
            else
            {
                totalBytes += _offsets[srcIdx + 1] - _offsets[srcIdx];
            }
        }

        byte[] newBuffer = new byte[totalBytes];
        int[] newOffsets = new int[count + 1];
        BitmapMask? newMask = hasNull || _validityMask is not null ? new BitmapMask(count, initialValue: true) : null;

        int currOffset = 0;
        newOffsets[0] = 0;

        for (int i = 0; i < count; i++)
        {
            int srcIdx = indices[i];
            if (IsNull(srcIdx))
            {
                newMask?.Set(i, false);
            }
            else
            {
                int srcStart = _offsets[srcIdx];
                int srcLen = _offsets[srcIdx + 1] - srcStart;
                if (srcLen > 0)
                {
                    Array.Copy(_buffer, srcStart, newBuffer, currOffset, srcLen);
                    currOffset += srcLen;
                }
            }
            newOffsets[i + 1] = currOffset;
        }

        return new StringColumn(newBuffer, newOffsets, newMask);
    }

    public IColumn Filter(BitmapMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        var indices = mask.ToIndices();
        return Take(indices);
    }

    public IColumn Clone()
    {
        var copyBuf = (byte[])_buffer.Clone();
        var copyOffsets = (int[])_offsets.Clone();
        var copyMask = _validityMask?.Clone();
        return new StringColumn(copyBuf, copyOffsets, copyMask);
    }

    public IColumn Cast(DataType targetType)
    {
        if (targetType == DataType.String) return Clone();

        int len = Length;
        var mask = _validityMask?.Clone();

        return targetType switch
        {
            DataType.Int8 => CastNumeric<sbyte>(s => sbyte.Parse(s, CultureInfo.InvariantCulture)),
            DataType.Int16 => CastNumeric<short>(s => short.Parse(s, CultureInfo.InvariantCulture)),
            DataType.Int32 => CastNumeric<int>(s => int.Parse(s, CultureInfo.InvariantCulture)),
            DataType.Int64 => CastNumeric<long>(s => long.Parse(s, CultureInfo.InvariantCulture)),
            DataType.UInt8 => CastNumeric<byte>(s => byte.Parse(s, CultureInfo.InvariantCulture)),
            DataType.UInt16 => CastNumeric<ushort>(s => ushort.Parse(s, CultureInfo.InvariantCulture)),
            DataType.UInt32 => CastNumeric<uint>(s => uint.Parse(s, CultureInfo.InvariantCulture)),
            DataType.UInt64 => CastNumeric<ulong>(s => ulong.Parse(s, CultureInfo.InvariantCulture)),
            DataType.Float32 => CastNumeric<float>(s => float.Parse(s, CultureInfo.InvariantCulture)),
            DataType.Float64 => CastNumeric<double>(s => double.Parse(s, CultureInfo.InvariantCulture)),
            DataType.Boolean => CastNumeric<bool>(s => bool.Parse(s)),
            DataType.DateTime64 => CastNumeric<long>(s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture).ToUnixTimeMilliseconds() * 1_000_000L),
            _ => throw new NotSupportedException($"Cannot cast String column to {targetType}.")
        };
    }

    private Column<T> CastNumeric<T>(Func<string, T> parser) where T : unmanaged
    {
        int len = Length;
        var data = new T[len];
        var mask = _validityMask?.Clone();

        for (int i = 0; i < len; i++)
        {
            if (!IsNull(i))
            {
                string? str = GetString(i);
                if (!string.IsNullOrEmpty(str))
                {
                    data[i] = parser(str);
                }
            }
        }
        return new Column<T>(data, mask);
    }

    #endregion

    public IEnumerator<string?> GetEnumerator()
    {
        for (int i = 0; i < Length; i++)
        {
            yield return GetString(i);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _buffer = Array.Empty<byte>();
            _offsets = [0];
        }
    }
}
