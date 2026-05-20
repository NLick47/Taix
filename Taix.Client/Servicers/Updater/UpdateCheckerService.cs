using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Taix.Client.Logging;
using Taix.Client.ViewModels;
using Velopack;
using Velopack.Sources;

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
        try
        {
            var newVersion = await CheckForUpdatesAsync();
            if (newVersion == null)
                return;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await PromptUpdateAsync(newVersion);
            });
        }
        catch (Velopack.Exceptions.NotInstalledException)
        {
            // 开发模式下未安装，忽略
        }
        catch (Exception ex)
        {
            Logger.Error($"Auto update check failed: {ex.Message}", ex);
        }
    }

    public async Task ManualCheckForUpdatesAsync()
    {
        try
        {
            var newVersion = await CheckForUpdatesAsync();
            if (newVersion == null)
            {
                _mainViewModel.Info(GetResourceString("NoUpdateAvailable"));
                return;
            }

            await PromptUpdateAsync(newVersion);
        }
        catch (Velopack.Exceptions.NotInstalledException)
        {
            _mainViewModel.Info("Velopack update is only available in installed mode.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Manual update check failed: {ex.Message}", ex);
            _mainViewModel.Error(GetResourceString("UpdateCheckFailed"));
        }
    }

    private async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        var mgr = CreateUpdateManager();
        return await mgr.CheckForUpdatesAsync();
    }

    private UpdateManager CreateUpdateManager()
    {
        // 使用 GitHub Releases 作为更新源
        // 后续可将仓库地址提取到配置中
        var source = new GithubSource("https://github.com/nlick47/taix", null, false);
        return new UpdateManager(source);
    }

    private async Task PromptUpdateAsync(UpdateInfo newVersion)
    {
        var title = GetResourceString("NewVersionAvailable");
        var message = string.Format(
            GetResourceString("UpdatePromptFormat") ?? "New version {0} is available. Do you want to download and install it now?",
            newVersion.TargetFullRelease.Version);

        var result = await _uiServicer.ShowConfirmDialogAsync(title, message);
        if (!result)
            return;

        _mainViewModel.Info(GetResourceString("DownloadingUpdate") ?? "Downloading update...");

        var mgr = CreateUpdateManager();
        await mgr.DownloadUpdatesAsync(newVersion);

        var restartTitle = GetResourceString("UpdateReady") ?? "Update Ready";
        var restartMessage = GetResourceString("UpdateReadyPrompt") ?? "Update downloaded. Restart now to apply?";

        var restartResult = await _uiServicer.ShowConfirmDialogAsync(restartTitle, restartMessage);
        if (restartResult)
        {
            mgr.ApplyUpdatesAndRestart(newVersion);
        }
    }

    private static string GetResourceString(string key)
    {
        Application.Current!.Resources.TryGetResource(key, null, out var value);
        return (value as string) ?? string.Empty;
    }
}
