namespace UTAU.Models;

internal sealed class CharacterYaml
{
    public static CharacterYaml Empty { get; } = new();

    public IReadOnlyList<KeyValuePair<string, string>> Scalars { get; init; } = [];

    public IReadOnlyList<SubBank> SubBanks { get; init; } = [];

    static readonly string[] KnownKeys = ["name", "author", "voice", "web", "version", "image", "portrait", "sample"];

    public string? Find(string key)
        => Scalars.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    public IEnumerable<KeyValuePair<string, string>> EnumerateAdditionalScalars()
        => Scalars.Where(x => !KnownKeys.Contains(x.Key, StringComparer.OrdinalIgnoreCase));
}
