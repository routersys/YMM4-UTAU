namespace UTAU.Models;

internal sealed class CharacterYaml
{
    public static CharacterYaml Empty { get; } = new();

    public IReadOnlyList<KeyValuePair<string, string>> Scalars { get; init; } = [];

    public IReadOnlyList<SubBank> SubBanks { get; init; } = [];

    public string? Find(string key)
        => Scalars.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
}
