namespace UTAU.Phonemes;

internal readonly ref struct AliasWorkspace
{
    const int Margin = 8;

    public readonly Span<char> Pool;
    public readonly Span<char> Scratch;
    public readonly Span<char> Source;
    public readonly Span<char> Candidate;

    public AliasWorkspace(Span<char> buffer, int lyricLength)
    {
        var unit = lyricLength + Margin;
        Pool = buffer[..(AliasCandidates.FormCount * unit)];
        var rest = buffer[Pool.Length..];
        Scratch = rest[..(2 * unit)];
        rest = rest[Scratch.Length..];
        Source = rest[..unit];
        Candidate = rest.Slice(unit, unit + Margin);
    }

    public static int RequiredLength(int lyricLength)
        => (AliasCandidates.FormCount + 4) * (lyricLength + Margin) + Margin;
}
