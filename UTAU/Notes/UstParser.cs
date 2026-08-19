using System.IO;
using System.Text;
using UTAU.Models;

namespace UTAU.Notes;

internal static class UstParser
{
    static readonly byte[] CharsetPrefix = "Charset="u8.ToArray();

    public static UstDocument Parse(ReadOnlySpan<byte> bytes)
        => Parse(VoiceBankTextReader.Decode(bytes, ResolveEncoding(bytes)));

    public static UstDocument? ParseFile(string path)
    {
        try
        {
            return Parse(File.ReadAllBytes(path));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    public static Encoding ResolveEncoding(ReadOnlySpan<byte> bytes)
    {
        var detected = VoiceBankTextReader.Detect(bytes);
        if (detected.CodePage == Encoding.Unicode.CodePage || detected.CodePage == Encoding.BigEndianUnicode.CodePage)
            return detected;

        return VoiceBankTextReader.ResolveDeclaredEncoding(FindDeclaredCharset(bytes)) ?? detected;
    }

    public static UstDocument Parse(string content)
    {
        var sections = new List<UstSection>();
        string? header = null;
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bareLines = new List<string>();

        foreach (var line in VoiceBankTextReader.ReadLines(content))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']')
            {
                if (header is not null)
                    sections.Add(new UstSection(header, entries, bareLines));
                header = trimmed;
                entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bareLines = [];
                continue;
            }

            if (header is null)
                continue;

            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                bareLines.Add(trimmed);
                continue;
            }

            var key = line[..separator].Trim();
            if (key.Length == 0)
                continue;

            entries[key] = line[(separator + 1)..];
        }

        if (header is not null)
            sections.Add(new UstSection(header, entries, bareLines));

        return new UstDocument(sections);
    }

    static string? FindDeclaredCharset(ReadOnlySpan<byte> bytes)
    {
        while (!bytes.IsEmpty)
        {
            var end = bytes.IndexOfAny((byte)'\r', (byte)'\n');
            var line = (end < 0 ? bytes : bytes[..end]).Trim((byte)' ');

            if (line.Length >= 2 && line[0] == (byte)'['
                && !EqualsAscii(line, UstKeys.VersionHeader)
                && !EqualsAscii(line, UstKeys.SettingHeader))
                return null;

            if (line.Length > CharsetPrefix.Length && EqualsAscii(line[..CharsetPrefix.Length], UstKeys.Charset + "="))
                return Encoding.ASCII.GetString(line[CharsetPrefix.Length..]).Trim();

            if (end < 0)
                return null;
            bytes = bytes[(end + 1)..];
        }
        return null;
    }

    static bool EqualsAscii(ReadOnlySpan<byte> line, string text)
    {
        if (line.Length != text.Length)
            return false;

        for (var i = 0; i < text.Length; i++)
        {
            if (char.ToUpperInvariant((char)line[i]) != char.ToUpperInvariant(text[i]))
                return false;
        }
        return true;
    }
}
