using System;
using System.Net.Http;
using Jab;
using Taix.Client.Events;
using Taix.Client.Librarys.Api;
using Taix.Client.Servicers;
using Taix.Client.Servicers.Instances;
using Taix.Client.Servicers.Interfaces;
using Taix.Client.Servicers.Updater;
using Taix.Client.Shared.Servicers.Interfaces;
using Taix.Client.ViewModels;
using Taix.Client.Views;

namespace Taix.Client;

[ServiceProvider]
[Singleton(typeof(HttpClient), Factory = nameof(CreateHttpClient))]
[Singleton(typeof(ITaixApiClient), typeof(TaixApiClient))]
[Singleton(typeof(IData), typeof(ApiData))]
[Singleton(typeof(IWebData), typeof(ApiWebData))]
[Singleton(typeof(IAppData), typeof(ApiAppData))]
[Singleton(typeof(IWebSiteData), typeof(ApiWebSiteData))]
[Singleton(typeof(ICategorys), typeof(ApiCategorys))]
[Singleton(typeof(ICategorySummaryData), typeof(ApiCategorySummaryData))]
[Singleton(typeof(IAppConfig), typeof(ApiAppConfig))]
[Singleton(typeof(IWindowStateService), typeof(WindowStateService))]
[Singleton(typeof(IUIServicer), typeof(UIServicer))]
[Singleton(typeof(IDialogService), Factory = nameof(CreateDialogService))]
[Singleton(typeof(IClipboardService), typeof(ClipboardService))]
[Singleton(typeof(IProcessService), typeof(ProcessService))]
[Singleton(typeof(IContextMenuServicer), typeof(ContextMenuServicer))]
[Singleton(typeof(IAppEventService), typeof(AppEventService))]
[Singleton(typeof(IAppUpdateService), typeof(AppUpdateService))]
[Singleton(typeof(IThemeServicer), typeof(ThemeServicer))]
[Singleton(typeof(IMainServicer), typeof(MainServicer))]
[Singleton(typeof(IShutdownService), typeof(ShutdownService))]
[Singleton(typeof(IStateService), typeof(StateService))]
[Singleton(typeof(ISearchService), typeof(SearchService))]
[Singleton(typeof(IShortcutService), typeof(ShortcutService))]
[Singleton(typeof(MainViewModel))]
[Singleton(typeof(INavigationService), Factory = nameof(CreateNavigationService))]
[Singleton(typeof(INavigationDataService), Factory = nameof(CreateNavigationDataService))]
[Singleton(typeof(IToastService), Factory = nameof(CreateToastService))]
[Transient(typeof(UpdateCheckerService))]
[Transient(typeof(IndexPage))]
[Transient(typeof(IndexPageViewModel))]
[Transient(typeof(DataPage))]
[Transient(typeof(DataPageViewModel))]
[Transient(typeof(SettingPage))]
[Transient(typeof(SettingPageViewModel))]
[Transient(typeof(DetailPage))]
[Transient(typeof(DetailPageViewModel))]
[Transient(typeof(CategoryPage))]
[Transient(typeof(CategoryPageViewModel))]
[Transient(typeof(CategoryAppListPage))]
[Transient(typeof(CategoryAppListPageViewModel))]
[Transient(typeof(CategoryWebSiteListPage))]
[Transient(typeof(CategoryWebSiteListPageViewModel))]
[Transient(typeof(ChartPage))]
[Transient(typeof(ChartPageViewModel))]
[Transient(typeof(WebSiteDetailPage))]
[Transient(typeof(WebSiteDetailPageViewModel))]
[Transient(typeof(CategorySummaryPage))]
[Transient(typeof(CategorySummaryPageViewModel))]
[Transient(typeof(SearchPaletteViewModel))]
internal partial class AppServiceProvider
{
    public HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        var serverUrl = Environment.GetEnvironmentVariable("TAIX_SERVER") ?? "http://127.0.0.1:37091";
        client.BaseAddress = new Uri(serverUrl);
        client.Timeout = TimeSpan.FromSeconds(6);
        return client;
    }

    public static IDialogService CreateDialogService(IUIServicer ui) => ui as IDialogService;
    public static INavigationService CreateNavigationService(MainViewModel vm) => vm;
    public static INavigationDataService CreateNavigationDataService(MainViewModel vm) => vm;
    public static IToastService CreateToastService(MainViewModel vm) => vm;
}
