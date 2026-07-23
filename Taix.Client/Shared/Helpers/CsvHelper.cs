using System;
using System.Collections.Generic;
using System.IO;

namespace Taix.Client.Shared.Helpers;

public static class CsvHelper
{
    public static void WriteCsv<T>(string path, IEnumerable<T> rows, Func<T, string> toLine, string header)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine(header);
        foreach (var row in rows)
            writer.WriteLine(toLine(row));
    }

    public static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
