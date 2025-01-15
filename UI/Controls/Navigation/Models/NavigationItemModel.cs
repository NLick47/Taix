using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Base.Color;
using UI.Controls.Base;
using UI.Models;
using UI.ViewModels;

namespace UI.Controls.Navigation.Models
{
    public class NavigationItemModel : UINotifyPropertyChanged
    {
        public int ID { get; set; }

        private string _title;
        public string Title
        {
            get => _title; 
            set
            {
                if (value != _title)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }
        /// <summary>
        /// 未选择时默认图标
        /// </summary>
        public IconTypes UnSelectedIcon { get; set; }

        private IconTypes SelectedIcon_ = IconTypes.None;
        /// <summary>
        /// 选中后图标
        /// </summary>
        public IconTypes SelectedIcon
        {
            get
            {

                if (SelectedIcon_ == IconTypes.None)
                {
                    return UnSelectedIcon;
                }
                else
                {
                    return SelectedIcon_;
                }
            }
            set
            {
                SelectedIcon_ = value;
            }
        }
        public ColorTypes IconColor { get; set; }
        public string BadgeText { get; set; }

        private string _uri;
        public string Uri { get => _uri;
            set
            {
                if (value != _uri)
                {
                    _uri = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
