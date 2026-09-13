using System;
using TokenVector.Data.Common;
using TokenVector.Data.Core;
using TokenVector.Data.Relational;
using Xunit;

namespace TokenVector.Data.Tests;

public class JoinAndReshapeTests
{
    [Fact]
    public void TestJoin_InnerJoin()
    {
        var left = new DataFrame(
            Series.FromValues("user_id", new[] { 1, 2, 3, 4 }),
            Series.FromStrings("name", new[] { "Alice", "Bob", "Charlie", "David" })
        );

        var right = new DataFrame(
            Series.FromValues("user_id", new[] { 2, 3, 5 }),
            Series.FromStrings("city", new[] { "NYC", "London", "Tokyo" })
        );

        var joined = left.Join(right, "user_id", "user_id", JoinType.Inner);
        Assert.Equal(2, joined.RowCount); // user_id 2 and 3
        Assert.Equal(3, joined.ColumnCount); // user_id, name, city
    }

    [Fact]
    public void TestJoin_LeftJoin()
    {
        var left = new DataFrame(
            Series.FromValues("id", new[] { 1, 2, 3 }),
            Series.FromStrings("item", new[] { "Pen", "Book", "Desk" })
        );

        var right = new DataFrame(
            Series.FromValues("id", new[] { 2, 4 }),
            Series.FromValues("price", new[] { 15.0, 50.0 })
        );

        var joined = left.Join(right, "id", "id", JoinType.Left);
        Assert.Equal(3, joined.RowCount);
        Assert.Equal("Pen", joined["item"][0]);
        Assert.True(joined["price"].IsNull(0)); // Pen has no price match
        Assert.Equal(15.0, Convert.ToDouble(joined["price"][1])); // Book price is 15.0
    }

    [Fact]
    public void TestJoin_CrossJoin()
    {
        var left = new DataFrame(
            Series.FromStrings("color", new[] { "Red", "Blue" })
        );

        var right = new DataFrame(
            Series.FromStrings("size", new[] { "S", "M", "L" })
        );

        var cross = left.Join(right, "color", "size", JoinType.Cross);
        Assert.Equal(6, cross.RowCount); // 2 * 3 = 6
        Assert.Equal(2, cross.ColumnCount);
    }

    [Fact]
    public void TestJoin_AsOfJoin_TimeMatching()
    {
        var trades = new DataFrame(
            Series.FromValues("time", new[] { 10.0, 15.0, 25.0, 32.0 }),
            Series.FromValues("qty", new[] { 100, 200, 150, 300 })
        );

        var quotes = new DataFrame(
            Series.FromValues("time", new[] { 9.0, 14.0, 20.0, 30.0 }),
            Series.FromValues("bid", new[] { 101.5, 102.0, 102.5, 103.0 })
        );

        // Match latest quote where quote.time <= trade.time
        var asof = trades.AsOfJoin(quotes, leftOn: "time", rightOn: "time", direction: AsOfDirection.Backward);
        Assert.Equal(4, asof.RowCount);
        Assert.Equal(101.5, asof["bid"][0]); // Trade at 10.0 matches quote at 9.0 (101.5)
        Assert.Equal(102.0, asof["bid"][1]); // Trade at 15.0 matches quote at 14.0 (102.0)
        Assert.Equal(102.5, asof["bid"][2]); // Trade at 25.0 matches quote at 20.0 (102.5)
        Assert.Equal(103.0, asof["bid"][3]); // Trade at 32.0 matches quote at 30.0 (103.0)
    }

    [Fact]
    public void TestReshape_PivotAndMelt()
    {
        var longDf = new DataFrame(
            Series.FromStrings("date", new[] { "2026-01-01", "2026-01-01", "2026-01-02", "2026-01-02" }),
            Series.FromStrings("sensor", new[] { "temp", "humidity", "temp", "humidity" }),
            Series.FromValues("value", new[] { 22.5, 60.0, 24.0, 55.0 })
        );

        var pivoted = longDf.Pivot(index: "date", columns: "sensor", values: "value");
        Assert.Equal(2, pivoted.RowCount); // 2 distinct dates
        Assert.True(pivoted.Schema.Contains("temp"));
        Assert.True(pivoted.Schema.Contains("humidity"));
        Assert.Equal(22.5, Convert.ToDouble(pivoted["temp"][0]));
        Assert.Equal(60.0, Convert.ToDouble(pivoted["humidity"][0]));

        var melted = ReshapeEngine.Melt(pivoted, idVars: new[] { "date" }, valueVars: new[] { "temp", "humidity" });
        Assert.Equal(4, melted.RowCount);
        Assert.True(melted.Schema.Contains("variable"));
        Assert.True(melted.Schema.Contains("value"));
    }

    [Fact]
    public void TestReshape_ConcatVerticalAndHorizontal()
    {
        var df1 = new DataFrame(
            Series.FromValues("a", new[] { 1, 2 }),
            Series.FromValues("b", new[] { 10.0, 20.0 })
        );

        var df2 = new DataFrame(
            Series.FromValues("a", new[] { 3, 4, 5 }),
            Series.FromValues("b", new[] { 30.0, 40.0, 50.0 })
        );

        var vert = ReshapeEngine.ConcatVertical(df1, df2);
        Assert.Equal(5, vert.RowCount);
        Assert.Equal(1, vert["a"][0]);
        Assert.Equal(5, vert["a"][4]);

        var df3 = new DataFrame(
            Series.FromStrings("c", new[] { "x", "y" })
        );

        var horiz = ReshapeEngine.ConcatHorizontal(df1, df3);
        Assert.Equal(2, horiz.RowCount);
        Assert.Equal(3, horiz.ColumnCount);
        Assert.Equal("x", horiz["c"][0]);
    }

    [Fact]
    public void TestJoin_RightAndFullOuter()
    {
        var left = new DataFrame(
            Series.FromValues("id", new[] { 1, 2 }),
            Series.FromStrings("l_val", new[] { "L1", "L2" })
        );

        var right = new DataFrame(
            Series.FromValues("id", new[] { 2, 3 }),
            Series.FromStrings("r_val", new[] { "R2", "R3" })
        );

        var rightJoin = left.Join(right, "id", "id", JoinType.Right);
        Assert.Equal(2, rightJoin.RowCount); // id 2 and 3

        var outerJoin = left.Join(right, "id", "id", JoinType.FullOuter);
        Assert.Equal(3, outerJoin.RowCount); // id 1, 2, 3
    }

    [Fact]
    public void TestJoin_AsOfJoin_ForwardAndNearest()
    {
        var left = new DataFrame(
            Series.FromValues("time", new[] { 10.0, 20.0 })
        );

        var right = new DataFrame(
            Series.FromValues("time", new[] { 8.0, 12.0, 19.0, 25.0 }),
            Series.FromValues("val", new[] { 1.0, 2.0, 3.0, 4.0 })
        );

        var fwd = left.AsOfJoin(right, "time", "time", direction: AsOfDirection.Forward);
        Assert.Equal(2.0, fwd["val"][0]); // time 10 matches forward 12 (val=2.0)
        Assert.Equal(4.0, fwd["val"][1]); // time 20 matches forward 25 (val=4.0)

        var nearest = left.AsOfJoin(right, "time", "time", direction: AsOfDirection.Nearest);
        Assert.Equal(1.0, nearest["val"][0]); // |10-8|=2 vs |10-12|=2 -> 8 matches
        Assert.Equal(3.0, nearest["val"][1]); // |20-19|=1 vs |20-25|=5 -> 19 matches
    }
}
