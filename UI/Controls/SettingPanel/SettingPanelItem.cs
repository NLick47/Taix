using System;
using Avalonia;
using Avalonia.Controls;
using Core.Models.Config;

namespace UI.Controls.SettingPanel;

public class SettingPanelItem : ContentControl
{
    public static readonly DirectProperty<SettingPanelItem, string> DescriptionProperty =
        AvaloniaProperty.RegisterDirect<SettingPanelItem, string>(
            nameof(Description),
            o => o.Description,
            (o, v) => o.Description = v);

    public static readonly StyledProperty<bool> IsBetaProperty =
        AvaloniaProperty.Register<SettingPanelItem, bool>(nameof(IsBetaProperty));

    private string _description = string.Empty;

    public string Description
    {
        get => _description;
        set => SetAndRaise(DescriptionProperty, ref _description, value);
    }

    /// <summary>
    ///     是否显示beta标识
    /// </summary>
    public bool IsBeta
    {
        get => GetValue(IsBetaProperty);
        set => SetValue(IsBetaProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(SettingPanelItem);

    public void Init(ConfigAttribute configAttribute_, object content_)
    {
        Name = configAttribute_.Name;
        Description = configAttribute_.Description;
        IsBeta = configAttribute_.IsBeta;
        Content = content_;
    }
}