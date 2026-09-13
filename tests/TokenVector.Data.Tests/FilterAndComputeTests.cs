using System;
using TokenVector.Data.Common;
using TokenVector.Data.Compute;
using TokenVector.Data.Core;
using Xunit;

namespace TokenVector.Data.Tests;

public class FilterAndComputeTests
{
    [Fact]
    public void TestVectorMath_ArithmeticOperations()
    {
        var colA = new Column<double>(new[] { 10.0, 20.0, 30.0, 40.0 });
        var colB = new Column<double>(new[] { 2.0, 4.0, 5.0, 8.0 });

        var add = VectorMath.Add(colA, colB);
        Assert.Equal(12.0, add[0]);
        Assert.Equal(48.0, add[3]);

        var sub = VectorMath.Subtract(colA, colB);
        Assert.Equal(8.0, sub[0]);
        Assert.Equal(32.0, sub[3]);

        var mul = VectorMath.Multiply(colA, colB);
        Assert.Equal(20.0, mul[0]);
        Assert.Equal(320.0, mul[3]);

        var div = VectorMath.Divide(colA, colB);
        Assert.Equal(5.0, div[0]);
        Assert.Equal(5.0, div[3]);

        var pow = VectorMath.Pow(colA, 2.0);
        Assert.Equal(100.0, pow[0]);
        Assert.Equal(1600.0, pow[3]);
    }

    [Fact]
    public void TestVectorMath_UnaryAndTrig()
    {
        var col = new Column<double>(new[] { -4.0, 0.0, 9.0, 16.0 });
        var abs = VectorMath.Abs(col);
        Assert.Equal(4.0, abs[0]);

        var sqrt = VectorMath.Sqrt(col);
        Assert.Equal(3.0, sqrt[2]);
        Assert.Equal(4.0, sqrt[3]);

        var exp = VectorMath.Exp(new Column<double>(new[] { 0.0, 1.0 }));
        Assert.Equal(1.0, exp[0], 5);
        Assert.Equal(Math.E, exp[1], 5);
    }

    [Fact]
    public void TestVectorMath_Comparisons()
    {
        var col = new Column<int>(new[] { 5, 10, 15, 20, 25 });
        var maskGt = VectorMath.GreaterThanScalar(col, 12);
        Assert.Equal(3, maskGt.PopCount());
        Assert.False(maskGt[0]);
        Assert.False(maskGt[1]);
        Assert.True(maskGt[2]);
        Assert.True(maskGt[3]);
        Assert.True(maskGt[4]);

        var maskEq = VectorMath.EqualScalar(col, 10);
        Assert.Equal(1, maskEq.PopCount());
        Assert.True(maskEq[1]);
    }

    [Fact]
    public void TestFilterEngine_ParallelPredicate()
    {
        var df = new DataFrame(
            Series.FromValues("age", new[] { 18, 25, 30, 42, 55, 65 }),
            Series.FromValues("income", new[] { 30000.0, 50000.0, 75000.0, 120000.0, 90000.0, 60000.0 })
        );

        var filtered = df.Filter(row => (int)df["age"][row]! >= 30);
        Assert.Equal(4, filtered.RowCount);
        Assert.Equal(30, filtered["age"][0]);
        Assert.Equal(65, filtered["age"][3]);
    }

    [Fact]
    public void TestWindowFunctions_RollingStatistics()
    {
        var col = new Column<double>(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });

        var rollMean = WindowFunctions.RollingMean(col, windowSize: 3, minPeriods: 1);
        Assert.Equal(1.0, rollMean[0]);
        Assert.Equal(1.5, rollMean[1]); // (1+2)/2
        Assert.Equal(2.0, rollMean[2]); // (1+2+3)/3
        Assert.Equal(3.0, rollMean[3]); // (2+3+4)/3
        Assert.Equal(4.0, rollMean[4]); // (3+4+5)/3

        var rollSum = WindowFunctions.RollingSum(col, windowSize: 2);
        Assert.Equal(1.0, rollSum[0]);
        Assert.Equal(3.0, rollSum[1]);
        Assert.Equal(9.0, rollSum[4]);
    }

    [Fact]
    public void TestWindowFunctions_ShiftDiffCumSumRank()
    {
        var col = new Column<double>(new[] { 10.0, 20.0, 15.0, 30.0 });

        var lag1 = (Column<double>)WindowFunctions.Shift(col, periods: 1);
        Assert.True(lag1.IsNull(0));
        Assert.Equal(10.0, lag1[1]);
        Assert.Equal(20.0, lag1[2]);

        var diff = WindowFunctions.Diff(col, periods: 1);
        Assert.True(diff.IsNull(0));
        Assert.Equal(10.0, diff[1]); // 20 - 10
        Assert.Equal(-5.0, diff[2]); // 15 - 20
        Assert.Equal(15.0, diff[3]); // 30 - 15

        var cumsum = WindowFunctions.CumSum(col);
        Assert.Equal(10.0, cumsum[0]);
        Assert.Equal(30.0, cumsum[1]);
        Assert.Equal(45.0, cumsum[2]);
        Assert.Equal(75.0, cumsum[3]);

        var rank = WindowFunctions.Rank(col, ascending: true);
        Assert.Equal(1.0, rank[0]); // 10.0 is rank 1
        Assert.Equal(3.0, rank[1]); // 20.0 is rank 3
        Assert.Equal(2.0, rank[2]); // 15.0 is rank 2
        Assert.Equal(4.0, rank[3]); // 30.0 is rank 4
    }

    [Fact]
    public void TestAggregations_VarianceStdAndQuantile()
    {
        var col = new Column<double>(new[] { 2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0 });
        double mean = Aggregations.Mean(col);
        Assert.Equal(5.0, mean);

        double var = Aggregations.Variance(col, ddof: 1);
        Assert.Equal(4.5714, var, 3);

        double std = Aggregations.Std(col, ddof: 1);
        Assert.Equal(Math.Sqrt(4.5714), std, 2);

        double q25 = Aggregations.Quantile(col, 0.25);
        double q50 = Aggregations.Quantile(col, 0.50);
        double q75 = Aggregations.Quantile(col, 0.75);

        Assert.Equal(4.0, q25);
        Assert.Equal(4.5, q50);
        Assert.Equal(5.5, q75);
    }

    [Fact]
    public void TestFilterEngine_CombineMasks()
    {
        var m1 = BitmapMask.FromIndices(5, new[] { 0, 1, 2 });
        var m2 = BitmapMask.FromIndices(5, new[] { 1, 2, 3 });
        var m3 = BitmapMask.FromIndices(5, new[] { 2, 3, 4 });

        var combinedAnd = FilterEngine.And(m1, m2, m3);
        Assert.Equal(1, combinedAnd.PopCount());
        Assert.True(combinedAnd[2]);

        var combinedOr = FilterEngine.Or(m1, m3);
        Assert.Equal(5, combinedOr.PopCount());
        Assert.True(combinedOr.All());
    }
}
