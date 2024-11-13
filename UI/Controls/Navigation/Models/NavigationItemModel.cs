using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Base.Color;
using UI.Controls.Base;
using UI.ViewModels;

namespace UI.Controls.Navigation.Models
{
    public class NavigationItemModel : ReactiveObject
    {
        public int ID { get; set; }

        private string _title;
        public string Title { get => _title; set => this.RaiseAndSetIfChanged(ref _title,value); }
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
        public string Uri { get; set; }
    }
}
