namespace UTAU.Synthesis;

internal readonly record struct RenderSettings(
    F0Estimator Estimator,
    StretchMode StretchMode,
    double Volume,
    double FormantSemitones,
    double Breathiness,
    double Brightness)
{
    public const double AnalysisMarginMilliseconds = 50.0;
    public const double MinimumUnitLengthMilliseconds = 10.0;
    public const double MinimumFormant = -12.0;
    public const double MaximumFormant = 12.0;
    public const double MinimumBreathiness = -100.0;
    public const double MaximumBreathiness = 100.0;

    public static RenderSettings Default => new(F0Estimator.Harvest, StretchMode.Loop, 100.0, 0.0, 0.0, 0.0);

    public double Gain => Math.Clamp(Volume, 0.0, 200.0) / 100.0;

    public double FormantRatio => SpectrumTransform.FormantRatioFromSemitones(Math.Clamp(FormantSemitones, MinimumFormant, MaximumFormant));
}
