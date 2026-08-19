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
        IReadOnlyList<UTAUNote> notes,
        string? color,
        PhonemizeOptions options,
        TempoMap tempoMap)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(tempoMap);

        var units = new List<PhonemeUnit>();
        var previousVowel = KanaRomanization.StartVowel;

        for (var i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            var start = tempoMap.StartMilliseconds(i);
            var length = tempoMap.LengthMilliseconds(i);

            if (note.IsRest)
            {
                units.Add(new PhonemeUnit(note, null, UTAUNote.RestLyric, start, length, start, length, note.Tone));
                previousVowel = KanaRomanization.StartVowel;
                continue;
            }

            var entry = AliasResolver.Resolve(bank, note.Lyric, previousVowel, note.Tone, color, out var alias);
            units.Add(new PhonemeUnit(note, entry, alias, start, length, start, length, note.Tone));

            var vowel = KanaRomanization.GetVowel(note.Lyric);
            previousVowel = entry is null || vowel is null ? KanaRomanization.StartVowel : vowel;
        }

        InsertTransitions(bank, units, color, options);
        AppendEnding(bank, units, color, options);
        return units;
    }

    static void InsertTransitions(
        VoiceBank bank,
        List<PhonemeUnit> units,
        string? color,
        PhonemizeOptions options)
    {
        for (var i = units.Count - 2; i >= 0; i--)
        {
            var current = units[i];
            var next = units[i + 1];
            if (current.IsSilent || next.IsSilent)
                continue;

            var vowel = KanaRomanization.GetVowel(current.Note.Lyric);
            var consonant = KanaRomanization.GetConsonant(next.Note.Lyric);
            if (vowel is null || consonant is null)
                continue;

            var entry = AliasResolver.ResolveTransition(bank, vowel, consonant, current.Tone, color, out var alias);
            if (entry is null)
                continue;

            var requested = Math.Max(entry.Preutterance, 0.0) + options.TransitionTailMilliseconds;
            var length = Math.Min(Math.Max(requested, PhonemizeOptions.MinimumTransitionMilliseconds), current.LengthMilliseconds * 0.5);
            if (length < PhonemizeOptions.MinimumTransitionMilliseconds)
                continue;

            units[i] = current with { LengthMilliseconds = current.LengthMilliseconds - length };
            units.Insert(i + 1, new PhonemeUnit(
                current.Note,
                entry,
                alias,
                current.NoteStartMilliseconds,
                current.NoteLengthMilliseconds,
                current.EndMilliseconds - length,
                length,
                current.Tone));
        }
    }

    static void AppendEnding(VoiceBank bank, List<PhonemeUnit> units, string? color, PhonemizeOptions options)
    {
        var last = units.LastOrDefault(x => !x.IsSilent);
        if (last is null || units[^1].IsSilent)
            return;

        var vowel = KanaRomanization.GetVowel(last.Note.Lyric);
        if (vowel is null)
            return;

        var entry = AliasResolver.ResolveTransition(bank, vowel, KanaRomanization.SilenceConsonant, last.Tone, color, out var alias);
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
