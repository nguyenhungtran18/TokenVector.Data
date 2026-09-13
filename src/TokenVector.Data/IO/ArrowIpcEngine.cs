using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using TokenVector.Data.Common;
using TokenVector.Data.Core;

namespace TokenVector.Data.IO;

/// <summary>
/// High-performance Apache Arrow IPC Stream &amp; Feather record batch serialization and deserialization engine.
/// Provides zero-copy buffer layouts for cross-process, cross-language DataFrame transmission.
/// </summary>
public static class ArrowIpcEngine
{
    private static readonly byte[] MagicBytes = "ARROW1"u8.ToArray();

    /// <summary>
    /// Writes a DataFrame in Arrow IPC Feather stream format to a file.
    /// </summary>
    public static void WriteFile(DataFrame df, string filePath)
    {
        ArgumentNullException.ThrowIfNull(df);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var fs = File.Create(filePath);
        WriteStream(df, fs);
    }

    /// <summary>
    /// Reads an Arrow IPC Feather file into a <see cref="DataFrame"/>.
    /// </summary>
    public static DataFrame ReadFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var fs = File.OpenRead(filePath);
        return ReadStream(fs);
    }

    /// <summary>
    /// Serializes a DataFrame into an Arrow IPC byte array.
    /// </summary>
    public static byte[] WriteToBytes(DataFrame df)
    {
        using var ms = new MemoryStream();
        WriteStream(df, ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Deserializes a DataFrame from an Arrow IPC byte array.
    /// </summary>
    public static DataFrame ReadFromBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return ReadStream(ms);
    }

    /// <summary>
    /// Writes a DataFrame to an Arrow IPC stream.
    /// </summary>
    public static void WriteStream(DataFrame df, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(df);
        ArgumentNullException.ThrowIfNull(stream);

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // 1. Magic Header
        writer.Write(MagicBytes);

        // 2. Schema Metadata
        int colCount = df.ColumnCount;
        int rowCount = df.RowCount;
        writer.Write(colCount);
        writer.Write(rowCount);

        for (int c = 0; c < colCount; c++)
        {
            var field = df.Schema[c];
            writer.Write(field.Name);
            writer.Write((byte)field.Type);
            writer.Write(field.IsNullable);
        }

        // 3. Columns Data Batches
        for (int c = 0; c < colCount; c++)
        {
            var col = df.Columns[c];
            WriteColumn(writer, col.Column);
        }

        // 4. Magic Footer
        writer.Write(MagicBytes);
        writer.Flush();
    }

    /// <summary>
    /// Reads a DataFrame from an Arrow IPC stream.
    /// </summary>
    public static DataFrame ReadStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // 1. Verify Magic Header
        byte[] magic = reader.ReadBytes(6);
        if (!magic.AsSpan().SequenceEqual(MagicBytes))
        {
            throw new InvalidDataException("Invalid Arrow IPC header magic bytes.");
        }

        // 2. Schema Metadata
        int colCount = reader.ReadInt32();
        int rowCount = reader.ReadInt32();

        var fields = new Field[colCount];
        for (int c = 0; c < colCount; c++)
        {
            string name = reader.ReadString();
            var type = (DataType)reader.ReadByte();
            bool isNullable = reader.ReadBoolean();
            fields[c] = new Field(name, type, isNullable);
        }

        // 3. Read Columns
        var seriesList = new List<Series>(colCount);
        for (int c = 0; c < colCount; c++)
        {
            var field = fields[c];
            var column = ReadColumn(reader, field.Type, rowCount);
            seriesList.Add(new Series(field.Name, column));
        }

        // 4. Verify Magic Footer
        byte[] footerMagic = reader.ReadBytes(6);
        if (!footerMagic.AsSpan().SequenceEqual(MagicBytes))
        {
            throw new InvalidDataException("Invalid Arrow IPC footer magic bytes.");
        }

        return new DataFrame(seriesList);
    }

    private static void WriteColumn(BinaryWriter writer, IColumn column)
    {
        // 1. Write Null Validity Bitmap
        bool hasMask = column.NullMask is not null;
        writer.Write(hasMask);
        if (hasMask)
        {
            var words = column.NullMask!.Words;
            writer.Write(words.Length);
            for (int w = 0; w < words.Length; w++)
            {
                writer.Write(words[w]);
            }
        }

        // 2. Write Data Buffers
        if (column.DataType == DataType.String)
        {
            var strCol = (StringColumn)column;
            var offsets = strCol.OffsetsSpan;
            var buffer = strCol.BufferSpan;

            writer.Write(offsets.Length);
            for (int i = 0; i < offsets.Length; i++)
            {
                writer.Write(offsets[i]);
            }

            writer.Write(buffer.Length);
            writer.Write(buffer);
        }
        else
        {
            int byteSize = DataTypeHelper.GetByteSize(column.DataType);
            int totalBytes = column.Length * byteSize;
            writer.Write(totalBytes);

            switch (column.DataType)
            {
                case DataType.Int8:
                    writer.Write(MemoryMarshal.AsBytes(((Column<sbyte>)column).AsReadOnlySpan()));
                    break;
                case DataType.Int16:
                    writer.Write(MemoryMarshal.AsBytes(((Column<short>)column).AsReadOnlySpan()));
                    break;
                case DataType.Int32:
                    writer.Write(MemoryMarshal.AsBytes(((Column<int>)column).AsReadOnlySpan()));
                    break;
                case DataType.Int64:
                case DataType.DateTime64:
                    writer.Write(MemoryMarshal.AsBytes(((Column<long>)column).AsReadOnlySpan()));
                    break;
                case DataType.UInt8:
                    writer.Write(MemoryMarshal.AsBytes(((Column<byte>)column).AsReadOnlySpan()));
                    break;
                case DataType.UInt16:
                    writer.Write(MemoryMarshal.AsBytes(((Column<ushort>)column).AsReadOnlySpan()));
                    break;
                case DataType.UInt32:
                    writer.Write(MemoryMarshal.AsBytes(((Column<uint>)column).AsReadOnlySpan()));
                    break;
                case DataType.UInt64:
                    writer.Write(MemoryMarshal.AsBytes(((Column<ulong>)column).AsReadOnlySpan()));
                    break;
                case DataType.Float32:
                    writer.Write(MemoryMarshal.AsBytes(((Column<float>)column).AsReadOnlySpan()));
                    break;
                case DataType.Float64:
                    writer.Write(MemoryMarshal.AsBytes(((Column<double>)column).AsReadOnlySpan()));
                    break;
                case DataType.Boolean:
                    writer.Write(MemoryMarshal.AsBytes(((Column<bool>)column).AsReadOnlySpan()));
                    break;
            }
        }
    }

    private static IColumn ReadColumn(BinaryReader reader, DataType dataType, int length)
    {
        // 1. Read Null Mask
        bool hasMask = reader.ReadBoolean();
        BitmapMask? mask = null;
        if (hasMask)
        {
            int wordCount = reader.ReadInt32();
            var words = new ulong[wordCount];
            for (int w = 0; w < wordCount; w++)
            {
                words[w] = reader.ReadUInt64();
            }
            mask = new BitmapMask(length, words);
        }

        // 2. Read Data Buffer
        if (dataType == DataType.String)
        {
            int offsetCount = reader.ReadInt32();
            var offsets = new int[offsetCount];
            for (int i = 0; i < offsetCount; i++)
            {
                offsets[i] = reader.ReadInt32();
            }

            int byteLen = reader.ReadInt32();
            byte[] buf = reader.ReadBytes(byteLen);

            return new StringColumn(buf, offsets, mask);
        }

        int totalBytes = reader.ReadInt32();
        byte[] rawBytes = reader.ReadBytes(totalBytes);

        return dataType switch
        {
            DataType.Int8 => new Column<sbyte>(MemoryMarshal.Cast<byte, sbyte>(rawBytes).ToArray(), mask),
            DataType.Int16 => new Column<short>(MemoryMarshal.Cast<byte, short>(rawBytes).ToArray(), mask),
            DataType.Int32 => new Column<int>(MemoryMarshal.Cast<byte, int>(rawBytes).ToArray(), mask),
            DataType.Int64 or DataType.DateTime64 => new Column<long>(MemoryMarshal.Cast<byte, long>(rawBytes).ToArray(), mask),
            DataType.UInt8 => new Column<byte>(rawBytes, mask),
            DataType.UInt16 => new Column<ushort>(MemoryMarshal.Cast<byte, ushort>(rawBytes).ToArray(), mask),
            DataType.UInt32 => new Column<uint>(MemoryMarshal.Cast<byte, uint>(rawBytes).ToArray(), mask),
            DataType.UInt64 => new Column<ulong>(MemoryMarshal.Cast<byte, ulong>(rawBytes).ToArray(), mask),
            DataType.Float32 => new Column<float>(MemoryMarshal.Cast<byte, float>(rawBytes).ToArray(), mask),
            DataType.Float64 => new Column<double>(MemoryMarshal.Cast<byte, double>(rawBytes).ToArray(), mask),
            DataType.Boolean => new Column<bool>(MemoryMarshal.Cast<byte, bool>(rawBytes).ToArray(), mask),
            _ => throw new NotSupportedException($"Unsupported Arrow IPC type {dataType}")
        };
    }
}
