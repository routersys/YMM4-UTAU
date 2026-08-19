using System.Globalization;
using UTAU.Models;

namespace UTAU.Notes;

internal sealed record UstImportResult(
    IReadOnlyList<UTAUNote> Notes,
    double Tempo,
    int LegacyPitchNoteCount,
    int TrimmedRestTicks);

internal static class UstImporter
{
    public const double MaximumDeclaredTempo = 1000.0;
    public const double CentsPerPitchUnit = 10.0;
    public const int EnvelopeFieldCount = 7;
    const double TempoEpsilon = 1e-9;

    public static UstImportResult Import(UstDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tempo = ResolveTempo(document);
        var currentTempo = tempo;
        var notes = new List<UTAUNote>();
        var legacyPitchNoteCount = 0;
        var position = 0;
        var end = 0;

        foreach (var section in document.NoteSections)
        {
            var length = ParseInteger(section.Find(UstKeys.Length));
            var duration = ParseInteger(section.Find(UstKeys.Duration));
            var delta = ParseInteger(section.Find(UstKeys.Delta));

            var noteLength = duration ?? length ?? 0;
            if (noteLength <= 0)
                continue;

            var start = delta is not null && duration is not null && length is not null
                ? Math.Max(position + delta.Value, end)
                : end;

            var previousTone = notes.Count == 0 ? MusicalTone.MiddleC.NoteNumber : notes[^1].Tone;
            AddRest(notes, start - end, previousTone);

            var sectionTempo = ParseNumber(section.Find(UstKeys.Tempo));
            var isTempoChange = IsUsableTempo(sectionTempo)
                && Math.Abs(ClampTempo(sectionTempo!.Value) - currentTempo) > TempoEpsilon;
            if (isTempoChange)
                currentTempo = ClampTempo(sectionTempo!.Value);

            var note = CreateNote(section, noteLength, new TimeBase(currentTempo, 1.0));
            if (isTempoChange)
                note.TempoOverride = currentTempo;
            if (note.PitchPoints.Count == 0 && HasLegacyPitch(section))
                legacyPitchNoteCount++;
            notes.Add(note);

            position = start;
            end = start + noteLength;
        }

        var trimmedRestTicks = TrimSurroundingRests(notes);
        return new UstImportResult(notes, tempo, legacyPitchNoteCount, trimmedRestTicks);
    }

    static UTAUNote CreateNote(UstSection section, int lengthTicks, TimeBase timeBase)
    {
        var note = new UTAUNote
        {
            Lyric = NormalizeLyric(section.Find(UstKeys.Lyric)),
            LengthTicks = lengthTicks,
            Tone = ParseInteger(section.Find(UstKeys.NoteNum)) ?? MusicalTone.MiddleC.NoteNumber,
        };

        if (ParseNumber(section.Find(UstKeys.Velocity)) is { } velocity)
            note.Velocity = velocity;
        if (ParseNumber(section.Find(UstKeys.Intensity)) is { } intensity)
            note.Intensity = intensity;
        if (ParseNumber(section.Find(UstKeys.Modulation) ?? section.Find(UstKeys.Moduration)) is { } modulation)
            note.Modulation = modulation;
        if (ParseNumber(section.Find(UstKeys.PreUtterance)) is { } preutterance)
            note.PreutteranceOverride = preutterance;
        if (ParseNumber(section.Find(UstKeys.VoiceOverlap)) is { } overlap)
            note.OverlapOverride = overlap;
        if (ParseNumber(section.Find(UstKeys.StartPoint)) is { } startPoint)
            note.StartPointMilliseconds = startPoint;

        ApplyEnvelope(note, section.Find(UstKeys.Envelope));
        ApplyVibrato(note, section.Find(UstKeys.Vibrato));
        ApplyPitchBend(note, section, timeBase);
        return note;
    }

    static void ApplyEnvelope(UTAUNote note, string? text)
    {
        var fields = ParseNumbers(text);
        if (fields.Length < EnvelopeFieldCount)
            return;

        note.FadeInMilliseconds = fields[0] + fields[1];
        note.FadeOutMilliseconds = fields[2];
    }

    static void ApplyVibrato(UTAUNote note, string? text)
    {
        var fields = ParseNumbers(text);
        if (fields.Length == 0 || fields[0] <= 0.0)
            return;

        note.Vibrato.LengthPercent = fields[0];
        if (fields.Length > 1)
            note.Vibrato.PeriodMilliseconds = fields[1];
        if (fields.Length > 2)
            note.Vibrato.DepthCents = fields[2];
        if (fields.Length > 3)
            note.Vibrato.FadeInPercent = fields[3];
        if (fields.Length > 4)
            note.Vibrato.FadeOutPercent = fields[4];
        if (fields.Length > 5)
            note.Vibrato.PhasePercent = fields[5];
        if (fields.Length > 6)
            note.Vibrato.OffsetPercent = fields[6];
    }

    static void ApplyPitchBend(UTAUNote note, UstSection section, TimeBase timeBase)
    {
        var widths = ParseNumbers(section.Find(UstKeys.PitchBendWidth));
        if (widths.Length == 0)
            return;

        var (startMilliseconds, startCents) = ParsePitchBendStart(section.Find(UstKeys.PitchBendStart));
        var offsets = ParseNumbers(section.Find(UstKeys.PitchBendY));
        var shapes = ParseShapes(section.Find(UstKeys.PitchBendMode));

        var milliseconds = startMilliseconds;
        note.PitchPoints.Add(new PitchPoint(timeBase.ToTicks(milliseconds), startCents, ShapeAt(shapes, 0)));
        for (var i = 0; i < widths.Length; i++)
        {
            milliseconds += Math.Max(widths[i], 0.0);
            var cents = (i < offsets.Length ? offsets[i] : 0.0) * CentsPerPitchUnit;
            note.PitchPoints.Add(new PitchPoint(timeBase.ToTicks(milliseconds), cents, ShapeAt(shapes, i + 1)));
        }
    }

    static bool HasLegacyPitch(UstSection section)
        => section.Find(UstKeys.LegacyPitchBend) is not null
            || section.Find(UstKeys.LegacyPitches) is not null
            || section.Find(UstKeys.LegacyPitchesTypo) is not null;

    static void AddRest(List<UTAUNote> notes, int lengthTicks, int tone)
    {
        while (lengthTicks >= UTAUNote.MinimumLengthTicks)
        {
            var chunk = Math.Min(lengthTicks, UTAUNote.MaximumLengthTicks);
            notes.Add(new UTAUNote
            {
                Lyric = UTAUNote.RestLyric,
                Tone = tone,
                LengthTicks = chunk,
            });
            lengthTicks -= chunk;
        }
    }

    static int TrimSurroundingRests(List<UTAUNote> notes)
    {
        var trimmed = 0;
        var leading = 0;
        while (leading < notes.Count && notes[leading].IsRest)
        {
            trimmed += notes[leading].LengthTicks;
            leading++;
        }
        notes.RemoveRange(0, leading);

        while (notes.Count > 0 && notes[^1].IsRest)
        {
            trimmed += notes[^1].LengthTicks;
            notes.RemoveAt(notes.Count - 1);
        }
        return trimmed;
    }

    static double ResolveTempo(UstDocument document)
    {
        var declared = ParseNumber(document.Setting?.Find(UstKeys.Tempo));
        if (IsUsableTempo(declared))
            return ClampTempo(declared!.Value);

        foreach (var section in document.NoteSections)
        {
            var noteTempo = ParseNumber(section.Find(UstKeys.Tempo));
            if (IsUsableTempo(noteTempo))
                return ClampTempo(noteTempo!.Value);
        }

        return TimeBase.DefaultTempo;
    }

    static double ClampTempo(double tempo)
        => Math.Clamp(tempo, TimeBase.MinimumTempo, TimeBase.MaximumTempo);

    static bool IsUsableTempo(double? tempo) => tempo is > 0.0 and <= MaximumDeclaredTempo;

    static string NormalizeLyric(string? text)
    {
        var lyric = (text ?? string.Empty).Trim();
        while (lyric.Length > 0 && lyric[0] is UstKeys.IgnorePrefixMapMarker or UstKeys.SuppressAutoVcvMarker)
            lyric = lyric[1..].Trim();

        return lyric.Equals(UstKeys.RestLyric, StringComparison.OrdinalIgnoreCase) ? UTAUNote.RestLyric : lyric;
    }

    static (double Milliseconds, double Cents) ParsePitchBendStart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (0.0, 0.0);

        var separator = text.IndexOf(UstKeys.PitchBendStartSeparator);
        var fields = separator >= 0
            ? new[] { text[..separator], text[(separator + 1)..] }
            : text.Split(UstKeys.FieldSeparator);

        var milliseconds = ParseNumber(fields[0]) ?? 0.0;
        var cents = (fields.Length > 1 ? ParseNumber(fields[1]) ?? 0.0 : 0.0) * CentsPerPitchUnit;
        return (milliseconds, cents);
    }

    static PitchPointShape ShapeAt(PitchPointShape[] shapes, int index)
        => index < shapes.Length ? shapes[index] : PitchPointShape.SCurve;

    static PitchPointShape[] ParseShapes(string? text)
    {
        if (text is null)
            return [];

        var fields = text.Split(UstKeys.FieldSeparator);
        var shapes = new PitchPointShape[fields.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            shapes[i] = fields[i].Trim().ToLowerInvariant() switch
            {
                UstKeys.ShapeLinear => PitchPointShape.Linear,
                UstKeys.ShapeRCurve => PitchPointShape.RCurve,
                UstKeys.ShapeJCurve => PitchPointShape.JCurve,
                _ => PitchPointShape.SCurve,
            };
        }
        return shapes;
    }

    static double[] ParseNumbers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var fields = text.Split(UstKeys.FieldSeparator);
        var values = new double[fields.Length];
        for (var i = 0; i < fields.Length; i++)
            values[i] = ParseNumber(fields[i]) ?? 0.0;
        return values;
    }

    static int? ParseInteger(string? text)
    {
        if (ParseNumber(text) is not { } value)
            return null;

        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(rounded, int.MinValue, int.MaxValue);
    }

    static double? ParseNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value)
            ? value
            : null;
    }
}
