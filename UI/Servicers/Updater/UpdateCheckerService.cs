using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using UI.ViewModels;

namespace UI.Servicers.Updater;

public class UpdateCheckerService
{
    private readonly IServiceProvider _serviceProvider;

    public UpdateCheckerService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task AutoCheckForUpdatesAsync()
    {
        var (release, info) = await GetReleaseInfoAsync();
        if (info != null && release.IsCanUpdate()) await ShowUpdateDialogAsync();
    }

    public async Task ManualCheckForUpdatesAsync()
    {
        var (release, info) = await GetReleaseInfoAsync();
        var uiService = GetUIServicer();
        var mainView = _serviceProvider.GetService<MainViewModel>();
        if (info != null)
        {
            if (release.IsCanUpdate())
                await ShowUpdateDialogAsync();
            else
                mainView.Info(Application.Current.Resources["NoUpdateAvailable"] as string);
        }
        else
        {
            mainView.Error(Application.Current.Resources["UpdateCheckFailed"] as string);
        }
    }

    private async Task<(GithubRelease release, dynamic info)> GetReleaseInfoAsync()
    {
        var release = new GithubRelease("https://api.github.com/repos/nlick47/taix/releases/latest",
            Assembly.GetExecutingAssembly().GetName().Version.ToString());
        var info = await release.GetRequest();
        return (release, info);
    }

    private IUIServicer GetUIServicer()
    {
        return _serviceProvider.GetService(typeof(IUIServicer)) as IUIServicer;
    }

    private async Task ShowUpdateDialogAsync()
    {
        var uiService = GetUIServicer();
        var result = await uiService.ShowConfirmDialogAsync(
            Application.Current.Resources["NewVersionAvailable"] as string,
            Application.Current.Resources["WantGoDownloadPage"] as string);

        if (result)
            Process.Start(new ProcessStartInfo("https://github.com/NLick47/Taix/releases/latest")
                { UseShellExecute = true });
    }
}