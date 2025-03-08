using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Core.Servicers.Instances;
using Core.Servicers.Interfaces;
using System.Reactive.Concurrency;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using UI.Controls.Base;
using UI.Controls.Navigation.Models;
using UI.Controls.Window;
using UI.Models;
using UI.Views;
using Avalonia.Controls.ApplicationLifetimes;
using Core;
using static UI.Controls.SettingPanel.SettingPanel;
using System.Reflection;
using UI.Servicers.Updater;
using System.Threading.Tasks;
using UI.Servicers;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace UI.ViewModels
{
    public class MainViewModel : MainWindowModel
    {
        private readonly IServiceProvider serviceProvider;
        private readonly IAppConfig appConfig;
        public ReactiveCommand<object, Unit> OnSelectedCommand { get; }
        public ICommand GotoPageCommand { get; }

        public IndexPageViewModel view { get; }

        private string[]
            pages =
            [
                nameof(IndexPage), nameof(DataPage), nameof(ChartPage), nameof(CategoryPage)
            ]; /* nameof(ChartPage), nameof(DataPage), nameof(CategoryPage)*/

        public MainViewModel(
            IServiceProvider serviceProvider,
            IAppConfig appConfig,
            IMain main
        )
        {
            this.serviceProvider = serviceProvider;
            this.appConfig = appConfig;
            ServiceProvider = serviceProvider;
            OnSelectedCommand = ReactiveCommand.Create<object>(OnSelectedCommandHandle);
            GotoPageCommand = ReactiveCommand.Create<object>(OnGotoPageCommand);
            Items = new();
#if !DEBUG
            Title = "Taix";
#elif DEBUG
            Title = "Taix -Debug";
#endif
            PropertyChanged += MainViewModel_PropertyChanged;
            InitNavigation();
        }

        private void MainViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Uri))
            {
                if (Uri == nameof(IndexPageViewModel))
                {
                    Data = null;
                }
            }
        }

        private void OnGotoPageCommand(object obj)
        {
            Uri = obj.ToString();
        }

        private void InitNavigation()
        {
            IndexUriList = new(pages);
            var overviewObservable = Application.Current.Resources.GetResourceObservable("SideOverview")
                .DistinctUntilChanged();
            var statisticsObservable = Application.Current.Resources.GetResourceObservable("SideStatistics")
                .DistinctUntilChanged();
            var detailsObservable = Application.Current.Resources.GetResourceObservable("SideDetails")
                .DistinctUntilChanged();
            var sortObservable = Application.Current.Resources.GetResourceObservable("SideSort").DistinctUntilChanged();

            Items =
            [
                new()
                {
                    UnSelectedIcon = Controls.Base.IconTypes.Home,
                    SelectedIcon = IconTypes.HomeSolid,
                    Uri = nameof(IndexPage),
                    ID = -1
                },
                new()
                {
                    UnSelectedIcon = Controls.Base.IconTypes.ZeroBars,
                    SelectedIcon = IconTypes.FourBars,
                    Uri = nameof(ChartPage),
                    ID = 1,
                },
                new()
                {
                    UnSelectedIcon = Controls.Base.IconTypes.Calendar,
                    SelectedIcon = IconTypes.CalendarSolid,
                    Uri = nameof(DataPage),
                    ID = 2,
                },
                new()
                {
                    UnSelectedIcon = Controls.Base.IconTypes.EndPoint,
                    SelectedIcon = IconTypes.EndPointSolid,
                    Uri = nameof(CategoryPage),
                    ID = 3,
                }
            ];
            SubscribeToResourceObservable(overviewObservable, -1);
            SubscribeToResourceObservable(statisticsObservable, 1);
            SubscribeToResourceObservable(detailsObservable, 2);
            SubscribeToResourceObservable(sortObservable, 3);
        }

        public void Toast(string content, ToastType type = ToastType.Info, IconTypes icon = IconTypes.Accept)
        {
            ToastContent = content;
            ToastIcon = icon;
            ToastType = type;
            IsShowToast = true;
        }


        public async void LoadDefaultPage()
        {
            int startPageIndex = appConfig.GetConfig().General.StartPage;
            NavSelectedItem = Items[startPageIndex];
            if (NavSelectedItem != Items[startPageIndex])
            {
                NavSelectedItem = Items[startPageIndex];
            }

            Uri = Items[startPageIndex].Uri;
            if (Uri != Items[startPageIndex].Uri)
            {
                Uri = Items[startPageIndex].Uri;
            }

            if (appConfig.GetConfig().General.IsAutoUpdate)
            {
                var updateService = serviceProvider.GetService<UpdateCheckerService>();
                await updateService!.AutoCheckForUpdatesAsync();
            }
        }

        private void SubscribeToResourceObservable(IObservable<object> observable, int id)
        {
            observable.Subscribe(newTitle =>
            {
                var nv = Items.First(x => x.ID == id);

                Items.Remove(nv);
                nv.Title = newTitle as string;
                Items.Add(nv);
            });
        }

        private void OnSelectedCommandHandle(object obj)
        {
            if (!string.IsNullOrEmpty(NavSelectedItem.Uri))
            {
                Uri = NavSelectedItem.Uri;
            }
        }

        public void Error(string message_)
        {
            Toast(message_, ToastType.Error, IconTypes.Error);
        }

        public void Info(string message_)
        {
            Toast(message_, ToastType.Info, IconTypes.Info);
        }

        public void Success(string message_)
        {
            Toast(message_, ToastType.Success, IconTypes.Accept);
        }
    }
}