﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Core.Models.Config;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UI.Controls.Button;
using UI.Controls.Input;
using UI.Controls.List;

namespace UI.Controls.SettingPanel
{
    public class SettingPanelMultiItem : TemplatedControl
    {

        /// <summary>
        /// 所有设置数据
        /// </summary>
        public Dictionary<string, List<string>> SettingData { get { return GetValue(SettingDataProperty); } set { SetValue(SettingDataProperty, value); } }
        public static readonly StyledProperty<Dictionary<string, List<string>>> SettingDataProperty =
            AvaloniaProperty.Register<SettingPanelMultiItem, Dictionary<string, List<string>>>(nameof(SettingData));
        public bool Fold { get { return GetValue(FoldProperty); } set { SetValue(FoldProperty, value); } }
        public static readonly StyledProperty<bool> FoldProperty =
            AvaloniaProperty.Register<SettingPanelMultiItem, bool>(nameof(Fold), false);

        public object Data { get { return (object)GetValue(DataProperty); } set { SetValue(DataProperty, value); } }
        public static readonly StyledProperty<object> DataProperty =
            AvaloniaProperty.Register<SettingPanelMultiItem, object>(nameof(Data));

        public ICommand OnRemoveAction { get { return GetValue(OnRemoveActionProperty); } set { SetValue(OnRemoveActionProperty, value); } }
        public static readonly StyledProperty<ICommand> OnRemoveActionProperty =
             AvaloniaProperty.Register<SettingPanelMultiItem, ICommand>(nameof(OnRemoveAction));
        public ICommand OnFoldAction { get { return GetValue(OnFoldActionProperty); } set { SetValue(OnFoldActionProperty, value); } }
        public static readonly StyledProperty<ICommand> OnFoldActionProperty =
            AvaloniaProperty.Register<SettingPanelMultiItem, ICommand>(nameof(OnFoldAction));
        public string Title { get { return GetValue(TitleProperty); } set { SetValue(TitleProperty, value); } }
        public static readonly StyledProperty<string> TitleProperty =
           AvaloniaProperty.Register<SettingPanelMultiItem, string>(nameof(Title));
        public event EventHandler DataChanged;

        private StackPanel Container;
        private object configData;
        private IconButton FoldBtn;

        protected override Type StyleKeyOverride => typeof(SettingPanelMultiItem);

        public SettingPanelMultiItem()
        {
            if (SettingData == null)
            {
                SettingData = new Dictionary<string, List<string>>();
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            Container = e.NameScope.Get<StackPanel>("Container");
            FoldBtn = e.NameScope.Find<IconButton>("FoldBtn");

            OnFoldAction = ReactiveCommand.Create<object>(FoldAction);
            Render();
        }

        private void Render()
        {
            if (Container == null)
            {
                return;
            }

            Container.Children.Clear();

            configData = Activator.CreateInstance(Data.GetType());

            // 遍历方法特性
            foreach (PropertyInfo pi in Data.GetType().GetProperties())
            {
                foreach (Attribute attr in pi.GetCustomAttributes(true))
                {
                    if (attr is ConfigAttribute)
                    {
                        ConfigAttribute attribute = (ConfigAttribute)attr;

                        if (pi.PropertyType == typeof(string))
                        {
                            //  输入框
                            RenderTextBox(attribute, pi);
                        }
                        else if (pi.PropertyType == typeof(List<string>))
                        {
                            //  String 集合
                            RenderStringList(attribute, pi);
                        }
                    }
                }
            }
        }



        private void FoldAction(object obj)
        {
            Fold = !Fold;
            if (Fold)
            {
                FoldBtn.Icon = Base.IconTypes.ChevronDown;
                ToolTip.SetTip(FoldBtn, "展开");
            }
            else
            {
                FoldBtn.Icon = Base.IconTypes.ChevronUp;
                ToolTip.SetTip(FoldBtn, "收起");
            }
        }
        private void RenderStringList(ConfigAttribute attribute, PropertyInfo pi)
        {
            var title = new SettingPanelItem();
            title.Name = attribute.Name;
            title.Description = attribute.Description;

            Container.Children.Add(title);

            var list = pi.GetValue(Data) as List<string>;

            if (SettingData.ContainsKey(pi.Name))
            {
                SettingData[pi.Name] = SettingData[pi.Name].Concat(list).ToList();
            }
            else
            {
                SettingData.Add(pi.Name, list);
            }

            var listControl = new BaseList();

            listControl.Loaded += (sender, args) =>
            {
                listControl.Items.CollectionChanged += (o, e) =>
                {
                    var newData = new List<string>();
                    foreach (var item in listControl.Items)
                    {
                        if(!string.IsNullOrEmpty(item))
                        {
                            newData.Add(item.ToString());
                        }
                        
                    }
                    pi.SetValue(configData, newData);
                    Data = configData;
                    DataChanged?.Invoke(this, EventArgs.Empty);
                };
            };
            listControl.Margin = new Thickness(15, 0, 15, 10);

            if (list != null)
            {
                foreach (string item in list)
                {
                    listControl.Items.Add(item);
                }
            }
            else
            {
                list = new List<string>();
            }
            var contextMenu = new ContextMenu();

            var contextMenuItemDel = new MenuItem();
            contextMenuItemDel.Header = Application.Current.FindResource("Remove");
            contextMenuItemDel.Click += (e, c) =>
            {
                listControl.Items.Remove(listControl.SelectedItem);
            };
            contextMenu.Items.Add(contextMenuItemDel);
            listControl.ContextMenu = contextMenu;


            var addInputBox = new InputBox();
            addInputBox.Placeholder = attribute.Name;
            addInputBox.Margin = new Thickness(0, 0, 10, 0);

            var addBtn = new Button.Button();
            //addBtn.Margin = new Thickness(15, 0, 15, 10);
            addBtn.Content = Application.Current.FindResource("Add");

            addBtn.Click += (e, c) =>
            {
                addInputBox.Error = attribute.Name + (string.IsNullOrEmpty(addInputBox.Text) ? "不能为空" : "已存在");

                if ((string.IsNullOrEmpty(addInputBox?.Text) || list.Contains(addInputBox.Text)))
                {
                    addInputBox.ShowError();
                    return;
                }

                //  判断重复
                if (!attribute.IsCanRepeat && SettingData[pi.Name].Contains(addInputBox.Text))
                {
                    addInputBox.ShowError();
                    return;
                }

                //list.Add(addInputBox.Text);
                listControl.Items.Add(addInputBox.Text);
                addInputBox.Text = String.Empty;



            };
            pi.SetValue(configData, list);
            Container.Children.Add(listControl);

            var inputPanel = new Grid();
            inputPanel.ColumnDefinitions.Add(
                new ColumnDefinition()
                {
                    Width = new GridLength(10, GridUnitType.Star)
                });
            inputPanel.ColumnDefinitions.Add(
               new ColumnDefinition()
               {
                   Width = new GridLength(2, GridUnitType.Star)
               });
            inputPanel.Margin = new Thickness(15, 10, 15, 10);
            Grid.SetColumn(addInputBox, 0);
            Grid.SetColumn(addBtn, 1);
            inputPanel.Children.Add(addInputBox);
            inputPanel.Children.Add(addBtn);
            Container.Children.Add(inputPanel);


        }
        private void RenderTextBox(ConfigAttribute attribute, PropertyInfo pi)
        {
            string value = (string)pi.GetValue(Data);

            if (SettingData.ContainsKey(pi.Name))
            {
                SettingData[pi.Name].Add(value);
            }
            else
            {
                SettingData.Add(pi.Name, new List<string>()
                {
                    value
                });
            }


            var textBox = new InputBox();
            textBox.Text = value;
            textBox.Placeholder = attribute.Name;
            textBox.Width = 125;
            textBox.TextChanged += (e, c) =>
            {
                textBox.Error = attribute.Name + (string.IsNullOrEmpty(textBox.Text) ? Application.Current.FindResource("CannotBeEmpty") 
                    :  Application.Current.FindResource("AlreadyExists"));

                if (!attribute.IsCanRepeat)
                {
                    if (SettingData[pi.Name].Contains(textBox.Text) && textBox.Tag?.ToString() != textBox.Text)
                    {
                        textBox.ShowError();
                        return;
                    }
                    else
                    {
                        textBox.HideError();
                    }
                }
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.ShowError();
                }
                else
                {
                    textBox.HideError();
                }
            };
            textBox.GotFocus += (e, c) =>
            {
                //  记录获得焦点时的数据
                textBox.Tag = textBox.Text;
            };
            textBox.LostFocus += (e, c) =>
            {
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.Text = textBox.Tag.ToString();
                    return;
                }
                if (!attribute.IsCanRepeat)
                {
                    if (SettingData[pi.Name].Contains(textBox.Text) && textBox.Tag.ToString() != textBox.Text)
                    {
                        textBox.Text = textBox.Tag.ToString();
                        return;
                    }
                }
                if (attribute.IsName)
                {
                    Title = textBox.Text;
                }
                pi.SetValue(configData, textBox.Text);
                Data = configData;
                DataChanged?.Invoke(this, EventArgs.Empty);

                SettingData[pi.Name].Remove(textBox.Tag.ToString());
                SettingData[pi.Name].Add(textBox.Text);

            };
            var item = new SettingPanelItem();
            item.Name = attribute.Name;
            item.Description = attribute.Description;
            item.Content = textBox;
            pi.SetValue(configData, textBox.Text);
            if (attribute.IsName)
            {
                Title = textBox.Text;
            }
            Container.Children.Add(item);
        }

    }
}
