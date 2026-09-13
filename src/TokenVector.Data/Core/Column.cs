using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TokenVector.Data.Common;

namespace TokenVector.Data.Core;

/// <summary>
/// Contiguous typed columnar memory buffer for unmanaged types with 64-bit Bitboard validity tracking.
/// </summary>
/// <typeparam name="T">Unmanaged primitive type.</typeparam>
public sealed unsafe class Column<T> : IColumn, IEnumerable<T?> where T : unmanaged
{
    private T[] _data;
    private int _length;
    private BitmapMask? _validityMask; // 1 = valid, 0 = null (Arrow convention)
    private bool _isDisposed;

    public DataType DataType { get; }
    public int Length => _length;
    public BitmapMask? NullMask => _validityMask;

    public int NullCount
    {
        get
        {
            if (_validityMask is null) return 0;
            return _length - _validityMask.PopCount();
        }
    }

    /// <summary>
    /// Direct mutable span access to contiguous unmanaged memory.
    /// </summary>
    public Span<T> AsSpan() => _data.AsSpan(0, _length);

    /// <summary>
    /// Direct read-only span access to contiguous unmanaged memory.
    /// </summary>
    public ReadOnlySpan<T> AsReadOnlySpan() => new ReadOnlySpan<T>(_data, 0, _length);

    /// <summary>
    /// Raw underlying data array.
    /// </summary>
    public T[] RawData => _data;

    #region Constructors

    public Column(int length, bool nullable = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        DataType = DataTypeHelper.FromClrType(typeof(T));
        _length = length;
        _data = GC.AllocateArray<T>(length, pinned: false);
        if (nullable)
        {
            _validityMask = new BitmapMask(length, initialValue: true);
        }
    }

    public Column(T[] data, BitmapMask? validityMask = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        DataType = DataTypeHelper.FromClrType(typeof(T));
        _data = data;
        _length = data.Length;
        _validityMask = validityMask;
    }

    public Column(ReadOnlySpan<T> data, BitmapMask? validityMask = null)
    {
        DataType = DataTypeHelper.FromClrType(typeof(T));
        _length = data.Length;
        _data = data.ToArray();
        _validityMask = validityMask?.Clone();
    }

    #endregion

    #region Indexers & Accessors

    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be within [0, {_length - 1}].");
            return ref _data[index];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNull(int index)
    {
        if (_validityMask is null) return false;
        return !_validityMask.Get(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetValue(int index)
    {
        if (IsNull(index)) return null;
        return _data[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValue(int index, T value)
    {
        this[index] = value;
        _validityMask?.Set(index, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetNull(int index)
    {
        if (_validityMask is null)
        {
            _validityMask = new BitmapMask(_length, initialValue: true);
        }
        _validityMask.Set(index, false);
    }

    public object? GetBoxed(int index) => GetValue(index);

    #endregion

    #region Operations & Transforms

    public IColumn Slice(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Invalid slice parameters.");
        }

        var sliceData = new T[length];
        Array.Copy(_data, offset, sliceData, 0, length);
        var sliceMask = _validityMask?.Slice(offset, length);
        return new Column<T>(sliceData, sliceMask);
    }

    public IColumn Take(ReadOnlySpan<int> indices)
    {
        int count = indices.Length;
        var newData = new T[count];
        BitmapMask? newMask = _validityMask is not null ? new BitmapMask(count) : null;

        ref T srcRef = ref MemoryMarshal.GetArrayDataReference(_data);
        ref T dstRef = ref MemoryMarshal.GetArrayDataReference(newData);

        for (int i = 0; i < count; i++)
        {
            int srcIdx = indices[i];
            Unsafe.Add(ref dstRef, i) = Unsafe.Add(ref srcRef, srcIdx);
            if (newMask is not null && _validityMask is not null)
            {
                newMask.Set(i, _validityMask.Get(srcIdx));
            }
        }

        return new Column<T>(newData, newMask);
    }

    public IColumn Filter(BitmapMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        var indices = mask.ToIndices();
        return Take(indices);
    }

    public IColumn Clone()
    {
        var copyData = (T[])_data.Clone();
        var copyMask = _validityMask?.Clone();
        return new Column<T>(copyData, copyMask);
    }

    public IColumn Cast(DataType targetType)
    {
        if (targetType == DataType) return Clone();

        int len = _length;
        var mask = _validityMask?.Clone();

        return targetType switch
        {
            DataType.Int8 => CastTo<sbyte>(len, mask, v => (sbyte)Convert.ChangeType(v, typeof(sbyte))!),
            DataType.Int16 => CastTo<short>(len, mask, v => (short)Convert.ChangeType(v, typeof(short))!),
            DataType.Int32 => CastTo<int>(len, mask, v => (int)Convert.ChangeType(v, typeof(int))!),
            DataType.Int64 => CastTo<long>(len, mask, v => (long)Convert.ChangeType(v, typeof(long))!),
            DataType.UInt8 => CastTo<byte>(len, mask, v => (byte)Convert.ChangeType(v, typeof(byte))!),
            DataType.UInt16 => CastTo<ushort>(len, mask, v => (ushort)Convert.ChangeType(v, typeof(ushort))!),
            DataType.UInt32 => CastTo<uint>(len, mask, v => (uint)Convert.ChangeType(v, typeof(uint))!),
            DataType.UInt64 => CastTo<ulong>(len, mask, v => (ulong)Convert.ChangeType(v, typeof(ulong))!),
            DataType.Float32 => CastTo<float>(len, mask, v => (float)Convert.ChangeType(v, typeof(float))!),
            DataType.Float64 => CastTo<double>(len, mask, v => (double)Convert.ChangeType(v, typeof(double))!),
            DataType.Boolean => CastTo<bool>(len, mask, v => (bool)Convert.ChangeType(v, typeof(bool))!),
            DataType.String => CastToStringColumn(),
            _ => throw new NotSupportedException($"Cannot cast column from {DataType} to {targetType}.")
        };
    }

    private Column<TTarget> CastTo<TTarget>(int len, BitmapMask? mask, Func<T, TTarget> converter) where TTarget : unmanaged
    {
        var resultData = new TTarget[len];
        for (int i = 0; i < len; i++)
        {
            if (_validityMask is null || _validityMask.Get(i))
            {
                resultData[i] = converter(_data[i]);
            }
        }
        return new Column<TTarget>(resultData, mask);
    }

    private StringColumn CastToStringColumn()
    {
        var strings = new string?[_length];
        for (int i = 0; i < _length; i++)
        {
            if (_validityMask is null || _validityMask.Get(i))
            {
                strings[i] = _data[i].ToString();
            }
            else
            {
                strings[i] = null;
            }
        }
        return StringColumn.FromStrings(strings);
    }

    #endregion

    public IEnumerator<T?> GetEnumerator()
    {
        for (int i = 0; i < _length; i++)
        {
            yield return GetValue(i);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _data = Array.Empty<T>();
            _length = 0;
        }
    }
}
