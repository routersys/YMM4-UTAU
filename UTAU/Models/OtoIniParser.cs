using System.Globalization;
using System.IO;
using System.Text;

namespace UTAU.Models;

internal static class OtoIniParser
{
    const int NumericFieldCount = 5;

    static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static IReadOnlyList<OtoEntry> Parse(string directoryPath, string content)
    {
        var entries = new List<OtoEntry>();
        foreach (var line in VoiceBankTextReader.ReadLines(content))
        {
            var entry = ParseLine(directoryPath, line);
            if (entry is not null)
                entries.Add(entry);
        }
        return entries;
    }

    public static OtoEntry? ParseLine(string directoryPath, string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
            return null;

        var separator = trimmed.IndexOf('=');
        if (separator <= 0)
            return null;

        var sampleFileName = trimmed[..separator].Trim();
        if (sampleFileName.Length == 0 || sampleFileName.AsSpan().IndexOfAny(InvalidFileNameChars) >= 0)
            return null;

        var fields = trimmed[(separator + 1)..].Split(',');
        var alias = fields.Length > NumericFieldCount
            ? string.Join(',', fields[..^NumericFieldCount]).Trim()
            : fields[0].Trim();
        if (alias.Length == 0)
            alias = Path.GetFileNameWithoutExtension(sampleFileName);

        var numbers = new double[NumericFieldCount];
        var firstNumberIndex = fields.Length > NumericFieldCount ? fields.Length - NumericFieldCount : 1;
        for (var i = 0; i < NumericFieldCount; i++)
        {
            var fieldIndex = firstNumberIndex + i;
            numbers[i] = fieldIndex < fields.Length ? ParseNumber(fields[fieldIndex]) : 0.0;
        }

        return new OtoEntry(
            directoryPath,
            sampleFileName,
            alias,
            numbers[0],
            numbers[1],
            numbers[2],
            numbers[3],
            numbers[4]);
    }

    static double ParseNumber(string field)
    {
        var text = field.Trim();
        if (text.Length == 0)
            return 0.0;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value))
            return value;

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsAsciiDigit(c) || c is '-' or '+' or '.')
                builder.Append(c);
            else if (c is >= '０' and <= '９')
                builder.Append((char)(c - '０' + '0'));
            else if (c is '．')
                builder.Append('.');
            else if (c is '－' or 'ー')
                builder.Append('-');
        }

        return double.TryParse(builder.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value)
            ? value
            : 0.0;
    }
}
