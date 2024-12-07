﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Core.Servicers.Instances;
using Core.Servicers.Interfaces;
using NPOI.OpenXmlFormats.Dml.Diagram;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using UI.Controls.Base;
using UI.Controls.Navigation.Models;
using UI.Controls.Window;
using UI.Models;
using UI.Views;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace UI.ViewModels
{
    public class MainViewModel : MainWindowModel
    {
        private readonly IServiceProvider serviceProvider;
        private readonly IAppConfig appConfig;
        public ReactiveCommand<object, Unit> OnSelectedCommand { get; }
        public Command GotoPageCommand { get; }

        public IndexPageViewModel view { get; }
        private string[] pages = [ nameof(IndexPage), nameof(DataPage), nameof(ChartPage), nameof(CategoryPage)]; /* nameof(ChartPage), nameof(DataPage), nameof(CategoryPage)*/
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
            //GotoPageCommand = new Command(new Action<object>(OnGotoPageCommand));
            Items = new ObservableCollection<NavigationItemModel>();
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
        private void InitNavigation()
        {
            IndexUriList = new ();
            IndexUriList.AddRange(pages);
            Application.Current.TryFindResource("SideOverview", out var sideOverview);
            Application.Current.TryFindResource("SideStatistics", out var sideStatistics);
            Application.Current.TryFindResource("SideDetails", out var sideDetails);
            Application.Current.TryFindResource("SideSort", out var sideSort);
            var observable = Application.Current.Resources.GetResourceObservable("SideOverview");
            observable.Subscribe(newTitle =>
            {

            });


            Items.Add(new()
            {
                UnSelectedIcon = Controls.Base.IconTypes.Home,
                SelectedIcon = IconTypes.HomeSolid,
                Title = sideOverview as string,
                Uri = nameof(IndexPage),
                ID = -1
            });

            Items.Add(new Controls.Navigation.Models.NavigationItemModel()
            {
                UnSelectedIcon = Controls.Base.IconTypes.ZeroBars,
                SelectedIcon = IconTypes.FourBars,
                Title = sideStatistics as string,
                Uri = nameof(ChartPage),
                ID = 1,
            });

            Items.Add(new Controls.Navigation.Models.NavigationItemModel()
            {
                UnSelectedIcon = Controls.Base.IconTypes.Calendar,
                SelectedIcon = IconTypes.CalendarSolid,
                Title = sideDetails as string,
                Uri = nameof(DataPage),
                ID = 2,
            });

            Items.Add(new Controls.Navigation.Models.NavigationItemModel()
            {
                UnSelectedIcon = Controls.Base.IconTypes.EndPoint,
                SelectedIcon = IconTypes.EndPointSolid,
                Title = sideSort as string,
                Uri = nameof(CategoryPage),
                ID = 3,
            });

            LoadDefaultPage();
        }

        public void Toast(string content, ToastType type = ToastType.Info, IconTypes icon = IconTypes.Accept)
        {
            ToastContent = content;
            ToastIcon = icon;
            ToastType = type;
            IsShowToast = true;

        }



        public void LoadDefaultPage()
        {
            //int startPageIndex = appConfig.GetConfig().General.StartPage;
            //NavSelectedItem = Items[startPageIndex];
            //Uri = NavSelectedItem.Uri;
            NavSelectedItem = Items.First();
            Uri = NavSelectedItem.Uri;
        }

        private void SubscribeToResource(string key)
        {

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
