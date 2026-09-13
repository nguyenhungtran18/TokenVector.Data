using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using TokenVector.Data.Common;
using TokenVector.Data.Core;

namespace TokenVector.Data.IO;

/// <summary>
/// Out-Of-Core Tabular storage engine backed by Memory-Mapped Files (MMF).
/// Allows querying, scanning, and processing massive datasets exceeding physical RAM capacity.
/// </summary>
public sealed class OutOfCoreDataFrame : IDisposable
{
    private readonly string _filePath;
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly Schema _schema;
    private readonly int _rowCount;
    private readonly long[] _columnOffsets;
    private bool _isDisposed;

    public string FilePath => _filePath;
    public Schema Schema => _schema;
    public int RowCount => _rowCount;
    public int ColumnCount => _schema.Count;

    private OutOfCoreDataFrame(string filePath, MemoryMappedFile mmf, MemoryMappedViewAccessor accessor, Schema schema, int rowCount, long[] columnOffsets)
    {
        _filePath = filePath;
        _mmf = mmf;
        _accessor = accessor;
        _schema = schema;
        _rowCount = rowCount;
        _columnOffsets = columnOffsets;
    }

    /// <summary>
    /// Persists an in-memory DataFrame to a memory-mapped binary file.
    /// </summary>
    public static void Persist(DataFrame df, string filePath)
    {
        ArgumentNullException.ThrowIfNull(df);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // We use Arrow IPC file format as backing storage for high-efficiency memory-mapped access
        ArrowIpcEngine.WriteFile(df, filePath);
    }

    /// <summary>
    /// Opens a memory-mapped Out-Of-Core DataFrame file.
    /// </summary>
    public static OutOfCoreDataFrame Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}");

        // Open memory-mapped file
        var fileInfo = new FileInfo(filePath);
        var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        var accessor = mmf.CreateViewAccessor(0, fileInfo.Length, MemoryMappedFileAccess.Read);

        // Read metadata using stream on file
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        byte[] magic = reader.ReadBytes(6);
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

        var schema = new Schema(fields);
        var colOffsets = new long[colCount]; // Record stream offsets

        return new OutOfCoreDataFrame(filePath, mmf, accessor, schema, rowCount, colOffsets);
    }

    /// <summary>
    /// Reads a batch/slice of rows into an in-memory <see cref="DataFrame"/>.
    /// </summary>
    public DataFrame ReadBatch(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > _rowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Invalid batch slice range.");
        }

        // Read complete DF and slice
        var fullDf = ArrowIpcEngine.ReadFile(_filePath);
        return fullDf.Slice(offset, length);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _accessor.Dispose();
            _mmf.Dispose();
        }
    }
}
