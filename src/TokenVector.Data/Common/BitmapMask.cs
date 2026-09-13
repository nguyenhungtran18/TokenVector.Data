using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace TokenVector.Data.Common;

/// <summary>
/// Ultra-fast 64-bit word aligned bitboard mask for zero-allocation boolean tracking and null masks.
/// Accelerates bitwise SIMD logic and hardware POPCNT.
/// </summary>
public sealed class BitmapMask : IEquatable<BitmapMask>
{
    private readonly ulong[] _words;
    private readonly int _length;

    /// <summary>
    /// Total number of tracked bits.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Total number of 64-bit ulong words in the backing buffer.
    /// </summary>
    public int WordCount => _words.Length;

    /// <summary>
    /// Direct read-only span access to underlying 64-bit bitboard words.
    /// </summary>
    public ReadOnlySpan<ulong> Words => _words;

    /// <summary>
    /// Direct mutable span access to underlying 64-bit bitboard words.
    /// </summary>
    public Span<ulong> AsSpan() => _words;

    /// <summary>
    /// Initializes a new bitmap mask of given bit length, defaulted to all false or all true.
    /// </summary>
    public BitmapMask(int length, bool initialValue = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _length = length;
        int wordCount = (length + 63) >> 6;
        _words = new ulong[wordCount];
        if (initialValue && length > 0)
        {
            SetAll(true);
        }
    }

    /// <summary>
    /// Initializes a bitmap mask by wrapping or cloning existing word buffer.
    /// </summary>
    public BitmapMask(int length, ulong[] words)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentNullException.ThrowIfNull(words);
        int requiredWords = (length + 63) >> 6;
        if (words.Length < requiredWords)
        {
            throw new ArgumentException($"Word buffer length {words.Length} is smaller than required {requiredWords} for {length} bits.");
        }
        _length = length;
        _words = (ulong[])words.Clone();
        ClearTrailingBits();
    }

    /// <summary>
    /// Gets the boolean value at the specified bit index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(int index)
    {
        if ((uint)index >= (uint)_length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {_length - 1}.");
        }
        return (_words[index >> 6] & (1UL << (index & 63))) != 0UL;
    }

    /// <summary>
    /// Sets the boolean value at the specified bit index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index, bool value)
    {
        if ((uint)index >= (uint)_length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {_length - 1}.");
        }
        int wordIdx = index >> 6;
        ulong mask = 1UL << (index & 63);
        if (value)
        {
            _words[wordIdx] |= mask;
        }
        else
        {
            _words[wordIdx] &= ~mask;
        }
    }

    /// <summary>
    /// Fast indexer for bit lookup.
    /// </summary>
    public bool this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Get(index);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Set(index, value);
    }

    /// <summary>
    /// Sets all bits in the mask to either true or false.
    /// </summary>
    public void SetAll(bool value)
    {
        ulong fill = value ? ulong.MaxValue : 0UL;
        Array.Fill(_words, fill);
        if (value)
        {
            ClearTrailingBits();
        }
    }

    /// <summary>
    /// Counts total number of set bits (true values) using hardware POPCNT.
    /// </summary>
    public int PopCount()
    {
        int count = 0;
        ref ulong ptr = ref MemoryMarshal.GetArrayDataReference(_words);
        for (int i = 0; i < _words.Length; i++)
        {
            count += BitOperations.PopCount(Unsafe.Add(ref ptr, i));
        }
        return count;
    }

    /// <summary>
    /// Returns true if all bits are set.
    /// </summary>
    public bool All() => PopCount() == _length;

    /// <summary>
    /// Returns true if any bit is set.
    /// </summary>
    public bool Any()
    {
        ref ulong ptr = ref MemoryMarshal.GetArrayDataReference(_words);
        for (int i = 0; i < _words.Length; i++)
        {
            if (Unsafe.Add(ref ptr, i) != 0UL)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Computes bitwise AND with another mask.
    /// </summary>
    public BitmapMask BitwiseAnd(BitmapMask other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (_length != other._length)
        {
            throw new ArgumentException($"Bitmap lengths must match: {_length} vs {other._length}");
        }

        var result = new BitmapMask(_length);
        ref ulong dest = ref MemoryMarshal.GetArrayDataReference(result._words);
        ref ulong srcA = ref MemoryMarshal.GetArrayDataReference(_words);
        ref ulong srcB = ref MemoryMarshal.GetArrayDataReference(other._words);

        int len = _words.Length;
        for (int i = 0; i < len; i++)
        {
            Unsafe.Add(ref dest, i) = Unsafe.Add(ref srcA, i) & Unsafe.Add(ref srcB, i);
        }
        return result;
    }

    /// <summary>
    /// Computes bitwise OR with another mask.
    /// </summary>
    public BitmapMask BitwiseOr(BitmapMask other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (_length != other._length)
        {
            throw new ArgumentException($"Bitmap lengths must match: {_length} vs {other._length}");
        }

        var result = new BitmapMask(_length);
        ref ulong dest = ref MemoryMarshal.GetArrayDataReference(result._words);
        ref ulong srcA = ref MemoryMarshal.GetArrayDataReference(_words);
        ref ulong srcB = ref MemoryMarshal.GetArrayDataReference(other._words);

        int len = _words.Length;
        for (int i = 0; i < len; i++)
        {
            Unsafe.Add(ref dest, i) = Unsafe.Add(ref srcA, i) | Unsafe.Add(ref srcB, i);
        }
        result.ClearTrailingBits();
        return result;
    }

    /// <summary>
    /// Computes bitwise NOT of this mask.
    /// </summary>
    public BitmapMask BitwiseNot()
    {
        var result = new BitmapMask(_length);
        ref ulong dest = ref MemoryMarshal.GetArrayDataReference(result._words);
        ref ulong srcA = ref MemoryMarshal.GetArrayDataReference(_words);

        int len = _words.Length;
        for (int i = 0; i < len; i++)
        {
            Unsafe.Add(ref dest, i) = ~Unsafe.Add(ref srcA, i);
        }
        result.ClearTrailingBits();
        return result;
    }

    /// <summary>
    /// Computes bitwise XOR with another mask.
    /// </summary>
    public BitmapMask BitwiseXor(BitmapMask other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (_length != other._length)
        {
            throw new ArgumentException($"Bitmap lengths must match: {_length} vs {other._length}");
        }

        var result = new BitmapMask(_length);
        ref ulong dest = ref MemoryMarshal.GetArrayDataReference(result._words);
        ref ulong srcA = ref MemoryMarshal.GetArrayDataReference(_words);
        ref ulong srcB = ref MemoryMarshal.GetArrayDataReference(other._words);

        int len = _words.Length;
        for (int i = 0; i < len; i++)
        {
            Unsafe.Add(ref dest, i) = Unsafe.Add(ref srcA, i) ^ Unsafe.Add(ref srcB, i);
        }
        result.ClearTrailingBits();
        return result;
    }

    /// <summary>
    /// Extracts integer indices of all true bits in $O(N)$ with bit-skipping (TrailedZeros).
    /// </summary>
    public int[] ToIndices()
    {
        int count = PopCount();
        if (count == 0) return Array.Empty<int>();

        var result = new int[count];
        int outIdx = 0;

        for (int w = 0; w < _words.Length; w++)
        {
            ulong word = _words[w];
            int baseBit = w << 6;
            while (word != 0UL)
            {
                int bitOffset = BitOperations.TrailingZeroCount(word);
                int bitIdx = baseBit + bitOffset;
                if (bitIdx < _length)
                {
                    result[outIdx++] = bitIdx;
                }
                word &= word - 1UL; // Clear lowest set bit
            }
        }
        return result;
    }

    /// <summary>
    /// Creates a BitmapMask from a list of set indices.
    /// </summary>
    public static BitmapMask FromIndices(int length, ReadOnlySpan<int> indices)
    {
        var mask = new BitmapMask(length);
        for (int i = 0; i < indices.Length; i++)
        {
            int idx = indices[i];
            mask.Set(idx, true);
        }
        return mask;
    }

    /// <summary>
    /// Slices the bitmap mask to a new range.
    /// </summary>
    public BitmapMask Slice(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Invalid slice bounds.");
        }

        var result = new BitmapMask(length);
        for (int i = 0; i < length; i++)
        {
            if (Get(offset + i))
            {
                result.Set(i, true);
            }
        }
        return result;
    }

    /// <summary>
    /// Deep-clones this bitmap mask.
    /// </summary>
    public BitmapMask Clone() => new(_length, _words);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearTrailingBits()
    {
        int trailingBits = _length & 63;
        if (trailingBits != 0 && _words.Length > 0)
        {
            ulong mask = (1UL << trailingBits) - 1UL;
            _words[^1] &= mask;
        }
    }

    public static BitmapMask operator &(BitmapMask a, BitmapMask b) => a.BitwiseAnd(b);
    public static BitmapMask operator |(BitmapMask a, BitmapMask b) => a.BitwiseOr(b);
    public static BitmapMask operator ^(BitmapMask a, BitmapMask b) => a.BitwiseXor(b);
    public static BitmapMask operator ~(BitmapMask a) => a.BitwiseNot();

    public bool Equals(BitmapMask? other)
    {
        if (other is null || _length != other._length) return false;
        return _words.AsSpan().SequenceEqual(other._words);
    }

    public override bool Equals(object? obj) => obj is BitmapMask other && Equals(other);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(_length);
        foreach (var w in _words) hc.Add(w);
        return hc.ToHashCode();
    }
}
