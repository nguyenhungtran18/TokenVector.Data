using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TokenVector.Data.Common;
using TokenVector.Data.Core;
using TokenVector.Numerics.Autograd;
using TokenVector.Numerics.Core;

namespace TokenVector.Data.Interop;

/// <summary>
/// High-performance Zero-Copy and Low-Overhead bridge between TokenVector.Data and TokenVector.Numerics.
/// Facilitates seamless interoperability between DataFrames/Series and NDArray/Tensor autograd engines.
/// </summary>
public static class NumericsInterop
{
    #region Series to NDArray / Tensor

    /// <summary>
    /// Converts a 1D <see cref="Series"/> directly to a 1D <see cref="NDArray{T}"/>.
    /// Performs a Zero-Copy wrap of the underlying memory buffer when types match and no null values exist.
    /// </summary>
    public static NDArray<T> ToNDArray<T>(this Series series) where T : unmanaged, INumber<T>
    {
        ArgumentNullException.ThrowIfNull(series);

        if (series.Column is Column<T> typedCol && (typedCol.NullMask is null || typedCol.NullMask.All()))
        {
            // Direct Zero-Copy wrap of contiguous array
            return NDArray<T>.FromArray(typedCol.RawData, series.Length);
        }

        // Materialize copy with type conversion
        var data = new T[series.Length];
        for (int i = 0; i < series.Length; i++)
        {
            object? val = series[i];
            data[i] = val is not null ? T.CreateChecked(Convert.ToDouble(val)) : T.Zero;
        }

        return NDArray<T>.FromArray(data, series.Length);
    }

    /// <summary>
    /// Converts a 1D <see cref="Series"/> to an Autograd <see cref="Tensor{T}"/>.
    /// </summary>
    public static Tensor<T> ToTensor<T>(this Series series, bool requiresGrad = false)
        where T : unmanaged, IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        var ndarray = series.ToNDArray<T>();
        return new Tensor<T>(ndarray, requiresGrad);
    }

    #endregion

    #region DataFrame to NDArray / Tensor

    /// <summary>
    /// Converts selected numeric columns of a <see cref="DataFrame"/> to a 2D C-contiguous <see cref="NDArray{T}"/>.
    /// </summary>
    public static NDArray<T> ToNDArray<T>(this DataFrame df, string[]? columns = null) where T : unmanaged, INumber<T>
    {
        ArgumentNullException.ThrowIfNull(df);

        var targetColNames = columns ?? df.Columns
            .Where(c => DataTypeHelper.IsNumeric(c.DataType))
            .Select(c => c.Name)
            .ToArray();

        if (targetColNames.Length == 0)
        {
            throw new ArgumentException("No numeric columns found to convert to NDArray.");
        }

        int rowCount = df.RowCount;
        int colCount = targetColNames.Length;
        var flatData = new T[rowCount * colCount];

        var seriesList = targetColNames.Select(name => df[name]).ToArray();

        for (int c = 0; c < colCount; c++)
        {
            var s = seriesList[c];
            for (int r = 0; r < rowCount; r++)
            {
                object? val = s[r];
                T numVal = val is not null ? T.CreateChecked(Convert.ToDouble(val)) : T.Zero;
                flatData[r * colCount + c] = numVal;
            }
        }

        return NDArray<T>.FromArray(flatData, rowCount, colCount);
    }

    /// <summary>
    /// Converts a <see cref="DataFrame"/> to a 2D differentiable <see cref="Tensor{T}"/>.
    /// </summary>
    public static Tensor<T> ToTensor<T>(this DataFrame df, string[]? columns = null, bool requiresGrad = false)
        where T : unmanaged, IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        var ndarray = df.ToNDArray<T>(columns);
        return new Tensor<T>(ndarray, requiresGrad);
    }

    #endregion

    #region NDArray / Tensor to DataFrame

    /// <summary>
    /// Wraps or converts a 2D or 1D <see cref="NDArray{T}"/> into a <see cref="DataFrame"/>.
    /// </summary>
    public static DataFrame FromNDArray<T>(NDArray<T> ndarray, string[]? columnNames = null) where T : unmanaged, INumber<T>
    {
        ArgumentNullException.ThrowIfNull(ndarray);

        if (ndarray.Rank == 1)
        {
            string colName = (columnNames is not null && columnNames.Length > 0) ? columnNames[0] : "col_0";
            var data = ndarray.Contiguous().AsSpan().ToArray();
            var col = new Column<T>(data);
            return new DataFrame(new Series(colName, col));
        }

        if (ndarray.Rank != 2)
        {
            throw new ArgumentException($"Only 1D and 2D NDArrays can be converted to DataFrame. Got Rank {ndarray.Rank}.");
        }

        int rows = ndarray.Shape[0];
        int cols = ndarray.Shape[1];

        var names = columnNames ?? Enumerable.Range(0, cols).Select(i => $"col_{i}").ToArray();
        if (names.Length != cols)
        {
            throw new ArgumentException($"Column names count {names.Length} does not match NDArray columns {cols}.");
        }

        var resultSeries = new List<Series>(cols);
        for (int c = 0; c < cols; c++)
        {
            var colData = new T[rows];
            for (int r = 0; r < rows; r++)
            {
                colData[r] = ndarray[r, c];
            }
            resultSeries.Add(new Series(names[c], new Column<T>(colData)));
        }

        return new DataFrame(resultSeries);
    }

    /// <summary>
    /// Wraps or converts a <see cref="Tensor{T}"/> into a <see cref="DataFrame"/>.
    /// </summary>
    public static DataFrame FromTensor<T>(Tensor<T> tensor, string[]? columnNames = null)
        where T : unmanaged, IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        ArgumentNullException.ThrowIfNull(tensor);
        return FromNDArray(tensor.Data, columnNames);
    }

    #endregion
}
