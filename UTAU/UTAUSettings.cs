using System.Collections.ObjectModel;
using System.IO;
using UTAU.Models;
using UTAU.Synthesis;
using UTAU.Views;
using YukkuriMovieMaker.Plugin;

namespace UTAU;

internal sealed class UTAUSettings : SettingsBase<UTAUSettings>
{
    public const int MinimumCacheMegabytes = 64;
    public const int MaximumCacheMegabytes = 4096;

    F0Estimator f0Estimator = F0Estimator.Harvest;
    StretchMode stretchMode = StretchMode.Loop;
    int analysisCacheMegabytes = 512;
    int segmentCacheMegabytes = 256;

    public override SettingsCategory Category => SettingsCategory.Voice;

    public override string Name => UTAUVoicePlugin.EngineName;

    public override bool HasSettingView => true;

    public override object? SettingView => new UTAUSettingsView();

    public ObservableCollection<string> SearchDirectories { get; } = [];

    public F0Estimator F0Estimator
    {
        get => f0Estimator;
        set => Set(ref f0Estimator, value);
    }

    public StretchMode StretchMode
    {
        get => stretchMode;
        set => Set(ref stretchMode, value);
    }

    public int AnalysisCacheMegabytes
    {
        get => analysisCacheMegabytes;
        set
        {
            if (Set(ref analysisCacheMegabytes, Math.Clamp(value, MinimumCacheMegabytes, MaximumCacheMegabytes)))
                ApplyCacheBudget();
        }
    }

    public int SegmentCacheMegabytes
    {
        get => segmentCacheMegabytes;
        set
        {
            if (Set(ref segmentCacheMegabytes, Math.Clamp(value, MinimumCacheMegabytes, MaximumCacheMegabytes)))
                ApplyCacheBudget();
        }
    }

    public override void Initialize() => ApplyCacheBudget();

    void ApplyCacheBudget()
    {
        AnalysisCache.Shared.BudgetBytes = (long)analysisCacheMegabytes * 1024 * 1024;
        SegmentCache.Shared.BudgetBytes = (long)segmentCacheMegabytes * 1024 * 1024;
    }

    public IReadOnlyList<string> EnumerateSearchDirectories()
    {
        var directories = new List<string> { VoiceBankPaths.DefaultDirectory };
        directories.AddRange(SearchDirectories.Where(x => !string.IsNullOrWhiteSpace(x)));
        return directories
            .Select(NormalizeDirectory)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    static string? NormalizeDirectory(string directory)
    {
        try
        {
            return Path.GetFullPath(directory.Trim());
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (PathTooLongException)
        {
            return null;
        }
    }
}
