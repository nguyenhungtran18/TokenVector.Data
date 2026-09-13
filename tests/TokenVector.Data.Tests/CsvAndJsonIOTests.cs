using System;
using System.IO;
using TokenVector.Data.Common;
using TokenVector.Data.Core;
using TokenVector.Data.IO;
using Xunit;

namespace TokenVector.Data.Tests;

public class CsvAndJsonIOTests
{
    [Fact]
    public void TestCsvReader_BasicAndQuotedFields()
    {
        string csv = "id,name,score\n1,\"Alice\",95.5\n2,\"Bob, Jr.\",88.0\n3,\"Charlie \"\"The Great\"\"\",72.5\n";

        var df = FastCsvReader.ReadString(csv);
        Assert.Equal(3, df.RowCount);
        Assert.Equal(3, df.ColumnCount);
        Assert.Equal("Alice", df["name"][0]);
        Assert.Equal("Bob, Jr.", df["name"][1]);
        Assert.Equal("Charlie \"The Great\"", df["name"][2]);
        Assert.Equal(95.5, Convert.ToDouble(df["score"][0]));
    }

    [Fact]
    public void TestCsvWriter_RoundTrip()
    {
        var original = new DataFrame(
            Series.FromValues("id", new[] { 101, 102, 103 }),
            Series.FromStrings("city", new[] { "New York", "San Francisco, CA", "London\nUK" }),
            Series.FromValues("temp", new[] { 22.4, 18.2, 15.0 })
        );

        string csv = FastCsvWriter.WriteToString(original);
        var loaded = FastCsvReader.ReadString(csv);

        Assert.Equal(original.RowCount, loaded.RowCount);
        Assert.Equal(original.ColumnCount, loaded.ColumnCount);
        Assert.Equal("New York", loaded["city"][0]);
        Assert.Equal("San Francisco, CA", loaded["city"][1]);
        Assert.Equal("London\nUK", loaded["city"][2]);
    }

    [Fact]
    public void TestJsonReader_NDJson()
    {
        string ndjson = "{\"user\":\"alice\",\"age\":30,\"active\":true}\n{\"user\":\"bob\",\"age\":25,\"active\":false}\n";

        var df = FastJsonReader.ReadNdJsonString(ndjson);
        Assert.Equal(2, df.RowCount);
        Assert.Equal(3, df.ColumnCount);
        Assert.Equal("alice", df["user"][0]);
        Assert.Equal("bob", df["user"][1]);
        Assert.Equal(30L, Convert.ToInt64(df["age"][0]));
        Assert.True(Convert.ToBoolean(df["active"][0]));
        Assert.False(Convert.ToBoolean(df["active"][1]));
    }

    [Fact]
    public void TestJsonReader_JsonArray()
    {
        string json = "[{\"symbol\":\"AAPL\",\"price\":175.5},{\"symbol\":\"GOOGL\",\"price\":140.2}]";

        var df = FastJsonReader.ReadJsonArrayString(json);
        Assert.Equal(2, df.RowCount);
        Assert.Equal(2, df.ColumnCount);
        Assert.Equal("AAPL", df["symbol"][0]);
        Assert.Equal(175.5, Convert.ToDouble(df["price"][0]));
    }

    [Fact]
    public void TestCsvReader_SemicolonAndTabDelimiters()
    {
        string tsv = "colA\tcolB\n100\t200\n300\t400\n";
        var dfTsv = FastCsvReader.ReadString(tsv, new CsvReadOptions { Delimiter = '\t' });
        Assert.Equal(2, dfTsv.RowCount);
        Assert.Equal(100L, Convert.ToInt64(dfTsv["colA"][0]));
        Assert.Equal(400L, Convert.ToInt64(dfTsv["colB"][1]));

        string ssv = "x;y;z\n1.1;2.2;3.3\n4.4;5.5;6.6\n";
        var dfSsv = FastCsvReader.ReadString(ssv, new CsvReadOptions { Delimiter = ';' });
        Assert.Equal(2, dfSsv.RowCount);
        Assert.Equal(1.1, Convert.ToDouble(dfSsv["x"][0]));
        Assert.Equal(6.6, Convert.ToDouble(dfSsv["z"][1]));
    }

    [Fact]
    public void TestCsvReader_NoHeader()
    {
        string csv = "10,20\n30,40\n";
        var df = FastCsvReader.ReadString(csv, new CsvReadOptions { HasHeader = false });
        Assert.Equal(2, df.RowCount);
        Assert.Equal(2, df.ColumnCount);
        Assert.Equal("column_0", df.ColumnNames[0]);
        Assert.Equal(10L, Convert.ToInt64(df["column_0"][0]));
    }
}
