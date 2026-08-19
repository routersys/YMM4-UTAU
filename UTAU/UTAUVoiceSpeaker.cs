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
        => Task.FromResult(UstSource.TryGetPath(text, out var path) ? path : LyricNormalizer.Normalize(text));

    public IVoiceParameter CreateVoiceParameter() => new UTAUVoiceParameter();

    public bool IsMatch(string api, string id) => api == API && id == ID;

    public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
        => currentParameter is UTAUVoiceParameter ? currentParameter : CreateVoiceParameter();

    public async Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoicePronounce? pronounce, IVoiceParameter? parameter, string filePath)
    {
        var param = parameter as UTAUVoiceParameter ?? new UTAUVoiceParameter();
        var isUst = UstSource.TryGetPath(text, out var ustPath);
        var source = isUst ? ustPath : LyricNormalizer.Normalize(text);
        if (source.Length == 0)
            throw new InvalidOperationException(Texts.EmptyTextMessage);

        var result = pronounce as UTAUVoicePronounce;
        if (result is null || result.SourceText != source || result.Notes.Count == 0)
            result = isUst ? UTAUVoicePronounce.FromUst(ustPath, param) : UTAUVoicePronounce.FromText(source, param);

        var timeBase = isUst ? new TimeBase(result.Tempo, param.Speed) : new TimeBase(param.Tempo, param.Speed);
        var tempoMap = TempoMap.Create(result.Notes, timeBase);

        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() => Render(source, result, param, tempoMap, filePath)).ConfigureAwait(false);
        }
        finally
        {
            Semaphore.Release();
        }

        return result;
    }

    void Render(string source, UTAUVoicePronounce pronounce, UTAUVoiceParameter parameter, TempoMap tempoMap, string filePath)
    {
        var notes = pronounce.Notes.ToArray();
        var units = Phonemizer.Phonemize(bank, notes, parameter.Color, PhonemizeOptions.Default, tempoMap);
        ThrowIfUnresolved(units);

        var settings = new RenderSettings(
            UTAUSettings.Default.F0Estimator,
            UTAUSettings.Default.StretchMode,
            parameter.Volume,
            parameter.Formant,
            parameter.Breathiness,
            parameter.Brightness);

        using var arena = new WorldArena();
        var renderer = new UtauRenderer(settings, BuildCurves(pronounce, tempoMap), AnalysisCache.Shared);
        var result = renderer.Render(units, arena);
        if (result.Samples.Length == 0)
            throw new InvalidOperationException(Texts.NoRenderableNoteMessage);

        WaveIo.Write(filePath, result.Samples, result.SampleRate);
        pronounce.SourceText = source;
        pronounce.LipSyncFrames = BuildLipSyncFrames(notes, tempoMap, result.OffsetMilliseconds);
    }

    static RenderCurves BuildCurves(UTAUVoicePronounce pronounce, TempoMap tempoMap)
    {
        if (pronounce.FormantCurve.IsEmpty && pronounce.BreathinessCurve.IsEmpty)
            return RenderCurves.Empty;

        var resampled = ExpressionCurveResampler.Resample(pronounce.FormantCurve, pronounce.BreathinessCurve, tempoMap);
        return new RenderCurves(resampled.Formant, resampled.Breathiness, resampled.IntervalMilliseconds);
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

    static LipSyncFrame[] BuildLipSyncFrames(IReadOnlyList<UTAUNote> notes, TempoMap tempoMap, double offsetMilliseconds)
    {
        var frames = new List<LabFrame>(notes.Count);

        for (var index = 0; index < notes.Count; index++)
        {
            var start = tempoMap.StartMilliseconds(index) - offsetMilliseconds;
            var end = start + tempoMap.LengthMilliseconds(index);
            if (end <= 0.0)
                continue;

            frames.Add(new LabFrame(
                TimeSpan.FromMilliseconds(Math.Max(start, 0.0)),
                TimeSpan.FromMilliseconds(end),
                GetLabel(notes[index])));
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
