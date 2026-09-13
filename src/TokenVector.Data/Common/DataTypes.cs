using System;

namespace TokenVector.Data.Common;

/// <summary>
/// Supported primitive and structured data types in TokenVector.Data.
/// Aligned with Apache Arrow physical type representations.
/// </summary>
public enum DataType : byte
{
    /// <summary>8-bit signed integer</summary>
    Int8 = 1,
    /// <summary>16-bit signed integer</summary>
    Int16 = 2,
    /// <summary>32-bit signed integer</summary>
    Int32 = 3,
    /// <summary>64-bit signed integer</summary>
    Int64 = 4,

    /// <summary>8-bit unsigned integer</summary>
    UInt8 = 5,
    /// <summary>16-bit unsigned integer</summary>
    UInt16 = 6,
    /// <summary>32-bit unsigned integer</summary>
    UInt32 = 7,
    /// <summary>64-bit unsigned integer</summary>
    UInt64 = 8,

    /// <summary>32-bit IEEE 754 floating point</summary>
    Float32 = 9,
    /// <summary>64-bit IEEE 754 floating point</summary>
    Float64 = 10,

    /// <summary>Boolean (1 byte or bit-packed)</summary>
    Boolean = 11,

    /// <summary>UTF-8 variable-length binary string</summary>
    String = 12,

    /// <summary>64-bit timestamp in nanoseconds since Unix Epoch</summary>
    DateTime64 = 13
}

/// <summary>
/// Helper utilities and type conversions for <see cref="DataType"/>.
/// </summary>
public static class DataTypeHelper
{
    /// <summary>
    /// Maps a CLR type to its corresponding <see cref="DataType"/>.
    /// </summary>
    public static DataType FromClrType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(sbyte)) return DataType.Int8;
        if (underlying == typeof(short)) return DataType.Int16;
        if (underlying == typeof(int)) return DataType.Int32;
        if (underlying == typeof(long)) return DataType.Int64;
        if (underlying == typeof(byte)) return DataType.UInt8;
        if (underlying == typeof(ushort)) return DataType.UInt16;
        if (underlying == typeof(uint)) return DataType.UInt32;
        if (underlying == typeof(ulong)) return DataType.UInt64;
        if (underlying == typeof(float)) return DataType.Float32;
        if (underlying == typeof(double)) return DataType.Float64;
        if (underlying == typeof(bool)) return DataType.Boolean;
        if (underlying == typeof(string)) return DataType.String;
        if (underlying == typeof(DateTime)) return DataType.DateTime64;
        if (underlying == typeof(DateTimeOffset)) return DataType.DateTime64;

        throw new NotSupportedException($"CLR type '{type.FullName}' is not supported as a native columnar DataType.");
    }

    /// <summary>
    /// Maps a <see cref="DataType"/> to its native unmanaged or CLR type.
    /// </summary>
    public static Type ToClrType(DataType dataType) => dataType switch
    {
        DataType.Int8 => typeof(sbyte),
        DataType.Int16 => typeof(short),
        DataType.Int32 => typeof(int),
        DataType.Int64 => typeof(long),
        DataType.UInt8 => typeof(byte),
        DataType.UInt16 => typeof(ushort),
        DataType.UInt32 => typeof(uint),
        DataType.UInt64 => typeof(ulong),
        DataType.Float32 => typeof(float),
        DataType.Float64 => typeof(double),
        DataType.Boolean => typeof(bool),
        DataType.String => typeof(string),
        DataType.DateTime64 => typeof(long),
        _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Unsupported DataType.")
    };

    /// <summary>
    /// Gets the fixed byte size for fixed-width data types, or 0 for variable-length types.
    /// </summary>
    public static int GetByteSize(DataType dataType) => dataType switch
    {
        DataType.Int8 or DataType.UInt8 or DataType.Boolean => 1,
        DataType.Int16 or DataType.UInt16 => 2,
        DataType.Int32 or DataType.UInt32 or DataType.Float32 => 4,
        DataType.Int64 or DataType.UInt64 or DataType.Float64 or DataType.DateTime64 => 8,
        DataType.String => 0,
        _ => 0
    };

    /// <summary>
    /// Checks if the data type is a numeric type.
    /// </summary>
    public static bool IsNumeric(DataType dataType) => dataType switch
    {
        DataType.Int8 or DataType.Int16 or DataType.Int32 or DataType.Int64 or
        DataType.UInt8 or DataType.UInt16 or DataType.UInt32 or DataType.UInt64 or
        DataType.Float32 or DataType.Float64 => true,
        _ => false
    };

    /// <summary>
    /// Checks if the data type is a floating point type.
    /// </summary>
    public static bool IsFloatingPoint(DataType dataType) =>
        dataType is DataType.Float32 or DataType.Float64;
}
