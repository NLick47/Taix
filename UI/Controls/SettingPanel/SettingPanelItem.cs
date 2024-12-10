﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Core.Models.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Controls.SettingPanel
{
    public class SettingPanelItem  : ContentControl
    {
        public string Description { get { return GetValue(DescriptionProperty); } set { SetValue(DescriptionProperty, value); } }
        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<SettingPanelItem, string>(nameof(Description));

        /// <summary>
        /// 是否显示beta标识
        /// </summary>
        public bool IsBeta { get { return GetValue(IsBetaProperty); } set { SetValue(IsBetaProperty, value); } }
        public static readonly StyledProperty<bool> IsBetaProperty = 
            AvaloniaProperty.Register<SettingPanelItem, bool>(nameof(IsBetaProperty));

        protected override Type StyleKeyOverride => typeof(SettingPanelItem);

        public void Init(ConfigAttribute configAttribute_, object content_)
        {
            Name = configAttribute_.Name;
            Description = configAttribute_.Description;
            IsBeta = configAttribute_.IsBeta;
            Content = content_;
        }


    }
}
