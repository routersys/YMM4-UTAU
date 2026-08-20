using UTAU.Models;
using UTAU.Notes;

namespace UTAU.Phonemes;

internal readonly record struct PhonemizeOptions(double TransitionTailMilliseconds, double EndingMilliseconds)
{
    public const double BaseTransitionTailMilliseconds = 30.0;
    public const double BaseEndingMilliseconds = 120.0;
    public const double MinimumTransitionMilliseconds = 20.0;

    public static PhonemizeOptions Default => new(BaseTransitionTailMilliseconds, BaseEndingMilliseconds);
}

internal static class Phonemizer
{
    public static IReadOnlyList<PhonemeUnit> Phonemize(
        VoiceBank bank,
        TempoMap tempoMap,
        string? color,
        PhonemizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(tempoMap);

        var notes = tempoMap.Notes;
        var units = new List<PhonemeUnit>(notes.Count + notes.Count / 2 + 1);
        var previousVowel = KanaRomanization.StartVowel.AsSpan();
        Span<char> vowelBuffer = stackalloc char[KanaRomanization.StackTextLength];
        PhonemeUnit? pending = null;

        for (var i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            var start = tempoMap.StartMilliseconds(i);
            var length = tempoMap.LengthMilliseconds(i);

            if (note.IsRest)
            {
                Emit(bank, units, ref pending, new PhonemeUnit(note, null, UTAUNote.RestLyric, start, length, start, length, note.Tone), color, options);
                previousVowel = KanaRomanization.StartVowel;
                continue;
            }

            var context = note.SuppressAutoVcv ? default : previousVowel;
            var entry = AliasResolver.Resolve(bank, note.Lyric, context, note.Tone, color, note.IgnorePrefixMap, out var alias);
            Emit(bank, units, ref pending, new PhonemeUnit(note, entry, alias, start, length, start, length, note.Tone), color, options);

            var vowel = KanaRomanization.GetVowel(note.Lyric, vowelBuffer);
            previousVowel = entry is null ? KanaRomanization.StartVowel : vowel;
        }

        if (pending is { } trailing)
            units.Add(trailing);

        AppendEnding(bank, units, color, options);
        return units;
    }

    static void Emit(
        VoiceBank bank,
        List<PhonemeUnit> units,
        ref PhonemeUnit? pending,
        PhonemeUnit current,
        string? color,
        PhonemizeOptions options)
    {
        if (pending is { } previous)
        {
            if (TryBuildTransition(bank, previous, current, color, options) is { } transition)
            {
                units.Add(previous with { LengthMilliseconds = previous.LengthMilliseconds - transition.LengthMilliseconds });
                units.Add(transition);
            }
            else
            {
                units.Add(previous);
            }
        }

        pending = current;
    }

    static PhonemeUnit? TryBuildTransition(
        VoiceBank bank,
        PhonemeUnit current,
        PhonemeUnit next,
        string? color,
        PhonemizeOptions options)
    {
        if (current.IsSilent || next.IsSilent)
            return null;
        if (next.Alias.Contains(KanaRomanization.AliasSeparator))
            return null;

        Span<char> vowelBuffer = stackalloc char[KanaRomanization.StackTextLength];
        Span<char> consonantBuffer = stackalloc char[KanaRomanization.StackTextLength];
        var vowel = KanaRomanization.GetVowel(current.Note.Lyric, vowelBuffer);
        var consonant = KanaRomanization.GetConsonant(next.Note.Lyric, consonantBuffer);
        if (vowel.IsEmpty || consonant.IsEmpty)
            return null;

        var entry = AliasResolver.ResolveTransition(bank, vowel, consonant, current.Tone, color, current.Note.IgnorePrefixMap, out var alias);
        if (entry is null)
            return null;

        var requested = Math.Max(entry.Preutterance, 0.0) + options.TransitionTailMilliseconds;
        var length = Math.Min(Math.Max(requested, PhonemizeOptions.MinimumTransitionMilliseconds), current.LengthMilliseconds * 0.5);
        if (length < PhonemizeOptions.MinimumTransitionMilliseconds)
            return null;

        return new PhonemeUnit(
            current.Note,
            entry,
            alias,
            current.NoteStartMilliseconds,
            current.NoteLengthMilliseconds,
            current.EndMilliseconds - length,
            length,
            current.Tone);
    }

    static void AppendEnding(VoiceBank bank, List<PhonemeUnit> units, string? color, PhonemizeOptions options)
    {
        if (units.Count == 0 || units[^1].IsSilent)
            return;

        var last = units[^1];

        Span<char> vowelBuffer = stackalloc char[KanaRomanization.StackTextLength];
        var vowel = KanaRomanization.GetVowel(last.Note.Lyric, vowelBuffer);
        if (vowel.IsEmpty)
            return;

        var entry = AliasResolver.ResolveTransition(bank, vowel, KanaRomanization.SilenceConsonant, last.Tone, color, last.Note.IgnorePrefixMap, out var alias);
        if (entry is null)
            return;

        units.Add(new PhonemeUnit(
            last.Note,
            entry,
            alias,
            last.NoteStartMilliseconds,
            last.NoteLengthMilliseconds,
            last.EndMilliseconds,
            options.EndingMilliseconds,
            last.Tone));
    }
}
