using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Core.Servicers.Instances;
using Core.Servicers.Interfaces;
using NPOI.OpenXmlFormats.Dml.Diagram;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using UI.Controls.Base;
using UI.Controls.Navigation.Models;
using UI.Models;

namespace UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IAppConfig appConfig;
        public ObservableCollection<NavigationItemModel> Items { get; set; }

        private string Uri_;
        public string Uri
        {
            get { return Uri_; }
            set { this.RaiseAndSetIfChanged(ref Uri_, value); }
        }

        private NavigationItemModel NavSelectedItem_;
        public NavigationItemModel NavSelectedItem
        {
            get { return NavSelectedItem_; }
            set { this.RaiseAndSetIfChanged(ref NavSelectedItem_, value);  }
        }

        public void LoadDefaultPage()
        {
            int startPageIndex = appConfig.GetConfig().General.StartPage;
            NavSelectedItem = Items[startPageIndex];
            Uri = NavSelectedItem.Uri;
        }
    }
}
