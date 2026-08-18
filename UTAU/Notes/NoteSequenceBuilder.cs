using UTAU.Models;

namespace UTAU.Notes;

internal readonly record struct NoteBuildOptions(
    int BaseTone,
    double SyllableMilliseconds,
    double ShortRestMilliseconds,
    double LongRestMilliseconds,
    double SokuonMilliseconds,
    double Tempo)
{
    public const double BaseSyllableMilliseconds = 200.0;
    public const double BaseShortRestMilliseconds = 120.0;
    public const double BaseLongRestMilliseconds = 260.0;
    public const double BaseSokuonMilliseconds = 90.0;
    public const double DefaultTempo = 120.0;

    public static NoteBuildOptions Create(int baseTone, double speed, double tempo)
    {
        var scale = 1.0 / Math.Clamp(speed, 0.1, 10.0);
        return new NoteBuildOptions(
            Math.Clamp(baseTone, 0, 127),
            BaseSyllableMilliseconds * scale,
            BaseShortRestMilliseconds * scale,
            BaseLongRestMilliseconds * scale,
            BaseSokuonMilliseconds * scale,
            Math.Clamp(tempo, 20.0, 400.0));
    }

    public double WholeNoteMilliseconds => 4.0 * 60000.0 / Tempo;
}

internal static class NoteSequenceBuilder
{
    public static IReadOnlyList<UTAUNote> Build(string text, NoteBuildOptions options)
        => Build(NotationScanner.Scan(text ?? string.Empty), options);

    public static IReadOnlyList<UTAUNote> Build(IReadOnlyList<NotationToken> tokens, NoteBuildOptions options)
    {
        var notes = new List<UTAUNote>();
        var tone = options.BaseTone;
        double? pendingLength = null;

        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case NotationTokenKind.Directive:
                    if (token.Tone is { } directiveTone)
                        tone = directiveTone;
                    pendingLength = ResolveLength(token, options) ?? pendingLength;
                    break;

                case NotationTokenKind.Rest:
                    AppendRest(
                        notes,
                        ResolveLength(token, options)
                            ?? (NotationScanner.IsLongRest(token.Text) ? options.LongRestMilliseconds : options.ShortRestMilliseconds),
                        tone);
                    break;

                case NotationTokenKind.Extend:
                    if (notes.Count > 0 && !notes[^1].IsRest)
                        notes[^1].LengthMilliseconds += options.SyllableMilliseconds;
                    break;

                case NotationTokenKind.Sokuon:
                    notes.Add(CreateNote(token.Text, tone, options.SokuonMilliseconds));
                    break;

                case NotationTokenKind.Syllable:
                    notes.Add(CreateNote(token.Text, tone, pendingLength ?? options.SyllableMilliseconds));
                    pendingLength = null;
                    break;
            }
        }

        return notes;
    }

    static void AppendRest(List<UTAUNote> notes, double lengthMilliseconds, int tone)
    {
        if (notes.Count > 0 && notes[^1].IsRest)
        {
            notes[^1].LengthMilliseconds += lengthMilliseconds;
            return;
        }
        notes.Add(CreateNote(UTAUNote.RestLyric, tone, lengthMilliseconds));
    }

    static UTAUNote CreateNote(string lyric, int tone, double lengthMilliseconds) => new()
    {
        Lyric = lyric,
        Tone = tone,
        LengthMilliseconds = lengthMilliseconds,
    };

    static double? ResolveLength(NotationToken token, NoteBuildOptions options)
    {
        if (token.LengthMilliseconds is { } milliseconds)
            return milliseconds;
        if (token.LengthWholeNotes is { } wholeNotes)
            return wholeNotes * options.WholeNoteMilliseconds;
        return null;
    }
}
