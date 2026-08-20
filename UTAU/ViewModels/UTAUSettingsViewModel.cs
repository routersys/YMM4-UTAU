using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using UTAU.Models;
using UTAU.Synthesis;
using YukkuriMovieMaker.Commons;

namespace UTAU.ViewModels;

internal sealed class UTAUSettingsViewModel : Bindable
{
    bool isLoading;
    string statusText = string.Empty;
    VoiceBankViewModel? selectedBank;
    string? selectedDirectory;

    public UTAUSettingsViewModel()
    {
        RefreshCommand = new ActionCommand(_ => !IsLoading, async _ => await RefreshAsync());
        AddDirectoryCommand = new ActionCommand(_ => !IsLoading, _ => AddDirectory());
        RemoveDirectoryCommand = new ActionCommand(_ => SelectedDirectory is not null, _ => RemoveDirectory());
        OpenDefaultDirectoryCommand = new ActionCommand(_ => true, _ => OpenDefaultDirectory());
        ClearCacheCommand = new ActionCommand(_ => true, _ =>
        {
            AnalysisCache.Shared.Clear();
            SegmentCache.Shared.Clear();
        });
        UpdateBanks();
    }

    public UTAUSettings Settings => UTAUSettings.Default;

    public string DefaultDirectory => VoiceBankPaths.DefaultDirectory;

    public ObservableCollection<VoiceBankViewModel> Banks { get; } = [];

    public ICommand RefreshCommand { get; }

    public ICommand AddDirectoryCommand { get; }

    public ICommand RemoveDirectoryCommand { get; }

    public ICommand OpenDefaultDirectoryCommand { get; }

    public ICommand ClearCacheCommand { get; }

    public bool IsLoading
    {
        get => isLoading;
        private set => Set(ref isLoading, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => Set(ref statusText, value);
    }

    public VoiceBankViewModel? SelectedBank
    {
        get => selectedBank;
        set => Set(ref selectedBank, value);
    }

    public string? SelectedDirectory
    {
        get => selectedDirectory;
        set => Set(ref selectedDirectory, value);
    }

    async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            await Task.Run(VoiceBankRepository.Reload);
            UpdateBanks();
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    void UpdateBanks()
    {
        if (!VoiceBankRepository.IsLoaded)
            VoiceBankRepository.Reload();

        Banks.Clear();
        foreach (var bank in VoiceBankRepository.Banks.OrderBy(x => x.Name, StringComparer.CurrentCulture))
            Banks.Add(new VoiceBankViewModel(bank));

        SelectedBank = Banks.FirstOrDefault();
        StatusText = string.Format(Texts.VoiceBankCount, Banks.Count);
    }

    void AddDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Texts.AddDirectory,
            Multiselect = false,
        };
        if (dialog.ShowDialog() != true)
            return;

        var directory = dialog.FolderName;
        if (Settings.SearchDirectories.Any(x => string.Equals(x, directory, StringComparison.OrdinalIgnoreCase)))
            return;

        Settings.SearchDirectories.Add(directory);
        VoiceBankRepository.Invalidate();
        UpdateBanks();
    }

    void RemoveDirectory()
    {
        if (SelectedDirectory is not { } directory)
            return;

        Settings.SearchDirectories.Remove(directory);
        SelectedDirectory = null;
        VoiceBankRepository.Invalidate();
        UpdateBanks();
    }

    void OpenDefaultDirectory()
    {
        try
        {
            Directory.CreateDirectory(DefaultDirectory);
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(DefaultDirectory) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            StatusText = exception.Message;
        }
    }
}
