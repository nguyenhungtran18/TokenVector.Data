using System;
using System.IO;
using TokenVector.Data.Common;
using TokenVector.Data.Core;
using TokenVector.Data.IO;
using Xunit;

namespace TokenVector.Data.Tests;

public class ArrowAndMemMapTests
{
    [Fact]
    public void TestArrowIpc_StreamRoundTrip()
    {
        var mask = new BitmapMask(4, initialValue: true);
        mask.Set(2, false); // null at index 2

        var original = new DataFrame(
            Series.FromValues("int_col", new[] { 10, 20, 30, 40 }),
            Series.FromValues("double_col", new[] { 1.5, 2.5, 0.0, 4.5 }, mask),
            Series.FromStrings("str_col", new[] { "one", "two", null, "four" })
        );

        byte[] bytes = ArrowIpcEngine.WriteToBytes(original);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        var restored = ArrowIpcEngine.ReadFromBytes(bytes);
        Assert.Equal(original.RowCount, restored.RowCount);
        Assert.Equal(original.ColumnCount, restored.ColumnCount);

        Assert.Equal(10, restored["int_col"][0]);
        Assert.Equal(40, restored["int_col"][3]);

        Assert.Equal(1.5, restored["double_col"][0]);
        Assert.True(restored["double_col"].IsNull(2));

        Assert.Equal("one", restored["str_col"][0]);
        Assert.True(restored["str_col"].IsNull(2));
        Assert.Equal("four", restored["str_col"][3]);
    }

    [Fact]
    public void TestOutOfCoreDataFrame_PersistAndBatchRead()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"oof_test_{Guid.NewGuid():N}.feather");
        try
        {
            var df = new DataFrame(
                Series.FromValues("id", new[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
                Series.FromValues("val", new[] { 10.0, 20.0, 30.0, 40.0, 50.0, 60.0, 70.0, 80.0 })
            );

            OutOfCoreDataFrame.Persist(df, tempFile);
            Assert.True(File.Exists(tempFile));

            using var ooc = OutOfCoreDataFrame.Open(tempFile);
            Assert.Equal(8, ooc.RowCount);
            Assert.Equal(2, ooc.ColumnCount);

            var batch = ooc.ReadBatch(offset: 2, length: 4);
            Assert.Equal(4, batch.RowCount);
            Assert.Equal(3, batch["id"][0]);
            Assert.Equal(6, batch["id"][3]);
            Assert.Equal(30.0, batch["val"][0]);
            Assert.Equal(60.0, batch["val"][3]);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
