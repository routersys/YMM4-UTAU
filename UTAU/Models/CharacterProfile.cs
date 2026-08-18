namespace UTAU.Models;

internal sealed class CharacterProfile
{
    public static CharacterProfile Empty { get; } = new();

    public string? Name { get; init; }

    public string? Author { get; init; }

    public string? Web { get; init; }

    public string? Version { get; init; }

    public string? ImagePath { get; init; }

    public string? SamplePath { get; init; }

    public IReadOnlyList<KeyValuePair<string, string>> AdditionalEntries { get; init; } = [];
}
