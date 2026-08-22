using System.Globalization;
using System.Text;

namespace UTAU.Notes;

internal static class LyricSplitter
{
    public const char Quote = '"';
    public const char Separator = ' ';

    static readonly char[] Contracted =
        ['ゃ', 'ゅ', 'ょ', 'ぁ', 'ぃ', 'ぅ', 'ぇ', 'ぉ', 'ャ', 'ュ', 'ョ', 'ァ', 'ィ', 'ゥ', 'ェ', 'ォ'];

    public static List<string> Split(string? text)
    {
        var lyrics = new List<string>();
        if (string.IsNullOrEmpty(text))
            return lyrics;

        var builder = new StringBuilder();
        var rest = text.AsSpan();
        while (!rest.IsEmpty)
        {
            var element = Take(ref rest);
            if (ContainsWhitespace(element))
            {
                Flush(lyrics, builder);
            }
            else if (ContainsContracted(element))
            {
                builder.Append(element);
                lyrics.Add(builder.ToString());
                builder.Clear();
            }
            else if (ContainsStandalone(element))
            {
                Flush(lyrics, builder);
                builder.Append(element);
            }
            else if (IsQuote(element))
            {
                TakeQuoted(ref rest, lyrics, builder);
            }
            else
            {
                if (builder.Length > 0 && ContainsStandalone(builder))
                {
                    lyrics.Add(builder.ToString());
                    builder.Clear();
                }

                builder.Append(element);
            }
        }

        Flush(lyrics, builder);
        return lyrics;
    }

    public static string Join(IEnumerable<string> lyrics)
    {
        var builder = new StringBuilder();
        foreach (var lyric in lyrics)
        {
            if (builder.Length != 0)
                builder.Append(Separator);

            if (lyric.Length == 0)
                builder.Append(Quote).Append(Quote);
            else if (ContainsWhitespace(lyric))
                builder.Append(Quote).Append(lyric).Append(Quote);
            else if (!ContainsStandalone(lyric))
                builder.Append(lyric);
            else if (lyric.Length == 1 || (lyric.Length == 2 && ContainsContracted(lyric.AsSpan(1))))
                builder.Append(lyric);
            else
                builder.Append(Quote).Append(lyric).Append(Quote);
        }

        return builder.ToString();
    }

    static ReadOnlySpan<char> Take(ref ReadOnlySpan<char> rest)
    {
        var length = StringInfo.GetNextTextElementLength(rest);
        var element = rest[..length];
        rest = rest[length..];
        return element;
    }

    static void TakeQuoted(ref ReadOnlySpan<char> rest, List<string> lyrics, StringBuilder builder)
    {
        while (!rest.IsEmpty)
        {
            var element = Take(ref rest);
            if (IsQuote(element))
            {
                lyrics.Add(builder.ToString());
                builder.Clear();
                return;
            }

            builder.Append(element);
        }
    }

    static void Flush(List<string> lyrics, StringBuilder builder)
    {
        if (builder.Length == 0)
            return;

        lyrics.Add(builder.ToString());
        builder.Clear();
    }

    static bool IsQuote(ReadOnlySpan<char> element) => element.Length == 1 && element[0] == Quote;

    static bool ContainsWhitespace(ReadOnlySpan<char> element)
    {
        foreach (var value in element)
        {
            if (char.IsWhiteSpace(value))
                return true;
        }

        return false;
    }

    static bool ContainsContracted(ReadOnlySpan<char> element) => element.IndexOfAny(Contracted) >= 0;

    static bool ContainsStandalone(ReadOnlySpan<char> element)
    {
        foreach (var value in element)
        {
            if (IsStandalone(value))
                return true;
        }

        return false;
    }

    static bool ContainsStandalone(StringBuilder builder)
    {
        foreach (var chunk in builder.GetChunks())
        {
            if (ContainsStandalone(chunk.Span))
                return true;
        }

        return false;
    }

    static bool IsStandalone(char value)
        => value is >= '\u4e00' and <= '\u9fff'
            or >= '\u3040' and <= '\u309f'
            or >= '\u30a0' and <= '\u30ff'
            or >= '\uac00' and <= '\ud7af';
}
