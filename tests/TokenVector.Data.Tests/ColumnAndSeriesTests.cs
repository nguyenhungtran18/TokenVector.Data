using System;
using System.Linq;
using TokenVector.Data.Common;
using TokenVector.Data.Core;
using Xunit;

namespace TokenVector.Data.Tests;

public class ColumnAndSeriesTests
{
    [Fact]
    public void TestBitmapMask_BasicOperations()
    {
        var mask = new BitmapMask(100);
        Assert.Equal(100, mask.Length);
        Assert.Equal(0, mask.PopCount());
        Assert.False(mask.Any());

        mask.Set(0, true);
        mask.Set(63, true);
        mask.Set(64, true);
        mask.Set(99, true);

        Assert.Equal(4, mask.PopCount());
        Assert.True(mask.Get(0));
        Assert.True(mask.Get(63));
        Assert.True(mask.Get(64));
        Assert.True(mask.Get(99));
        Assert.False(mask.Get(1));

        int[] indices = mask.ToIndices();
        Assert.Equal(new[] { 0, 63, 64, 99 }, indices);

        var inverted = mask.BitwiseNot();
        Assert.Equal(96, inverted.PopCount());
        Assert.False(inverted.Get(0));
        Assert.True(inverted.Get(1));
    }

    [Fact]
    public void TestBitmapMask_BitwiseLogic()
    {
        var m1 = new BitmapMask(10);
        var m2 = new BitmapMask(10);

        m1.Set(1, true);
        m1.Set(2, true);

        m2.Set(2, true);
        m2.Set(3, true);

        var andMask = m1 & m2;
        Assert.Equal(1, andMask.PopCount());
        Assert.True(andMask[2]);

        var orMask = m1 | m2;
        Assert.Equal(3, orMask.PopCount());
        Assert.True(orMask[1]);
        Assert.True(orMask[2]);
        Assert.True(orMask[3]);

        var xorMask = m1 ^ m2;
        Assert.Equal(2, xorMask.PopCount());
        Assert.True(xorMask[1]);
        Assert.True(xorMask[3]);
        Assert.False(xorMask[2]);
    }

    [Fact]
    public void TestColumn_TypedAllocationAndNulls()
    {
        var col = new Column<double>(5, nullable: true);
        Assert.Equal(5, col.Length);
        Assert.Equal(0, col.NullCount);

        col.SetValue(0, 10.5);
        col.SetValue(1, 20.5);
        col.SetNull(2);
        col.SetValue(3, 40.5);
        col.SetValue(4, 50.5);

        Assert.Equal(1, col.NullCount);
        Assert.True(col.IsNull(2));
        Assert.False(col.IsNull(0));
        Assert.Equal(10.5, col[0]);
        Assert.Null(col.GetValue(2));

        var slice = (Column<double>)col.Slice(1, 3);
        Assert.Equal(3, slice.Length);
        Assert.Equal(20.5, slice[0]);
        Assert.True(slice.IsNull(1));
        Assert.Equal(40.5, slice[2]);
    }

    [Fact]
    public void TestColumn_TakeAndFilter()
    {
        var col = new Column<int>(new[] { 10, 20, 30, 40, 50 });
        var taken = (Column<int>)col.Take(new[] { 4, 2, 0 });

        Assert.Equal(3, taken.Length);
        Assert.Equal(50, taken[0]);
        Assert.Equal(30, taken[1]);
        Assert.Equal(10, taken[2]);

        var mask = new BitmapMask(5);
        mask.Set(1, true);
        mask.Set(3, true);

        var filtered = (Column<int>)col.Filter(mask);
        Assert.Equal(2, filtered.Length);
        Assert.Equal(20, filtered[0]);
        Assert.Equal(40, filtered[1]);
    }

    [Fact]
    public void TestStringColumn_ArrowLayoutAndNulls()
    {
        var strings = new[] { "apple", null, "banana", "cherry", "" };
        var col = StringColumn.FromStrings(strings);

        Assert.Equal(5, col.Length);
        Assert.Equal(1, col.NullCount);
        Assert.Equal("apple", col.GetString(0));
        Assert.Null(col.GetString(1));
        Assert.Equal("banana", col.GetString(2));
        Assert.Equal("cherry", col.GetString(3));
        Assert.Equal("", col.GetString(4));

        var slice = (StringColumn)col.Slice(2, 2);
        Assert.Equal(2, slice.Length);
        Assert.Equal("banana", slice[0]);
        Assert.Equal("cherry", slice[1]);

        var taken = (StringColumn)col.Take(new[] { 3, 0 });
        Assert.Equal(2, taken.Length);
        Assert.Equal("cherry", taken[0]);
        Assert.Equal("apple", taken[1]);
    }

    [Fact]
    public void TestSeries_ArithmeticAndAggregations()
    {
        var s1 = Series.FromValues("a", new[] { 1.0, 2.0, 3.0, 4.0 });
        var s2 = Series.FromValues("b", new[] { 10.0, 20.0, 30.0, 40.0 });

        var sumSeries = s1 + s2;
        Assert.Equal(11.0, sumSeries[0]);
        Assert.Equal(44.0, sumSeries[3]);

        var mulScalar = s1 * 2.5;
        Assert.Equal(2.5, mulScalar[0]);
        Assert.Equal(10.0, mulScalar[3]);

        Assert.Equal(10.0, s1.Sum());
        Assert.Equal(2.5, s1.Mean());
        Assert.Equal(1.0, s1.Min());
        Assert.Equal(4.0, s1.Max());
        Assert.Equal(2.5, s1.Median());
    }

    [Fact]
    public void TestSeries_FillNullAndDropNull()
    {
        var mask = new BitmapMask(4, initialValue: true);
        mask.Set(1, false); // null at 1
        var s = Series.FromValues("test", new[] { 10.0, 0.0, 30.0, 40.0 }, mask);

        Assert.Equal(1, s.NullCount);
        Assert.Null(s[1]);

        var dropped = s.DropNull();
        Assert.Equal(3, dropped.Length);
        Assert.Equal(10.0, dropped[0]);
        Assert.Equal(30.0, dropped[1]);
        Assert.Equal(40.0, dropped[2]);

        var filled = s.FillNull(99.0);
        Assert.Equal(4, filled.Length);
        Assert.Equal(99.0, filled[1]);
    }

    [Fact]
    public void TestChunkedArray_FlattenAndIndexing()
    {
        var chunk1 = new Column<int>(new[] { 1, 2, 3 });
        var chunk2 = new Column<int>(new[] { 4, 5 });
        var chunk3 = new Column<int>(new[] { 6, 7, 8, 9 });

        var chunked = new ChunkedArray(chunk1, chunk2, chunk3);
        Assert.Equal(9, chunked.Length);
        Assert.Equal(3, chunked.ChunkCount);
        Assert.Equal(1, chunked.GetBoxed(0));
        Assert.Equal(5, chunked.GetBoxed(4));
        Assert.Equal(9, chunked.GetBoxed(8));

        var flattened = (Column<int>)chunked.Flatten();
        Assert.Equal(9, flattened.Length);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, flattened.RawData);
    }

    [Fact]
    public void TestColumn_CastOperations()
    {
        var intCol = new Column<int>(new[] { 10, 20, 30 });
        var dblCol = (Column<double>)intCol.Cast(DataType.Float64);
        Assert.Equal(DataType.Float64, dblCol.DataType);
        Assert.Equal(10.0, dblCol[0]);
        Assert.Equal(30.0, dblCol[2]);

        var strCol = (StringColumn)intCol.Cast(DataType.String);
        Assert.Equal("10", strCol[0]);
        Assert.Equal("30", strCol[2]);
    }

    [Fact]
    public void TestStringColumn_CastToNumeric()
    {
        var strCol = StringColumn.FromStrings("100", "200", "300");
        var intCol = (Column<int>)strCol.Cast(DataType.Int32);
        Assert.Equal(100, intCol[0]);
        Assert.Equal(300, intCol[2]);
    }

    [Fact]
    public void TestSeries_MathUnaryOperations()
    {
        var s = Series.FromValues("x", new[] { 1.0, 4.0, 9.0, 16.0 });
        var sqrt = s.Sqrt();
        Assert.Equal(1.0, sqrt[0]);
        Assert.Equal(2.0, sqrt[1]);
        Assert.Equal(3.0, sqrt[2]);
        Assert.Equal(4.0, sqrt[3]);

        var abs = Series.FromValues("neg", new[] { -10.0, 20.0, -30.0 }).Abs();
        Assert.Equal(10.0, abs[0]);
        Assert.Equal(20.0, abs[1]);
        Assert.Equal(30.0, abs[2]);

        var pow = s.Pow(0.5);
        Assert.Equal(2.0, pow[1]);
    }

    [Fact]
    public void TestBitmapMask_SliceAndFromIndices()
    {
        var mask = BitmapMask.FromIndices(10, new[] { 2, 5, 8 });
        Assert.Equal(3, mask.PopCount());
        Assert.True(mask[2]);
        Assert.True(mask[5]);
        Assert.True(mask[8]);

        var slice = mask.Slice(4, 5); // indices 4..8 -> index 5 is local 1, index 8 is local 4
        Assert.Equal(5, slice.Length);
        Assert.Equal(2, slice.PopCount());
        Assert.True(slice[1]);
        Assert.True(slice[4]);
    }
}
