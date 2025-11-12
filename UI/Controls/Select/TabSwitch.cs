using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace UI.Controls.Select;

public class TabSwitch : TemplatedControl
{
    public static readonly StyledProperty<List<SelectItemModel>> OptionsProperty =
        AvaloniaProperty.Register<TabSwitch, List<SelectItemModel>>(nameof(Options));

    public static readonly StyledProperty<SelectItemModel> SelectedItemProperty =
        AvaloniaProperty.Register<TabSwitch, SelectItemModel>(nameof(SelectedItem));

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<TabSwitch, int>(nameof(SelectedIndex));

    public static readonly StyledProperty<bool> IsShowIconProperty =
        AvaloniaProperty.Register<TabSwitch, bool>(nameof(IsShowIcon), true);

    private StackPanel _itemsContainer;

    public TabSwitch()
    {
        Options = new List<SelectItemModel>();
    }

    public List<SelectItemModel> Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public SelectItemModel SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public bool IsShowIcon
    {
        get => GetValue(IsShowIconProperty);
        set => SetValue(IsShowIconProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == OptionsProperty || change.Property == IsShowIconProperty)
        {
            RenderItems();
        }
        else if (change.Property == SelectedIndexProperty)
        {
            UpdateSelectedItem();
        }
        else if (change.Property == SelectedItemProperty)
        {
            UpdateSelectedIndex();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _itemsContainer = e.NameScope.Get<StackPanel>("ItemsContainer");
        RenderItems();
    }

    private void RenderItems()
    {
        if (_itemsContainer == null || Options == null) return;

        _itemsContainer.Children.Clear();

        for (int i = 0; i < Options.Count; i++)
        {
            var item = Options[i];
            var option = CreateOption(item, i);
            _itemsContainer.Children.Add(option);
        }
        
        UpdateOptionsStyle();
    }

    private TabOption CreateOption(SelectItemModel item, int index)
    {
        var option = new TabOption  
        {
            Value = item,
            IsShowIcon = IsShowIcon,
            IsChecked = index == SelectedIndex
        };

        option.PointerPressed += (s, e) => 
        {
            SelectedIndex = index;
            e.Handled = true;
        };

        return option;
    }

    private void UpdateSelectedItem()
    {
        if (Options != null && SelectedIndex >= 0 && SelectedIndex < Options.Count)
        {
            SelectedItem = Options[SelectedIndex];
            UpdateOptionsStyle();
        }
    }

    private void UpdateSelectedIndex()
    {
        if (Options != null && SelectedItem != null)
        {
            var index = Options.IndexOf(SelectedItem);
            if (index >= 0)
            {
                SelectedIndex = index;
                UpdateOptionsStyle();
            }
        }
    }

    private void UpdateOptionsStyle()
    {
        if (_itemsContainer == null) return;

        for (int i = 0; i < _itemsContainer.Children.Count; i++)
        {
            if (_itemsContainer.Children[i] is TabOption option)
            {
                option.IsChecked = i == SelectedIndex;
            }
        }
    }

    protected override Type StyleKeyOverride => typeof(TabSwitch);
}