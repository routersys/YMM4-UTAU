namespace UTAU.Synthesis;

internal readonly record struct TimeMap(
    double RegionStart,
    double ConsonantEnd,
    double RegionEnd,
    double ConsonantOutputLength,
    double SustainOutputLength,
    double ConsonantScale,
    StretchMode StretchMode)
{
    public static double VelocityToConsonantScale(double velocity)
        => Math.Pow(2.0, (100.0 - Math.Clamp(velocity, 0.0, 200.0)) / 100.0);

    public static TimeMap Create(
        double regionStart,
        double consonantEnd,
        double regionEnd,
        double outputLength,
        double velocity,
        StretchMode stretchMode)
    {
        var scale = VelocityToConsonantScale(velocity);
        var consonantSourceLength = Math.Max(consonantEnd - regionStart, 0.0);
        var consonantOutputLength = Math.Min(consonantSourceLength * scale, Math.Max(outputLength, 0.0));
        if (consonantSourceLength > 0.0 && consonantOutputLength > 0.0)
            scale = consonantOutputLength / consonantSourceLength;

        return new TimeMap(
            regionStart,
            consonantEnd,
            regionEnd,
            consonantOutputLength,
            Math.Max(outputLength - consonantOutputLength, 0.0),
            scale,
            stretchMode);
    }

    public double Map(double outputMilliseconds)
    {
        if (outputMilliseconds <= 0.0)
            return RegionStart;

        if (outputMilliseconds < ConsonantOutputLength)
            return ConsonantScale > 0.0
                ? RegionStart + outputMilliseconds / ConsonantScale
                : ConsonantEnd;

        var sustainSourceLength = Math.Max(RegionEnd - ConsonantEnd, 0.0);
        if (sustainSourceLength <= 0.0)
            return RegionEnd;

        var elapsed = outputMilliseconds - ConsonantOutputLength;
        if (StretchMode == StretchMode.Stretch)
        {
            var progress = SustainOutputLength > 0.0 ? Math.Clamp(elapsed / SustainOutputLength, 0.0, 1.0) : 0.0;
            return ConsonantEnd + sustainSourceLength * progress;
        }

        var period = sustainSourceLength * 2.0;
        var phase = elapsed % period;
        if (phase < 0.0)
            phase += period;
        return ConsonantEnd + (phase <= sustainSourceLength ? phase : period - phase);
    }
}
