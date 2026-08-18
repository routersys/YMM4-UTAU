using System.IO;
using System.Text;
using System.Text.Unicode;

namespace UTAU.Models;

internal static class VoiceBankTextReader
{
    static readonly byte[] Utf8Preamble = [0xEF, 0xBB, 0xBF];
    static readonly byte[] Utf16LePreamble = [0xFF, 0xFE];
    static readonly byte[] Utf16BePreamble = [0xFE, 0xFF];
    static readonly UTF8Encoding Utf8NoBom = new(false, false);

    static VoiceBankTextReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ShiftJis = Encoding.GetEncoding(932, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
    }

    public static Encoding ShiftJis { get; }

    public static Encoding Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Utf8Preamble))
            return Utf8NoBom;
        if (bytes.StartsWith(Utf16LePreamble))
            return Encoding.Unicode;
        if (bytes.StartsWith(Utf16BePreamble))
            return Encoding.BigEndianUnicode;
        return Utf8.IsValid(bytes) ? Utf8NoBom : ShiftJis;
    }

    public static string Decode(ReadOnlySpan<byte> bytes, Encoding? forced = null)
    {
        var encoding = forced ?? Detect(bytes);
        var preamble = encoding.GetPreamble();
        if (preamble.Length > 0 && bytes.StartsWith(preamble))
            bytes = bytes[preamble.Length..];
        else if (encoding.CodePage == Utf8NoBom.CodePage && bytes.StartsWith(Utf8Preamble))
            bytes = bytes[Utf8Preamble.Length..];
        return encoding.GetString(bytes);
    }

    public static string? ReadAllText(string path, Encoding? forced = null)
    {
        try
        {
            return Decode(File.ReadAllBytes(path), forced);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static IEnumerable<string> ReadLines(string text)
    {
        var start = 0;
        while (start <= text.Length)
        {
            var index = text.AsSpan(start).IndexOfAny('\r', '\n');
            if (index < 0)
            {
                if (start < text.Length)
                    yield return text[start..];
                yield break;
            }

            yield return text.Substring(start, index);
            var breakPosition = start + index;
            start = breakPosition + 1;
            if (text[breakPosition] == '\r' && start < text.Length && text[start] == '\n')
                start++;
        }
    }

    public static Encoding? ResolveDeclaredEncoding(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            var encoding = Encoding.GetEncoding(name.Trim());
            return encoding.CodePage == Encoding.UTF8.CodePage ? Utf8NoBom : encoding;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
