using System.Globalization;
using UTAU.Models;

namespace UTAU.Notes;

internal enum NotationTokenKind
{
    Directive,
    Syllable,
    Rest,
    Extend,
    Sokuon,
}

internal sealed record NotationToken(
    NotationTokenKind Kind,
    string Text,
    int? Tone = null,
    int? LengthTicks = null,
    double? Tempo = null);

internal static class NotationScanner
{
    public const char DirectiveOpen = '<';
    public const char DirectiveClose = '>';
    public const char DirectiveMarker = '!';
    public const string TempoPrefix = "T=";
    public const char LongVowelMark = 'ー';

    static readonly char[] CombiningKana =
    [
        'ぁ', 'ぃ', 'ぅ', 'ぇ', 'ぉ', 'ゃ', 'ゅ', 'ょ', 'ゎ',
        'ァ', 'ィ', 'ゥ', 'ェ', 'ォ', 'ャ', 'ュ', 'ョ', 'ヮ',
    ];

    static readonly char[] ShortRestMarks = ['、', ',', '，', '・', ' ', '\t', '　'];
    static readonly char[] LongRestMarks = ['。', '.', '．', '?', '？', '!', '！', '\r', '\n'];

    public static IReadOnlyList<NotationToken> Scan(string text)
    {
        var tokens = new List<NotationToken>();
        if (string.IsNullOrEmpty(text))
            return tokens;

        var index = 0;
        while (index < text.Length)
        {
            var c = text[index];

            if (c == DirectiveOpen && TryScanDirective(text, index, out var directive, out var consumed))
            {
                tokens.Add(directive);
                index += consumed;
                continue;
            }

            if (Array.IndexOf(ShortRestMarks, c) >= 0 || Array.IndexOf(LongRestMarks, c) >= 0)
            {
                tokens.Add(new NotationToken(NotationTokenKind.Rest, c.ToString()));
                index++;
                continue;
            }

            if (c == LongVowelMark)
            {
                tokens.Add(new NotationToken(NotationTokenKind.Extend, c.ToString()));
                index++;
                continue;
            }

            if (c is 'っ' or 'ッ')
            {
                tokens.Add(new NotationToken(NotationTokenKind.Sokuon, c.ToString()));
                index++;
                continue;
            }

            var length = 1;
            while (index + length < text.Length && Array.IndexOf(CombiningKana, text[index + length]) >= 0)
                length++;

            tokens.Add(new NotationToken(NotationTokenKind.Syllable, text.Substring(index, length)));
            index += length;
        }

        return tokens;
    }

    public static bool IsLongRest(string text)
        => text.Length == 1 && Array.IndexOf(LongRestMarks, text[0]) >= 0;

    static bool TryScanDirective(string text, int start, out NotationToken token, out int consumed)
    {
        token = null!;
        consumed = 0;

        if (start + 2 >= text.Length || text[start + 1] != DirectiveMarker)
            return false;

        var close = text.IndexOf(DirectiveClose, start + 2);
        if (close < 0)
            return false;

        var content = text[(start + 2)..close];
        consumed = close - start + 1;

        var separator = content.IndexOf(':');
        var tonePart = (separator < 0 ? content : content[..separator]).Trim();
        var lengthPart = separator < 0 ? string.Empty : content[(separator + 1)..].Trim();

        int? lengthTicks = null;
        if (lengthPart.Length > 0 && !TryParseLength(lengthPart, out lengthTicks))
            return false;

        if (tonePart.Length == 0)
        {
            token = new NotationToken(NotationTokenKind.Directive, content, null, lengthTicks);
            return true;
        }

        if (TryParseTempo(tonePart, out var tempo))
        {
            token = new NotationToken(NotationTokenKind.Directive, content, null, lengthTicks, tempo);
            return true;
        }

        if (string.Equals(tonePart, "R", StringComparison.OrdinalIgnoreCase) || tonePart == "-")
        {
            token = new NotationToken(NotationTokenKind.Rest, tonePart, null, lengthTicks);
            return true;
        }

        if (!MusicalTone.TryParse(tonePart, out var tone))
            return false;

        token = new NotationToken(NotationTokenKind.Directive, content, tone.NoteNumber, lengthTicks);
        return true;
    }

    static bool TryParseTempo(string text, out double? tempo)
    {
        tempo = null;
        if (text.Length <= TempoPrefix.Length || !text.StartsWith(TempoPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!double.TryParse(text[TempoPrefix.Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value)
            || value < TimeBase.MinimumTempo
            || value > TimeBase.MaximumTempo)
            return false;

        tempo = value;
        return true;
    }

    static bool TryParseLength(string text, out int? ticks)
    {
        ticks = null;

        var slash = text.IndexOf('/');
        if (slash > 0)
        {
            if (!double.TryParse(text[..slash], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator)
                || !double.TryParse(text[(slash + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator)
                || !double.IsFinite(numerator)
                || !double.IsFinite(denominator)
                || numerator <= 0.0
                || denominator <= 0.0)
                return false;

            var value = TimeBase.FromWholeNotes(numerator / denominator);
            if (value < UTAUNote.MinimumLengthTicks || value > UTAUNote.MaximumLengthTicks)
                return false;

            ticks = value;
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || parsed < UTAUNote.MinimumLengthTicks
            || parsed > UTAUNote.MaximumLengthTicks)
            return false;

        ticks = parsed;
        return true;
    }
}
