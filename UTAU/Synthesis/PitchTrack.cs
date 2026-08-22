using UTAU.Notes;

namespace UTAU.Synthesis;

internal sealed class PitchTrack
{
    readonly record struct TrackNote(UTAUNote Note, double StartMilliseconds, double LengthMilliseconds, int Tone, double AudioStartMilliseconds)
    {
        public double EndMilliseconds => StartMilliseconds + LengthMilliseconds;

        public double Cents => Tone * 100.0;
    }

    readonly double[] cents;

    PitchTrack(double[] cents) => this.cents = cents;

    public static PitchTrack Create(IReadOnlyList<UnitTiming> timings, double startMilliseconds, double framePeriod, int frameCount)
    {
        ArgumentNullException.ThrowIfNull(timings);

        var cents = new double[Math.Max(frameCount, 0)];
        var notes = CollectNotes(timings);
        if (cents.Length == 0 || notes.Count == 0)
            return new PitchTrack(cents);

        FillTones(cents, notes, startMilliseconds, framePeriod);
        ApplyVibrato(cents, notes, startMilliseconds, framePeriod);
        ApplyPortamento(cents, notes, startMilliseconds, framePeriod);
        return new PitchTrack(cents);
    }

    public double CentsAt(int frame)
        => cents.Length == 0 ? 0.0 : cents[Math.Clamp(frame, 0, cents.Length - 1)];

    static List<TrackNote> CollectNotes(IReadOnlyList<UnitTiming> timings)
    {
        var notes = new List<TrackNote>(timings.Count);
        foreach (var timing in timings)
        {
            var unit = timing.Unit;
            if (unit.Note.IsRest)
                continue;
            notes.Add(new TrackNote(unit.Note, unit.NoteStartMilliseconds, unit.NoteLengthMilliseconds, unit.Tone, timing.AudioStartMilliseconds));
        }

        notes.Sort((left, right) => left.StartMilliseconds.CompareTo(right.StartMilliseconds));

        var distinct = new List<TrackNote>(notes.Count);
        foreach (var note in notes)
        {
            if (distinct.Count > 0 && distinct[^1].StartMilliseconds == note.StartMilliseconds && ReferenceEquals(distinct[^1].Note, note.Note))
            {
                distinct[^1] = distinct[^1] with
                {
                    AudioStartMilliseconds = Math.Min(distinct[^1].AudioStartMilliseconds, note.AudioStartMilliseconds),
                };
                continue;
            }
            distinct.Add(note);
        }
        return distinct;
    }

    static void FillTones(double[] cents, List<TrackNote> notes, double startMilliseconds, double framePeriod)
    {
        var index = 0;
        foreach (var note in notes)
        {
            while (index < cents.Length && startMilliseconds + index * framePeriod < note.EndMilliseconds)
            {
                cents[index] = note.Cents;
                index++;
            }
        }

        if (index == 0)
        {
            Array.Fill(cents, notes[^1].Cents);
            return;
        }

        for (var i = index; i < cents.Length; i++)
            cents[i] = cents[i - 1];
    }

    static void ApplyVibrato(double[] cents, List<TrackNote> notes, double startMilliseconds, double framePeriod)
    {
        foreach (var note in notes)
        {
            if (!note.Note.Vibrato.IsEnabled)
                continue;

            var from = Math.Max((int)Math.Ceiling((note.StartMilliseconds - startMilliseconds) / framePeriod), 0);
            var to = Math.Min((int)((note.EndMilliseconds - startMilliseconds) / framePeriod), cents.Length);
            for (var i = from; i < to; i++)
                cents[i] = note.Cents + note.Note.Vibrato.Evaluate(startMilliseconds + i * framePeriod - note.StartMilliseconds, note.LengthMilliseconds);
        }
    }

    static void ApplyPortamento(double[] cents, List<TrackNote> notes, double startMilliseconds, double framePeriod)
    {
        for (var n = 0; n < notes.Count; n++)
        {
            var note = notes[n];
            if (note.Note.PitchPoints.Count == 0 || note.Note.LengthTicks <= 0)
                continue;

            var scale = note.LengthMilliseconds / note.Note.LengthTicks;
            var points = new List<(double X, double Y, PitchPointShape Shape)>(note.Note.PitchPoints.Count + 2);
            foreach (var point in note.Note.PitchPoints)
                points.Add((note.StartMilliseconds + point.Ticks * scale, note.Cents + point.Cents, point.Shape));

            var opensPhrase = n == 0 || notes[n - 1].EndMilliseconds < note.StartMilliseconds;
            var lead = opensPhrase
                ? Math.Max(startMilliseconds, note.AudioStartMilliseconds)
                : note.StartMilliseconds;
            if (points[0].X > lead)
                points.Insert(0, (lead, points[0].Y, points[0].Shape));
            if (points[^1].X < note.EndMilliseconds)
                points.Add((note.EndMilliseconds, points[^1].Y, points[^1].Shape));

            for (var s = 0; s < points.Count - 1; s++)
            {
                var from = points[s];
                var to = points[s + 1];
                var first = Math.Max((int)Math.Ceiling((from.X - startMilliseconds) / framePeriod), 0);
                var last = Math.Min((int)Math.Ceiling((to.X - startMilliseconds) / framePeriod), cents.Length);
                var span = to.X - from.X;

                for (var i = first; i < last; i++)
                {
                    var x = startMilliseconds + i * framePeriod;
                    var progress = span <= 0.0 ? 1.0 : (x - from.X) / span;
                    var pitch = PitchPoint.Interpolate(from.Y, to.Y, progress, from.Shape);
                    var basePitch = n > 0 && x < notes[n - 1].EndMilliseconds ? notes[n - 1].Cents : note.Cents;
                    cents[i] += pitch - basePitch;
                }
            }
        }
    }
}
