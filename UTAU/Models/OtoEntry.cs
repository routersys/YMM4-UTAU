using System.IO;

namespace UTAU.Models;

internal sealed record OtoEntry(
    string DirectoryPath,
    string SampleFileName,
    string Alias,
    double Offset,
    double Consonant,
    double Cutoff,
    double Preutterance,
    double Overlap)
{
    public string SamplePath => Path.Combine(DirectoryPath, SampleFileName);

    public double GetEndMilliseconds(double sampleDurationMilliseconds)
        => Cutoff < 0 ? Offset - Cutoff : sampleDurationMilliseconds - Cutoff;

    public double GetLengthMilliseconds(double sampleDurationMilliseconds)
        => Math.Max(GetEndMilliseconds(sampleDurationMilliseconds) - Offset, 0.0);
}
