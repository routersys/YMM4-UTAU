using System.IO;

namespace UTAU.Models;

internal static class VoiceBankRepository
{
    static readonly Lock Gate = new();

    public static IReadOnlyList<VoiceBank> Banks { get; private set; } = [];

    public static bool IsLoaded { get; private set; }

    public static void Reload() => Reload(UTAUSettings.Default.EnumerateSearchDirectories());

    public static void Reload(IEnumerable<string> searchDirectories)
    {
        var banks = Load(searchDirectories);
        using (Gate.EnterScope())
        {
            Banks = banks;
            IsLoaded = true;
        }
    }

    public static void Invalidate()
    {
        using (Gate.EnterScope())
        {
            Banks = [];
            IsLoaded = false;
        }
    }

    public static VoiceBank? Find(string id)
        => Banks.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));

    public static IReadOnlyList<VoiceBank> Load(IEnumerable<string> searchDirectories)
    {
        var banks = new List<VoiceBank>();
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var searchDirectory in searchDirectories)
        {
            foreach (var bankDirectory in VoiceBankScanner.FindBankDirectories(searchDirectory))
            {
                if (!visitedDirectories.Add(bankDirectory))
                    continue;

                var id = VoiceBankPaths.CreateId(searchDirectory, bankDirectory);
                if (!usedIds.Add(id))
                {
                    id = Uri.EscapeDataString(bankDirectory.Replace(Path.DirectorySeparatorChar, '/'));
                    if (!usedIds.Add(id))
                        continue;
                }

                banks.Add(VoiceBankLoader.Load(id, bankDirectory));
            }
        }

        return banks;
    }
}
