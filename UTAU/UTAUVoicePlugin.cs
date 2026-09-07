using Telemetry;
using UTAU.Models;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Voice;

namespace UTAU;

[PluginDetails(AuthorName = "routersys", ContentId = "nc501723")]
internal sealed class UTAUVoicePlugin : IVoicePlugin
{
    public const string EngineName = "UTAU";
    public const string ApiName = "UTAU";

    public string Name => EngineName;

    public IEnumerable<IVoiceSpeaker> Voices => GetVoices();

    public bool CanUpdateVoices => true;

    public bool IsVoicesCached => VoiceBankRepository.IsLoaded;

    public Task UpdateVoicesAsync() => Task.Run(Reload);

    static IEnumerable<IVoiceSpeaker> GetVoices()
    {
        TelemetryReporter.Start();
        UTAUUpdateNotifier.EnsureCheckedOnce();

        if (!VoiceBankRepository.IsLoaded)
            Reload();

        foreach (var bank in VoiceBankRepository.Banks)
            yield return new UTAUVoiceSpeaker(bank);
    }

    static void Reload()
    {
        try
        {
            VoiceBankRepository.Reload();
        }
        catch (Exception exception)
        {
            TelemetryReporter.Report(exception);
            throw;
        }
    }
}
