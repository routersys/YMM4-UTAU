using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;
using UTAU.Synthesis;
using WorldNet;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Voice;

namespace UTAU;

internal sealed class UTAUVoiceSpeaker(VoiceBank bank) : IVoiceSpeaker
{
    const int MaximumReportedUnresolvedLyrics = 8;

    static readonly SemaphoreSlim Semaphore = new(1, 1);

    readonly UTAUVoiceLicense license = new(bank);

    public VoiceBank Bank => bank;

    public string EngineName => UTAUVoicePlugin.EngineName;

    public string SpeakerName => bank.Name;

    public string API => UTAUVoicePlugin.ApiName;

    public string ID => bank.Id;

    public bool IsVoiceDataCachingRequired => true;

    public SupportedTextFormat Format => SupportedTextFormat.Custom;

    public IVoiceLicense? License => license;

    public IVoiceResource? Resource => null;

    public string? SpeakerAuthor => bank.Character.Author;

    public IReadOnlyList<string> Colors => bank.Colors;

    public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter voiceParameter)
        => Task.FromResult(LyricNormalizer.Normalize(text));

    public IVoiceParameter CreateVoiceParameter() => new UTAUVoiceParameter();

    public bool IsMatch(string api, string id) => api == API && id == ID;

    public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
        => currentParameter is UTAUVoiceParameter ? currentParameter : CreateVoiceParameter();

    public async Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoicePronounce? pronounce, IVoiceParameter? parameter, string filePath)
    {
        var normalized = LyricNormalizer.Normalize(text);
        if (normalized.Length == 0)
            throw new InvalidOperationException(Texts.EmptyTextMessage);

        var param = parameter as UTAUVoiceParameter ?? new UTAUVoiceParameter();
        var result = pronounce as UTAUVoicePronounce;
        if (result is null || result.SourceText != normalized || result.Notes.Count == 0)
            result = UTAUVoicePronounce.FromText(normalized, param);

        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() => Render(normalized, result, param, filePath)).ConfigureAwait(false);
        }
        finally
        {
            Semaphore.Release();
        }

        return result;
    }

    void Render(string normalized, UTAUVoicePronounce pronounce, UTAUVoiceParameter parameter, string filePath)
    {
        var notes = pronounce.Notes.ToArray();
        var units = Phonemizer.Phonemize(bank, notes, parameter.Color, PhonemizeOptions.Create(parameter.Speed));
        ThrowIfUnresolved(units);

        var settings = new RenderSettings(
            UTAUSettings.Default.F0Estimator,
            UTAUSettings.Default.StretchMode,
            parameter.Volume,
            parameter.Formant,
            parameter.Breathiness,
            parameter.Brightness);

        using var arena = new WorldArena();
        var renderer = new UtauRenderer(settings, AnalysisCache.Shared);
        var result = renderer.Render(units, arena);
        if (result.Samples.Length == 0)
            throw new InvalidOperationException(Texts.NoRenderableNoteMessage);

        WaveIo.Write(filePath, result.Samples, result.SampleRate);
        pronounce.SourceText = normalized;
        pronounce.LipSyncFrames = BuildLipSyncFrames(notes, result.OffsetMilliseconds);
    }

    static void ThrowIfUnresolved(IReadOnlyList<PhonemeUnit> units)
    {
        var unresolved = units
            .Where(x => x.IsUnresolved)
            .Select(x => x.Note.Lyric)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unresolved.Length == 0)
            return;

        var listed = string.Join(", ", unresolved.Take(MaximumReportedUnresolvedLyrics));
        if (unresolved.Length > MaximumReportedUnresolvedLyrics)
            listed += " ...";
        throw new InvalidOperationException(string.Format(Texts.AliasNotFoundMessage, listed));
    }

    static LipSyncFrame[] BuildLipSyncFrames(IReadOnlyList<UTAUNote> notes, double offsetMilliseconds)
    {
        var frames = new List<LabFrame>(notes.Count);
        var position = 0.0;

        foreach (var note in notes)
        {
            var start = position - offsetMilliseconds;
            position += note.LengthMilliseconds;
            var end = position - offsetMilliseconds;
            if (end <= 0.0)
                continue;

            frames.Add(new LabFrame(
                TimeSpan.FromMilliseconds(Math.Max(start, 0.0)),
                TimeSpan.FromMilliseconds(end),
                GetLabel(note)));
        }

        return LipSyncFrame.FromLabFrames(frames);
    }

    static string GetLabel(UTAUNote note)
    {
        if (note.IsRest)
            return "pau";

        var vowel = KanaRomanization.GetVowel(note.Lyric);
        return vowel switch
        {
            null => "pau",
            "n" => "N",
            _ => vowel,
        };
    }
}
