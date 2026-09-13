using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TokenVector.Data.Common;
using TokenVector.Data.Core;

namespace TokenVector.Data.IO;

/// <summary>
/// High-throughput streaming NDJSON (JSON Lines) and JSON tabular reader.
/// </summary>
public static class FastJsonReader
{
    /// <summary>
    /// Reads an NDJSON (Newline Delimited JSON) file into a <see cref="DataFrame"/>.
    /// </summary>
    public static DataFrame ReadNdJsonFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = File.OpenRead(filePath);
        return ReadNdJsonStream(stream);
    }

    /// <summary>
    /// Reads an NDJSON string into a <see cref="DataFrame"/>.
    /// </summary>
    public static DataFrame ReadNdJsonString(string ndjson)
    {
        ArgumentNullException.ThrowIfNull(ndjson);
        using var reader = new StringReader(ndjson);
        return ReadFromTextReader(reader);
    }

    /// <summary>
    /// Reads an NDJSON stream into a <see cref="DataFrame"/>.
    /// </summary>
    public static DataFrame ReadNdJsonStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream);
        return ReadFromTextReader(reader);
    }

    /// <summary>
    /// Reads a standard JSON array of objects into a <see cref="DataFrame"/>.
    /// </summary>
    public static DataFrame ReadJsonArrayString(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Root element must be a JSON array.");
        }

        var rows = new List<Dictionary<string, object?>>();
        var allKeys = new HashSet<string>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                var dict = new Dictionary<string, object?>();
                foreach (var prop in item.EnumerateObject())
                {
                    allKeys.Add(prop.Name);
                    dict[prop.Name] = ExtractJsonValue(prop.Value);
                }
                rows.Add(dict);
            }
        }

        return BuildDataFrameFromRows(rows, allKeys);
    }

    private static DataFrame ReadFromTextReader(TextReader reader)
    {
        var rows = new List<Dictionary<string, object?>>();
        var allKeys = new HashSet<string>();

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var dict = new Dictionary<string, object?>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    allKeys.Add(prop.Name);
                    dict[prop.Name] = ExtractJsonValue(prop.Value);
                }
                rows.Add(dict);
            }
        }

        return BuildDataFrameFromRows(rows, allKeys);
    }

    private static DataFrame BuildDataFrameFromRows(List<Dictionary<string, object?>> rows, HashSet<string> allKeys)
    {
        if (rows.Count == 0 || allKeys.Count == 0) return DataFrame.Empty;

        int rowCount = rows.Count;
        var keyList = allKeys.ToList();
        var resultSeries = new List<Series>(keyList.Count);

        foreach (var key in keyList)
        {
            // Infer type
            bool isInt = true;
            bool isFloat = true;
            bool isBool = true;
            int nonNullCount = 0;

            for (int r = 0; r < rowCount; r++)
            {
                if (rows[r].TryGetValue(key, out var val) && val is not null)
                {
                    nonNullCount++;
                    if (isInt && val is not long && val is not int) isInt = false;
                    if (isFloat && val is not double && val is not float && val is not long && val is not int) isFloat = false;
                    if (isBool && val is not bool) isBool = false;
                }
            }

            var mask = new BitmapMask(rowCount, initialValue: true);

            if (isBool && nonNullCount > 0)
            {
                var boolData = new bool[rowCount];
                for (int r = 0; r < rowCount; r++)
                {
                    if (rows[r].TryGetValue(key, out var val) && val is bool b)
                        boolData[r] = b;
                    else
                        mask.Set(r, false);
                }
                resultSeries.Add(new Series(key, new Column<bool>(boolData, mask.All() ? null : mask)));
            }
            else if (isInt && nonNullCount > 0)
            {
                var intData = new long[rowCount];
                for (int r = 0; r < rowCount; r++)
                {
                    if (rows[r].TryGetValue(key, out var val) && val is not null)
                        intData[r] = Convert.ToInt64(val);
                    else
                        mask.Set(r, false);
                }
                resultSeries.Add(new Series(key, new Column<long>(intData, mask.All() ? null : mask)));
            }
            else if (isFloat && nonNullCount > 0)
            {
                var floatData = new double[rowCount];
                for (int r = 0; r < rowCount; r++)
                {
                    if (rows[r].TryGetValue(key, out var val) && val is not null)
                        floatData[r] = Convert.ToDouble(val);
                    else
                        mask.Set(r, false);
                }
                resultSeries.Add(new Series(key, new Column<double>(floatData, mask.All() ? null : mask)));
            }
            else
            {
                var strData = new string?[rowCount];
                for (int r = 0; r < rowCount; r++)
                {
                    if (rows[r].TryGetValue(key, out var val) && val is not null)
                        strData[r] = val.ToString();
                    else
                    {
                        strData[r] = null;
                        mask.Set(r, false);
                    }
                }
                resultSeries.Add(new Series(key, StringColumn.FromStrings(strData)));
            }
        }

        return new DataFrame(resultSeries);
    }

    private static object? ExtractJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText()
    };
}
