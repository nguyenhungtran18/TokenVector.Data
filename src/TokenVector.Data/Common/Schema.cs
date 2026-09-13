using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TokenVector.Data.Common;

/// <summary>
/// Represents a named field in a DataFrame or Columnar table.
/// </summary>
public sealed class Field : IEquatable<Field>
{
    /// <summary>Name of the column/field.</summary>
    public string Name { get; }

    /// <summary>Logical data type of the field.</summary>
    public DataType Type { get; }

    /// <summary>Indicates whether values in this field can be null.</summary>
    public bool IsNullable { get; }

    /// <summary>Optional custom metadata key-value dictionary.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; }

    public Field(string name, DataType type, bool isNullable = true, IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Type = type;
        IsNullable = isNullable;
        Metadata = metadata;
    }

    public bool Equals(Field? other)
    {
        if (other is null) return false;
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &&
               Type == other.Type &&
               IsNullable == other.IsNullable;
    }

    public override bool Equals(object? obj) => obj is Field other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Name.ToLowerInvariant(), Type, IsNullable);

    public override string ToString() => $"{Name}: {Type}{(IsNullable ? "?" : "")}";
}

/// <summary>
/// Represents the complete metadata schema of a DataFrame or record batch.
/// </summary>
public sealed class Schema : IReadOnlyList<Field>, IEquatable<Schema>
{
    private readonly Field[] _fields;
    private readonly Dictionary<string, int> _nameToIndex;

    /// <summary>
    /// Total number of columns/fields in the schema.
    /// </summary>
    public int Count => _fields.Length;

    public Field this[int index] => _fields[index];
    public Field this[string name] => _fields[GetIndex(name)];

    public IReadOnlyList<Field> Fields => _fields;
    public IReadOnlyList<string> Names => _fields.Select(f => f.Name).ToArray();
    public IReadOnlyList<DataType> Types => _fields.Select(f => f.Type).ToArray();

    public Schema(IEnumerable<Field> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _fields = fields.ToArray();
        _nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < _fields.Length; i++)
        {
            var field = _fields[i];
            if (!_nameToIndex.TryAdd(field.Name, i))
            {
                throw new ArgumentException($"Duplicate field name '{field.Name}' in schema.");
            }
        }
    }

    public Schema(params Field[] fields) : this((IEnumerable<Field>)fields) { }

    /// <summary>
    /// Gets the zero-based column index for a field name.
    /// </summary>
    public int GetIndex(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_nameToIndex.TryGetValue(name, out int index))
        {
            throw new KeyNotFoundException($"Field '{name}' does not exist in schema. Available: [{string.Join(", ", Names)}]");
        }
        return index;
    }

    /// <summary>
    /// Attempts to get column index for a field name.
    /// </summary>
    public bool TryGetIndex(string name, out int index) =>
        _nameToIndex.TryGetValue(name, out index);

    /// <summary>
    /// Checks if a field with the specified name exists.
    /// </summary>
    public bool Contains(string name) => _nameToIndex.ContainsKey(name);

    /// <summary>
    /// Creates a new sub-schema selecting only specified column names.
    /// </summary>
    public Schema Select(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var selected = new Field[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            selected[i] = this[names[i]];
        }
        return new Schema(selected);
    }

    /// <summary>
    /// Creates a new sub-schema selecting only specified column indices.
    /// </summary>
    public Schema Project(params int[] indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        var selected = new Field[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            selected[i] = _fields[indices[i]];
        }
        return new Schema(selected);
    }

    /// <summary>
    /// Appends fields to create an extended schema.
    /// </summary>
    public Schema Append(params Field[] newFields)
    {
        ArgumentNullException.ThrowIfNull(newFields);
        return new Schema(_fields.Concat(newFields));
    }

    public IEnumerator<Field> GetEnumerator() => ((IEnumerable<Field>)_fields).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _fields.GetEnumerator();

    public bool Equals(Schema? other)
    {
        if (other is null || _fields.Length != other._fields.Length) return false;
        for (int i = 0; i < _fields.Length; i++)
        {
            if (!_fields[i].Equals(other._fields[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is Schema other && Equals(other);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        foreach (var f in _fields) hc.Add(f);
        return hc.ToHashCode();
    }

    public override string ToString()
    {
        var sb = new StringBuilder("Schema(");
        for (int i = 0; i < _fields.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(_fields[i]);
        }
        sb.Append(')');
        return sb.ToString();
    }
}
