using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.ViewModels;

namespace Taix.Client.Servicers.Updater;

public class UpdateCheckerService
{
    private readonly IUIServicer _uiServicer;
    private readonly MainViewModel _mainViewModel;

    public UpdateCheckerService(IUIServicer uiServicer, MainViewModel mainViewModel)
    {
        _uiServicer = uiServicer;
        _mainViewModel = mainViewModel;
    }

    public async Task AutoCheckForUpdatesAsync()
    {
        var (release, info) = await GetReleaseInfoAsync();
        if (info != null && release.IsCanUpdate()) await ShowUpdateDialogAsync();
    }

    public async Task ManualCheckForUpdatesAsync()
    {
        var (release, info) = await GetReleaseInfoAsync();
        if (info != null)
        {
            if (release.IsCanUpdate())
                await ShowUpdateDialogAsync();
            else
                _mainViewModel.Info(Application.Current.Resources["NoUpdateAvailable"] as string);
        }
        else
        {
            _mainViewModel.Error(Application.Current.Resources["UpdateCheckFailed"] as string);
        }
    }

    private async Task<(GithubRelease release, GithubRelease.VersionInfo? info)> GetReleaseInfoAsync()
    {
        var release = new GithubRelease("https://api.github.com/repos/nlick47/taix/releases/latest",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? string.Empty);
        var info = await release.GetRequestAsync();
        return (release, info);
    }

    private async Task ShowUpdateDialogAsync()
    {
        var result = await _uiServicer.ShowConfirmDialogAsync(
            Application.Current.Resources["NewVersionAvailable"] as string,
            Application.Current.Resources["WantGoDownloadPage"] as string);

        if (result)
            Process.Start(new ProcessStartInfo("https://github.com/NLick47/Taix/releases/latest")
                { UseShellExecute = true });
    }
}