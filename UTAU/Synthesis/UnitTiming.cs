using UTAU.Notes;
using UTAU.Phonemes;

namespace UTAU.Synthesis;

internal readonly record struct UnitTiming(
    PhonemeUnit Unit,
    double AudioStartMilliseconds,
    double RenderLengthMilliseconds,
    double FadeInMilliseconds,
    double FadeOutMilliseconds,
    double SkipMilliseconds)
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
    const double AdjacentHeadShare = 0.5;
    const double PlosiveHeadShare = 0.9;

    public static IReadOnlyList<UnitTiming> Build(IReadOnlyList<PhonemeUnit> units)
    {
        var timings = new List<UnitTiming>(units.Count);
        var preutterances = new double[units.Count];
        var overlaps = new double[units.Count];
        var adjacent = new bool[units.Count];
        var skips = new double[units.Count];

        for (var i = 0; i < units.Count; i++)
        {
            var consonantScale = TimeMap.VelocityToConsonantScale(units[i].Note.Velocity);
            var preutterance = units[i].HasPreutteranceOverride ? units[i].Preutterance : units[i].Preutterance * consonantScale;
            var overlap = units[i].HasOverlapOverride ? units[i].Overlap : units[i].Overlap * consonantScale;
            var requested = preutterance;
            var preceding = FindPrecedingVoiced(units, i);

            if (preceding >= 0)
            {
                var precedingLength = units[preceding].LengthMilliseconds;
                var gap = units[i].StartMilliseconds - units[preceding].EndMilliseconds;
                var limit = preutterance;
                adjacent[i] = gap <= 0.0;

                if (adjacent[i])
                {
                    var head = preutterance - overlap;
                    if (overlap > 0.0)
                    {
                        if (head > precedingLength * AdjacentHeadShare)
                            limit = precedingLength * AdjacentHeadShare / head * preutterance;
                    }
                    else
                        limit = Math.Min(limit, precedingLength * PlosiveHeadShare);

                    limit = Math.Min(limit, precedingLength);
                    if (preutterances[preceding] < UTAUNote.DefaultFadeInMilliseconds)
                        limit = Math.Min(limit, precedingLength + preutterances[preceding] - UTAUNote.DefaultFadeInMilliseconds);
                }
                else if (gap < preutterance)
                    limit = gap;

                if (preutterance > limit)
                {
                    overlap *= preutterance > 0.0 ? limit / preutterance : 0.0;
                    preutterance = limit;
                }

                if (overlap < 0.0)
                    overlap = Math.Max(overlap, Math.Min(0.0, UTAUNote.DefaultFadeOutMilliseconds - precedingLength + preutterance));
            }

            preutterances[i] = Math.Max(preutterance, 0.0);
            overlaps[i] = overlap;
            skips[i] = Math.Max(requested - preutterances[i], 0.0);
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

            var fadeIn = adjacent[i] && overlaps[i] > 0.0 ? overlaps[i] : unit.Note.FadeInMilliseconds;
            var fadeOut = next >= 0 && adjacent[next] && overlaps[next] > 0.0 ? overlaps[next] : unit.Note.FadeOutMilliseconds;
            (fadeIn, fadeOut) = LimitFades(fadeIn, fadeOut, renderLength);

            timings.Add(new UnitTiming(unit, audioStart, renderLength, fadeIn, fadeOut, skips[i]));
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

    static int FindPrecedingVoiced(IReadOnlyList<PhonemeUnit> units, int index)
    {
        for (var preceding = index - 1; preceding >= 0; preceding--)
        {
            if (!units[preceding].IsSilent)
                return preceding;
        }
        return -1;
    }
}
