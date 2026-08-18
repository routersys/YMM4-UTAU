using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using UTAU.Models;

namespace UTAU.ViewModels;

internal sealed record VoiceBankInformation(string Key, string Value);

internal sealed class VoiceBankViewModel(VoiceBank bank)
{
    public VoiceBank Bank => bank;

    public string Name => bank.Name;

    public string? Author => bank.Character.Author;

    public string? Version => bank.Character.Version;

    public string? Web => bank.Character.Web;

    public string RootDirectory => bank.RootDirectory;

    public string? Readme => bank.Readme;

    public bool HasReadme => !string.IsNullOrWhiteSpace(bank.Readme);

    public bool HasWeb => !string.IsNullOrWhiteSpace(bank.Character.Web);

    public bool HasPortrait => Portrait is not null;

    public ImageSource? Image { get; } = LoadImage(bank.Character.ImagePath);

    public ImageSource? Portrait { get; } = LoadImage(bank.PortraitPath);

    public IReadOnlyList<VoiceBankInformation> Information { get; } = BuildInformation(bank);

    public override string ToString() => Name;

    static IReadOnlyList<VoiceBankInformation> BuildInformation(VoiceBank bank)
    {
        var items = new List<VoiceBankInformation>
        {
            new(Texts.InfoName, bank.Name),
            new(Texts.InfoAliasCount, bank.AliasCount.ToString()),
            new(Texts.InfoOtoCount, bank.OtoSets.Count.ToString()),
            new(Texts.InfoSampleCount, bank.OtoSets.SelectMany(x => x.Entries).Select(x => x.SampleFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString()),
        };

        if (bank.Character.Author is { Length: > 0 } author)
            items.Add(new VoiceBankInformation(Texts.InfoAuthor, author));
        if (bank.Character.Version is { Length: > 0 } version)
            items.Add(new VoiceBankInformation(Texts.InfoVersion, version));
        if (bank.Character.Web is { Length: > 0 } web)
            items.Add(new VoiceBankInformation(Texts.InfoWeb, web));
        if (bank.Character.SamplePath is { Length: > 0 } sample)
            items.Add(new VoiceBankInformation(Texts.InfoSample, sample));
        if (bank.PrefixMap is { Count: > 0 } prefixMap)
            items.Add(new VoiceBankInformation(Texts.InfoPrefixMap, prefixMap.Count.ToString()));
        if (bank.SubBanks.Count > 0)
            items.Add(new VoiceBankInformation(Texts.InfoSubBank, bank.SubBanks.Count.ToString()));
        if (bank.Colors.Count > 0)
            items.Add(new VoiceBankInformation(Texts.InfoColors, string.Join(", ", bank.Colors)));

        foreach (var set in bank.OtoSets)
            items.Add(new VoiceBankInformation(Texts.InfoOtoSet, $"{set.Name} ({set.Entries.Count})"));

        foreach (var subBank in bank.SubBanks)
            items.Add(new VoiceBankInformation(
                Texts.InfoSubBankDetail,
                $"{(subBank.Color.Length == 0 ? Texts.DefaultColor : subBank.Color)} / {subBank.Prefix}*{subBank.Suffix} / {string.Join(", ", subBank.ToneRanges)}"));

        items.AddRange(bank.Character.AdditionalEntries.Select(x => new VoiceBankInformation(x.Key, x.Value)));
        items.AddRange(bank.CharacterYaml.EnumerateAdditionalScalars().Select(x => new VoiceBankInformation(x.Key, x.Value)));
        items.Add(new VoiceBankInformation(Texts.InfoPath, bank.RootDirectory));
        return items;
    }

    static ImageSource? LoadImage(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or UriFormatException or ArgumentException)
        {
            return null;
        }
    }
}
