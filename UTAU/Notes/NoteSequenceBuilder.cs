using UTAU.Models;

namespace UTAU.Notes;

internal readonly record struct NoteBuildOptions(
    int BaseTone,
    int SyllableTicks,
    int ShortRestTicks,
    int LongRestTicks,
    int SokuonTicks)
{
    public const int BaseSyllableTicks = TimeBase.TicksPerQuarterNote / 2;
    public const int BaseShortRestTicks = TimeBase.TicksPerQuarterNote / 4;
    public const int BaseLongRestTicks = TimeBase.TicksPerQuarterNote;
    public const int BaseSokuonTicks = TimeBase.TicksPerQuarterNote / 4;

    public static NoteBuildOptions Create(int baseTone) => new(
        Math.Clamp(baseTone, 0, 127),
        BaseSyllableTicks,
        BaseShortRestTicks,
        BaseLongRestTicks,
        BaseSokuonTicks);
}

internal static class NoteSequenceBuilder
{
    public static IReadOnlyList<UTAUNote> Build(string text, NoteBuildOptions options)
        => Build(NotationScanner.Scan(text ?? string.Empty), options);

    public static IReadOnlyList<UTAUNote> Build(IReadOnlyList<NotationToken> tokens, NoteBuildOptions options)
    {
        var notes = new List<UTAUNote>();
        var tone = options.BaseTone;
        int? pendingLength = null;

        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case NotationTokenKind.Directive:
                    if (token.Tone is { } directiveTone)
                        tone = directiveTone;
                    pendingLength = token.LengthTicks ?? pendingLength;
                    break;

                case NotationTokenKind.Rest:
                    AppendRest(
                        notes,
                        token.LengthTicks
                            ?? (NotationScanner.IsLongRest(token.Text) ? options.LongRestTicks : options.ShortRestTicks),
                        tone);
                    break;

                case NotationTokenKind.Extend:
                    if (notes.Count > 0 && !notes[^1].IsRest)
                        notes[^1].LengthTicks += options.SyllableTicks;
                    break;

                case NotationTokenKind.Sokuon:
                    notes.Add(CreateNote(token.Text, tone, options.SokuonTicks));
                    break;

                case NotationTokenKind.Syllable:
                    notes.Add(CreateNote(token.Text, tone, pendingLength ?? options.SyllableTicks));
                    pendingLength = null;
                    break;
            }
        }

        return notes;
    }

    static void AppendRest(List<UTAUNote> notes, int lengthTicks, int tone)
    {
        if (notes.Count > 0 && notes[^1].IsRest)
        {
            notes[^1].LengthTicks += lengthTicks;
            return;
        }
        notes.Add(CreateNote(UTAUNote.RestLyric, tone, lengthTicks));
    }

    static UTAUNote CreateNote(string lyric, int tone, int lengthTicks) => new()
    {
        Lyric = lyric,
        Tone = tone,
        LengthTicks = lengthTicks,
    };
}
