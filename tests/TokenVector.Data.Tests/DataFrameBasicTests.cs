using System;
using System.Linq;
using TokenVector.Data.Common;
using TokenVector.Data.Core;
using Xunit;

namespace TokenVector.Data.Tests;

public class DataFrameBasicTests
{
    [Fact]
    public void TestDataFrame_CreationAndShapes()
    {
        var df = new DataFrame(
            Series.FromValues("id", new[] { 1, 2, 3, 4, 5 }),
            Series.FromStrings("name", new[] { "Alice", "Bob", "Charlie", "David", "Eve" }),
            Series.FromValues("score", new[] { 85.5, 92.0, 78.5, 95.0, 88.0 })
        );

        Assert.Equal(5, df.RowCount);
        Assert.Equal(3, df.ColumnCount);
        Assert.Equal((5, 3), df.Shape);
        Assert.Equal(new[] { "id", "name", "score" }, df.ColumnNames);

        Assert.Equal(1, df["id"][0]);
        Assert.Equal("Bob", df["name"][1]);
        Assert.Equal(78.5, df["score"][2]);
    }

    [Fact]
    public void TestDataFrame_IndexingAndSlicing()
    {
        var df = new DataFrame(
            Series.FromValues("x", new[] { 10.0, 20.0, 30.0, 40.0, 50.0 }),
            Series.FromValues("y", new[] { 1.0, 2.0, 3.0, 4.0, 5.0 })
        );

        var slice = df[1..4];
        Assert.Equal(3, slice.RowCount);
        Assert.Equal(20.0, slice["x"][0]);
        Assert.Equal(40.0, slice["x"][2]);

        var head = df.Head(2);
        Assert.Equal(2, head.RowCount);
        Assert.Equal(10.0, head["x"][0]);

        var tail = df.Tail(2);
        Assert.Equal(2, tail.RowCount);
        Assert.Equal(40.0, tail["x"][0]);
        Assert.Equal(50.0, tail["x"][1]);
    }

    [Fact]
    public void TestDataFrame_MutationAndRenaming()
    {
        var df = new DataFrame(
            Series.FromValues("a", new[] { 1, 2, 3 }),
            Series.FromValues("b", new[] { 10, 20, 30 })
        );

        var added = df.WithColumn("c", Series.FromValues("c", new[] { 100, 200, 300 }));
        Assert.Equal(3, added.ColumnCount);
        Assert.Equal(100, added["c"][0]);

        var renamed = added.WithColumnRenamed("a", "alpha");
        Assert.True(renamed.Schema.Contains("alpha"));
        Assert.False(renamed.Schema.Contains("a"));

        var dropped = renamed.Drop("b");
        Assert.Equal(2, dropped.ColumnCount);
        Assert.False(dropped.Schema.Contains("b"));
    }

    [Fact]
    public void TestDataFrame_Sorting()
    {
        var df = new DataFrame(
            Series.FromStrings("item", new[] { "Banana", "Apple", "Cherry" }),
            Series.FromValues("price", new[] { 2.5, 1.2, 4.0 })
        );

        var sorted = df.SortBy("price", ascending: true);
        Assert.Equal("Apple", sorted["item"][0]);
        Assert.Equal("Banana", sorted["item"][1]);
        Assert.Equal("Cherry", sorted["item"][2]);

        var sortedDesc = df.SortBy("price", ascending: false);
        Assert.Equal("Cherry", sortedDesc["item"][0]);
        Assert.Equal("Apple", sortedDesc["item"][2]);
    }

    [Fact]
    public void TestDataFrame_Describe()
    {
        var df = new DataFrame(
            Series.FromValues("val", new[] { 10.0, 20.0, 30.0, 40.0, 50.0 })
        );

        var desc = df.Describe();
        Assert.Equal(8, desc.RowCount);
        Assert.Equal(2, desc.ColumnCount);
        Assert.Equal("statistic", desc.ColumnNames[0]);
        Assert.Equal("val", desc.ColumnNames[1]);
    }

    [Fact]
    public void TestDataFrame_MaskIndexerAndSelection()
    {
        var df = new DataFrame(
            Series.FromValues("x", new[] { 10, 20, 30, 40, 50 }),
            Series.FromStrings("label", new[] { "A", "B", "C", "D", "E" })
        );

        var mask = new BitmapMask(5);
        mask.Set(0, true);
        mask.Set(3, true);

        var filtered = df[mask];
        Assert.Equal(2, filtered.RowCount);
        Assert.Equal(10, filtered["x"][0]);
        Assert.Equal(40, filtered["x"][1]);
        Assert.Equal("A", filtered["label"][0]);
        Assert.Equal("D", filtered["label"][1]);
    }

    [Fact]
    public void TestDataFrame_DropNullAndFillNull()
    {
        var mask = new BitmapMask(3, initialValue: true);
        mask.Set(1, false); // null at 1

        var df = new DataFrame(
            Series.FromValues("id", new[] { 1, 2, 3 }),
            Series.FromValues("val", new[] { 10.0, 0.0, 30.0 }, mask)
        );

        var dropped = df.DropNull();
        Assert.Equal(2, dropped.RowCount);
        Assert.Equal(1, dropped["id"][0]);
        Assert.Equal(3, dropped["id"][1]);

        var filled = df.FillNull(99.0);
        Assert.Equal(3, filled.RowCount);
        Assert.Equal(99.0, filled["val"][1]);
    }
}
