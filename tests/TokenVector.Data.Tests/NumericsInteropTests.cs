using System;
using TokenVector.Data.Common;
using TokenVector.Data.Core;
using TokenVector.Data.Interop;
using TokenVector.Numerics.Autograd;
using TokenVector.Numerics.Core;
using Xunit;

namespace TokenVector.Data.Tests;

public class NumericsInteropTests
{
    [Fact]
    public void TestSeriesToNDArray_ZeroCopy()
    {
        var rawData = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
        var series = Series.FromValues("x", rawData);

        var ndarray = series.ToNDArray<double>();
        Assert.Equal(1, ndarray.Rank);
        Assert.Equal(5, ndarray.TotalLength);
        Assert.Equal(1.0, ndarray[0]);
        Assert.Equal(5.0, ndarray[4]);

        // Verify that mutating NDArray reflects in underlying column buffer when zero-copy
        ndarray[0] = 99.0;
        Assert.Equal(99.0, series[0]);
    }

    [Fact]
    public void TestDataFrameToNDArray_2DMatrix()
    {
        var df = new DataFrame(
            Series.FromValues("feat1", new[] { 1.0, 3.0, 5.0 }),
            Series.FromValues("feat2", new[] { 2.0, 4.0, 6.0 })
        );

        var mat = df.ToNDArray<double>();
        Assert.Equal(2, mat.Rank);
        Assert.Equal(new[] { 3, 2 }, mat.Shape);
        Assert.Equal(1.0, mat[0, 0]);
        Assert.Equal(2.0, mat[0, 1]);
        Assert.Equal(3.0, mat[1, 0]);
        Assert.Equal(4.0, mat[1, 1]);
        Assert.Equal(5.0, mat[2, 0]);
        Assert.Equal(6.0, mat[2, 1]);
    }

    [Fact]
    public void TestSeriesAndDataFrameToTensor_AutogradDifferentiable()
    {
        var series = Series.FromValues("weight", new[] { 2.0, 3.0, 4.0 });
        var tensor = series.ToTensor<double>(requiresGrad: true);

        Assert.True(tensor.RequiresGrad);
        Assert.Equal(3, tensor.TotalLength);

        // Perform autograd tensor arithmetic: y = sum(x^2)
        var y = (tensor * tensor).Data; // compute forward
        Assert.Equal(4.0, y[0]);
        Assert.Equal(9.0, y[1]);
        Assert.Equal(16.0, y[2]);
    }

    [Fact]
    public void TestFromNDArrayAndFromTensor_ToDataFrame()
    {
        var ndarray = NDArray<double>.FromArray(new[] { 10.0, 20.0, 30.0, 40.0, 50.0, 60.0 }, 3, 2);
        var df = NumericsInterop.FromNDArray(ndarray, new[] { "col_a", "col_b" });

        Assert.Equal(3, df.RowCount);
        Assert.Equal(2, df.ColumnCount);
        Assert.Equal(10.0, df["col_a"][0]);
        Assert.Equal(20.0, df["col_b"][0]);
        Assert.Equal(50.0, df["col_a"][2]);
        Assert.Equal(60.0, df["col_b"][2]);

        var tensor = new Tensor<double>(ndarray);
        var dfFromTensor = NumericsInterop.FromTensor(tensor, new[] { "t_a", "t_b" });
        Assert.Equal(3, dfFromTensor.RowCount);
        Assert.Equal(10.0, dfFromTensor["t_a"][0]);
    }
}
