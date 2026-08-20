namespace UTAU.Phonemes;

internal readonly ref struct AliasWorkspace
{
    const int Margin = 8;
    const int Units = AliasCandidates.FormCount + 3 + (AliasCandidates.StepCount * 2);

    public readonly Span<char> Pool;
    public readonly Span<char> Scratch;
    public readonly Span<char> Source;
    public readonly Span<char> Emitted;

    public AliasWorkspace(Span<char> buffer, int lyricLength)
    {
        var unit = lyricLength + Margin;
        Pool = buffer[..(AliasCandidates.FormCount * unit)];
        var rest = buffer[Pool.Length..];
        Scratch = rest[..(2 * unit)];
        rest = rest[Scratch.Length..];
        Source = rest[..unit];
        rest = rest[Source.Length..];
        Emitted = rest[..(AliasCandidates.StepCount * 2 * unit)];
    }

    public static int RequiredLength(int lyricLength) => Units * (lyricLength + Margin);
}
