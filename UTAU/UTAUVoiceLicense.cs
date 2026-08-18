using UTAU.Models;
using YukkuriMovieMaker.Plugin.Voice;

namespace UTAU;

internal sealed class UTAUVoiceLicense(VoiceBank bank) : IVoiceLicense
{
    public VoiceLicenseDisplayLocation SummaryLocation => VoiceLicenseDisplayLocation.ItemEditor | VoiceLicenseDisplayLocation.CharacterEditor;

    public string? Summary => BuildSummary(bank);

    public bool IsTermsAgreed
    {
        get => true;
        set { }
    }

    public string? Terms => bank.Readme;

    public string? TermsURL => bank.Character.Web;

    public ValueTask<bool> ValidateLicenseAsync() => ValueTask.FromResult(true);

    static string? BuildSummary(VoiceBank bank)
    {
        var parts = new List<string>();
        if (bank.Character.Author is { Length: > 0 } author)
            parts.Add(string.Format(Texts.LicenseAuthorFormat, author));
        if (bank.Character.Version is { Length: > 0 } version)
            parts.Add(string.Format(Texts.LicenseVersionFormat, version));
        return parts.Count == 0 ? null : string.Join(" / ", parts);
    }
}
