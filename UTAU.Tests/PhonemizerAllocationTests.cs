using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;

namespace UTAU.Tests;

public sealed class PhonemizerAllocationTests : IDisposable
{
    const int NoteCount = 5000;

    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    static UTAUNote[] Notes()
    {
        var notes = new UTAUNote[NoteCount];
        for (var index = 0; index < notes.Length; index++)
            notes[index] = new UTAUNote { Lyric = index % 2 == 0 ? "あ" : "か", LengthTicks = 240, Tone = 60 };
        return notes;
    }

    static long Measure(Action action)
    {
        for (var warm = 0; warm < 3; warm++)
            action();

        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void NormalizingRegularLyricsAllocatesNothing()
    {
        string[] lyrics = ["あ", "か", "a か", "- あ", "a k", "* か", "ka"];

        var allocated = Measure(() =>
        {
            foreach (var lyric in lyrics)
                for (var pass = 0; pass < 1000; pass++)
                    AliasNormalizer.Normalize(lyric);
        });

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void LookingUpAnAliasAllocatesNothing()
    {
        var bank = TestVoiceBank.CreateVcvAndCvvcBank(directory);
        var notes = Notes();

        var allocated = Measure(() =>
        {
            for (var i = 0; i < notes.Length; i++)
                bank.Resolve(notes[i].Lyric, 60, null);
        });

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void DecomposingAMoraAllocatesNothing()
    {
        var notes = Notes();

        var allocated = Measure(() =>
        {
            Span<char> buffer = stackalloc char[KanaRomanization.StackTextLength];
            for (var i = 0; i < notes.Length; i++)
            {
                _ = KanaRomanization.GetVowel(notes[i].Lyric, buffer);
                _ = KanaRomanization.GetConsonant(notes[i].Lyric, buffer);
            }
        });

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ResolvingAllocatesOnlyTheChosenAlias()
    {
        var bank = TestVoiceBank.CreateVcvAndCvvcBank(directory);
        var notes = Notes();

        var perNote = Measure(() =>
        {
            for (var i = 0; i < notes.Length; i++)
                AliasResolver.Resolve(bank, notes[i].Lyric, "a", 60, null, false, out _);
        }) / (double)NoteCount;

        Assert.True(perNote < 48.0, $"perNote={perNote:F0}B");
    }

    [Fact]
    public void PhonemizingALongScoreStaysWithinBudget()
    {
        var bank = TestVoiceBank.CreateVcvAndCvvcBank(directory);
        var tempoMap = TempoMap.Create(Notes(), TimeBase.Default);

        var perNote = Measure(() => Phonemizer.Phonemize(bank, tempoMap, null, PhonemizeOptions.Default)) / (double)NoteCount;

        Assert.True(perNote < 160.0, $"perNote={perNote:F0}B");
    }

    [Fact]
    public void ATransitionHeavyScoreStaysWithinBudget()
    {
        var bank = TestVoiceBank.CreateCvvcBank(directory);
        var tempoMap = TempoMap.Create(Notes(), TimeBase.Default);
        var units = Phonemizer.Phonemize(bank, tempoMap, null, PhonemizeOptions.Default);

        var perNote = Measure(() => Phonemizer.Phonemize(bank, tempoMap, null, PhonemizeOptions.Default)) / (double)NoteCount;

        Assert.Equal(NoteCount + NoteCount / 2 + 1, units.Count);
        Assert.True(perNote < 260.0, $"perNote={perNote:F0}B");
    }

    [Fact]
    public void PhonemizingScalesLinearlyWithTheNoteCount()
    {
        var bank = TestVoiceBank.CreateCvvcBank(directory);
        var small = new UTAUNote[1000];
        var large = new UTAUNote[8000];
        for (var index = 0; index < large.Length; index++)
        {
            var note = new UTAUNote { Lyric = index % 2 == 0 ? "あ" : "か", LengthTicks = 240, Tone = 60 };
            large[index] = note;
            if (index < small.Length)
                small[index] = new UTAUNote { Lyric = note.Lyric, LengthTicks = 240, Tone = 60 };
        }

        var smallMap = TempoMap.Create(small, TimeBase.Default);
        var largeMap = TempoMap.Create(large, TimeBase.Default);

        var smallPerNote = Measure(() => Phonemizer.Phonemize(bank, smallMap, null, PhonemizeOptions.Default)) / (double)small.Length;
        var largePerNote = Measure(() => Phonemizer.Phonemize(bank, largeMap, null, PhonemizeOptions.Default)) / (double)large.Length;

        Assert.True(largePerNote < smallPerNote * 1.5, $"small={smallPerNote:F0}B large={largePerNote:F0}B");
    }
}
