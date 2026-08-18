using UTAU.Phonemes;

namespace UTAU.Synthesis;

internal readonly record struct UnitTiming(
    PhonemeUnit Unit,
    double AudioStartMilliseconds,
    double RenderLengthMilliseconds,
    double FadeInMilliseconds,
    double FadeOutMilliseconds)
{
    public double AudioEndMilliseconds => AudioStartMilliseconds + RenderLengthMilliseconds;

    public double GetWeight(double millisecondsFromStart)
    {
        if (millisecondsFromStart < 0.0 || millisecondsFromStart > RenderLengthMilliseconds)
            return 0.0;

        var weight = 1.0;
        if (FadeInMilliseconds > 0.0 && millisecondsFromStart < FadeInMilliseconds)
            weight = millisecondsFromStart / FadeInMilliseconds;

        if (FadeOutMilliseconds > 0.0)
        {
            var remaining = RenderLengthMilliseconds - millisecondsFromStart;
            if (remaining < FadeOutMilliseconds)
                weight = Math.Min(weight, remaining / FadeOutMilliseconds);
        }

        return Math.Clamp(weight, 0.0, 1.0);
    }
}

internal static class UnitTimingBuilder
{
    public static IReadOnlyList<UnitTiming> Build(IReadOnlyList<PhonemeUnit> units)
    {
        var timings = new List<UnitTiming>(units.Count);
        var preutterances = new double[units.Count];
        var overlaps = new double[units.Count];

        for (var i = 0; i < units.Count; i++)
        {
            var preutterance = units[i].Preutterance;
            var overlap = units[i].Overlap;
            var head = preutterance - overlap;
            var available = i == 0 ? 0.0 : units[i - 1].LengthMilliseconds;

            if (i > 0 && head > available && head > 0.0)
            {
                var ratio = available / head;
                preutterance *= ratio;
                overlap *= ratio;
            }
            else if (i == 0 && head < 0.0)
            {
                overlap = preutterance;
            }

            preutterances[i] = preutterance;
            overlaps[i] = Math.Max(overlap, 0.0);
        }

        for (var i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            if (unit.IsSilent)
                continue;

            var audioStart = unit.StartMilliseconds - preutterances[i];
            var next = FindNextVoiced(units, i);
            var renderLength = next < 0
                ? unit.EndMilliseconds - audioStart
                : units[next].StartMilliseconds - preutterances[next] + overlaps[next] - audioStart;
            renderLength = Math.Max(renderLength, RenderSettings.MinimumUnitLengthMilliseconds);

            var previous = FindPreviousVoiced(units, i);
            var fadeIn = previous >= 0 && overlaps[i] > 0.0 ? overlaps[i] : unit.Note.FadeInMilliseconds;
            var fadeOut = next >= 0 && overlaps[next] > 0.0 ? overlaps[next] : unit.Note.FadeOutMilliseconds;
            (fadeIn, fadeOut) = LimitFades(fadeIn, fadeOut, renderLength);

            timings.Add(new UnitTiming(unit, audioStart, renderLength, fadeIn, fadeOut));
        }

        return timings;
    }

    static (double FadeIn, double FadeOut) LimitFades(double fadeIn, double fadeOut, double renderLength)
    {
        fadeIn = Math.Max(fadeIn, 0.0);
        fadeOut = Math.Max(fadeOut, 0.0);
        var total = fadeIn + fadeOut;
        if (total <= renderLength || total <= 0.0)
            return (fadeIn, fadeOut);

        var ratio = renderLength / total;
        return (fadeIn * ratio, fadeOut * ratio);
    }

    static int FindNextVoiced(IReadOnlyList<PhonemeUnit> units, int index)
    {
        var next = index + 1;
        return next < units.Count && !units[next].IsSilent ? next : -1;
    }

    static int FindPreviousVoiced(IReadOnlyList<PhonemeUnit> units, int index)
    {
        var previous = index - 1;
        return previous >= 0 && !units[previous].IsSilent ? previous : -1;
    }
}
