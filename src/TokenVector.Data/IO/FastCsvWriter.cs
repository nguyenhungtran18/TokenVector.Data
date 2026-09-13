using System;
using System.IO;
using System.Text;
using TokenVector.Data.Core;

namespace TokenVector.Data.IO;

/// <summary>
/// Options for configuring CSV serialization.
/// </summary>
public sealed class CsvWriteOptions
{
    public char Delimiter { get; set; } = ',';
    public bool IncludeHeader { get; set; } = true;
    public string NullValue { get; set; } = "";
    public char QuoteChar { get; set; } = '"';
    public Encoding Encoding { get; set; } = Encoding.UTF8;
}

/// <summary>
/// High-throughput buffered CSV stream writer.
/// </summary>
public static class FastCsvWriter
{
    /// <summary>
    /// Writes a DataFrame to a CSV file.
    /// </summary>
    public static void WriteFile(DataFrame df, string filePath, CsvWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(df);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var opt = options ?? new CsvWriteOptions();
        using var stream = File.Create(filePath);
        WriteStream(df, stream, opt);
    }

    /// <summary>
    /// Writes a DataFrame to a writable Stream.
    /// </summary>
    public static void WriteStream(DataFrame df, Stream stream, CsvWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(df);
        ArgumentNullException.ThrowIfNull(stream);

        var opt = options ?? new CsvWriteOptions();
        using var writer = new StreamWriter(stream, opt.Encoding, bufferSize: 65536, leaveOpen: true);

        int colCount = df.ColumnCount;
        int rowCount = df.RowCount;

        // 1. Header
        if (opt.IncludeHeader)
        {
            for (int c = 0; c < colCount; c++)
            {
                if (c > 0) writer.Write(opt.Delimiter);
                WriteEscapedField(writer, df.ColumnNames[c], opt.Delimiter, opt.QuoteChar);
            }
            writer.WriteLine();
        }

        // 2. Data Rows
        var columns = df.Columns;
        for (int r = 0; r < rowCount; r++)
        {
            for (int c = 0; c < colCount; c++)
            {
                if (c > 0) writer.Write(opt.Delimiter);
                var col = columns[c];
                if (col.IsNull(r))
                {
                    writer.Write(opt.NullValue);
                }
                else
                {
                    string val = col[r]?.ToString() ?? "";
                    WriteEscapedField(writer, val, opt.Delimiter, opt.QuoteChar);
                }
            }
            writer.WriteLine();
        }

        writer.Flush();
    }

    /// <summary>
    /// Serializes a DataFrame to an in-memory CSV string.
    /// </summary>
    public static string WriteToString(DataFrame df, CsvWriteOptions? options = null)
    {
        using var ms = new MemoryStream();
        WriteStream(df, ms, options);
        var opt = options ?? new CsvWriteOptions();
        return opt.Encoding.GetString(ms.ToArray());
    }

    private static void WriteEscapedField(StreamWriter writer, string value, char delimiter, char quote)
    {
        bool needsQuotes = value.Contains(delimiter) || value.Contains(quote) || value.Contains('\n') || value.Contains('\r');

        if (!needsQuotes)
        {
            writer.Write(value);
            return;
        }

        writer.Write(quote);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == quote)
            {
                writer.Write(quote);
                writer.Write(quote);
            }
            else
            {
                writer.Write(c);
            }
        }
        writer.Write(quote);
    }
}
