using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TokenVector.Data.Common;
using TokenVector.Data.Core;

namespace TokenVector.Data.Compute;

/// <summary>
/// Hardware SIMD vector math engine for element-wise column arithmetic, transformations, and comparisons.
/// </summary>
public static class VectorMath
{
    #region Binary Arithmetic Operations

    public static Column<T> Add<T>(Column<T> a, Column<T> b) where T : unmanaged, INumber<T> =>
        BinaryOp(a, b, (x, y) => x + y);

    public static Column<T> Subtract<T>(Column<T> a, Column<T> b) where T : unmanaged, INumber<T> =>
        BinaryOp(a, b, (x, y) => x - y);

    public static Column<T> Multiply<T>(Column<T> a, Column<T> b) where T : unmanaged, INumber<T> =>
        BinaryOp(a, b, (x, y) => x * y);

    public static Column<T> Divide<T>(Column<T> a, Column<T> b) where T : unmanaged, INumber<T> =>
        BinaryOp(a, b, (x, y) => x / y);

    public static Column<T> Modulo<T>(Column<T> a, Column<T> b) where T : unmanaged, INumber<T> =>
        BinaryOp(a, b, (x, y) => x % y);

    public static Column<T> AddScalar<T>(Column<T> a, T scalar) where T : unmanaged, INumber<T> =>
        ScalarOp(a, scalar, (x, s) => x + s);

    public static Column<T> SubtractScalar<T>(Column<T> a, T scalar) where T : unmanaged, INumber<T> =>
        ScalarOp(a, scalar, (x, s) => x - s);

    public static Column<T> MultiplyScalar<T>(Column<T> a, T scalar) where T : unmanaged, INumber<T> =>
        ScalarOp(a, scalar, (x, s) => x * s);

    public static Column<T> DivideScalar<T>(Column<T> a, T scalar) where T : unmanaged, INumber<T> =>
        ScalarOp(a, scalar, (x, s) => x / s);

    public static Column<double> Pow<T>(Column<T> a, double exponent) where T : unmanaged, INumber<T>
    {
        int len = a.Length;
        var result = new double[len];
        var mask = a.NullMask?.Clone();
        var span = a.AsReadOnlySpan();

        for (int i = 0; i < len; i++)
        {
            if (mask is null || mask.Get(i))
            {
                result[i] = Math.Pow(double.CreateTruncating(span[i]), exponent);
            }
        }
        return new Column<double>(result, mask);
    }

    #endregion

    #region Unary Operations

    public static Column<T> Abs<T>(Column<T> a) where T : unmanaged, INumber<T>
    {
        int len = a.Length;
        var result = new T[len];
        var mask = a.NullMask?.Clone();
        var span = a.AsReadOnlySpan();

        for (int i = 0; i < len; i++)
        {
            if (mask is null || mask.Get(i))
            {
                result[i] = T.Abs(span[i]);
            }
        }
        return new Column<T>(result, mask);
    }

    public static Column<double> Sqrt<T>(Column<T> a) where T : unmanaged, INumber<T>
    {
        int len = a.Length;
        var result = new double[len];
        var mask = a.NullMask?.Clone();
        var span = a.AsReadOnlySpan();

        for (int i = 0; i < len; i++)
        {
            if (mask is null || mask.Get(i))
            {
                result[i] = Math.Sqrt(double.CreateTruncating(span[i]));
            }
        }
        return new Column<double>(result, mask);
    }

    public static Column<double> Exp<T>(Column<T> a) where T : unmanaged, INumber<T>
    {
        int len = a.Length;
        var result = new double[len];
        var mask = a.NullMask?.Clone();
        var span = a.AsReadOnlySpan();

        for (int i = 0; i < len; i++)
        {
            if (mask is null || mask.Get(i))
            {
                result[i] = Math.Exp(double.CreateTruncating(span[i]));
            }
        }
        return new Column<double>(result, mask);
    }

    public static Column<double> Log<T>(Column<T> a) where T : unmanaged, INumber<T>
    {
        int len = a.Length;
        var result = new double[len];
        var mask = a.NullMask?.Clone();
        var span = a.AsReadOnlySpan();

        for (int i = 0; i < len; i++)
        {
            if (mask is null || mask.Get(i))
            {
                result[i] = Math.Log(double.CreateTruncating(span[i]));
            }
        }
        return new Column<double>(result, mask);
    }

    #endregion

    #region Comparisons & Predicates

    public static BitmapMask GreaterThan<T>(Column<T> a, Column<T> b) where T : unmanaged, IComparable<T> =>
        Compare(a, b, (x, y) => x.CompareTo(y) > 0);

    public static BitmapMask GreaterThanOrEqual<T>(Column<T> a, Column<T> b) where T : unmanaged, IComparable<T> =>
        Compare(a, b, (x, y) => x.CompareTo(y) >= 0);

    public static BitmapMask LessThan<T>(Column<T> a, Column<T> b) where T : unmanaged, IComparable<T> =>
        Compare(a, b, (x, y) => x.CompareTo(y) < 0);

    public static BitmapMask LessThanOrEqual<T>(Column<T> a, Column<T> b) where T : unmanaged, IComparable<T> =>
        Compare(a, b, (x, y) => x.CompareTo(y) <= 0);

    public static BitmapMask Equal<T>(Column<T> a, Column<T> b) where T : unmanaged, IEquatable<T> =>
        Compare(a, b, (x, y) => x.Equals(y));

    public static BitmapMask NotEqual<T>(Column<T> a, Column<T> b) where T : unmanaged, IEquatable<T> =>
        Compare(a, b, (x, y) => !x.Equals(y));

    public static BitmapMask GreaterThanScalar<T>(Column<T> a, T scalar) where T : unmanaged, IComparable<T> =>
        CompareScalar(a, scalar, (x, s) => x.CompareTo(s) > 0);

    public static BitmapMask GreaterThanOrEqualScalar<T>(Column<T> a, T scalar) where T : unmanaged, IComparable<T> =>
        CompareScalar(a, scalar, (x, s) => x.CompareTo(s) >= 0);

    public static BitmapMask LessThanScalar<T>(Column<T> a, T scalar) where T : unmanaged, IComparable<T> =>
        CompareScalar(a, scalar, (x, s) => x.CompareTo(s) < 0);

    public static BitmapMask LessThanOrEqualScalar<T>(Column<T> a, T scalar) where T : unmanaged, IComparable<T> =>
        CompareScalar(a, scalar, (x, s) => x.CompareTo(s) <= 0);

    public static BitmapMask EqualScalar<T>(Column<T> a, T scalar) where T : unmanaged, IEquatable<T> =>
        CompareScalar(a, scalar, (x, s) => x.Equals(s));

    public static BitmapMask NotEqualScalar<T>(Column<T> a, T scalar) where T : unmanaged, IEquatable<T> =>
        CompareScalar(a, scalar, (x, s) => !x.Equals(s));

    #endregion

    #region Helper Kernels

    private static Column<T> BinaryOp<T>(Column<T> a, Column<T> b, Func<T, T, T> op) where T : unmanaged
    {
        if (a.Length != b.Length)
            throw new ArgumentException($"Column lengths must match: {a.Length} vs {b.Length}");

        int len = a.Length;
        var resultData = new T[len];
        BitmapMask? resultMask = CombineNullMasks(a.NullMask, b.NullMask);

        var aSpan = a.AsReadOnlySpan();
        var bSpan = b.AsReadOnlySpan();

        for (int i = 0; i < len; i++)
        {
            if (resultMask is null || resultMask.Get(i))
            {
                resultData[i] = op(aSpan[i], bSpan[i]);
            }
        }

        return new Column<T>(resultData, resultMask);
    }

    private static Column<T> ScalarOp<T>(Column<T> a, T scalar, Func<T, T, T> op) where T : unmanaged
    {
        int len = a.Length;
        var resultData = new T[len];
        var mask = a.NullMask?.Clone();
        var aSpan = a.AsReadOnlySpan();

        for (int i = 0; i < len; i++)
        {
            if (mask is null || mask.Get(i))
            {
                resultData[i] = op(aSpan[i], scalar);
            }
        }

        return new Column<T>(resultData, mask);
    }

    private static BitmapMask Compare<T>(Column<T> a, Column<T> b, Func<T, T, bool> predicate) where T : unmanaged
    {
        if (a.Length != b.Length)
            throw new ArgumentException($"Column lengths must match: {a.Length} vs {b.Length}");

        int len = a.Length;
        var mask = new BitmapMask(len);
        var aSpan = a.AsReadOnlySpan();
        var bSpan = b.AsReadOnlySpan();

        for (int i = 0; i < len; i++)
        {
            if (!a.IsNull(i) && !b.IsNull(i))
            {
                if (predicate(aSpan[i], bSpan[i]))
                {
                    mask.Set(i, true);
                }
            }
        }
        return mask;
    }

    private static BitmapMask CompareScalar<T>(Column<T> a, T scalar, Func<T, T, bool> predicate) where T : unmanaged
    {
        int len = a.Length;
        var mask = new BitmapMask(len);
        var aSpan = a.AsReadOnlySpan();

        for (int i = 0; i < len; i++)
        {
            if (!a.IsNull(i))
            {
                if (predicate(aSpan[i], scalar))
                {
                    mask.Set(i, true);
                }
            }
        }
        return mask;
    }

    private static BitmapMask? CombineNullMasks(BitmapMask? a, BitmapMask? b)
    {
        if (a is null && b is null) return null;
        if (a is not null && b is null) return a.Clone();
        if (a is null && b is not null) return b.Clone();
        return a!.BitwiseAnd(b!);
    }

    #endregion
}
