using Avalonia;
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
        // Dictionary property
        private Dictionary<string, List<string>> _settingData = new Dictionary<string, List<string>>();

        public static readonly DirectProperty<SettingPanelMultiItem, Dictionary<string, List<string>>>
            SettingDataProperty =
                AvaloniaProperty.RegisterDirect<SettingPanelMultiItem, Dictionary<string, List<string>>>(
                    nameof(SettingData),
                    o => o.SettingData,
                    (o, v) => o.SettingData = v);

        public Dictionary<string, List<string>> SettingData
        {
            get => _settingData;
            set => SetAndRaise(SettingDataProperty, ref _settingData, value);
        }

// Fold property with default value
        private bool _fold = false;

        public static readonly DirectProperty<SettingPanelMultiItem, bool> FoldProperty =
            AvaloniaProperty.RegisterDirect<SettingPanelMultiItem, bool>(
                nameof(Fold),
                o => o.Fold,
                (o, v) => o.Fold = v);

        public bool Fold
        {
            get => _fold;
            set => SetAndRaise(FoldProperty, ref _fold, value);
        }

// Generic object property
        private object _data;

        public static readonly DirectProperty<SettingPanelMultiItem, object> DataProperty =
            AvaloniaProperty.RegisterDirect<SettingPanelMultiItem, object>(
                nameof(Data),
                o => o.Data,
                (o, v) => o.Data = v);

        public object Data
        {
            get => _data;
            set => SetAndRaise(DataProperty, ref _data, value);
        }

// Command properties
        private ICommand _onRemoveAction;

        public static readonly DirectProperty<SettingPanelMultiItem, ICommand> OnRemoveActionProperty =
            AvaloniaProperty.RegisterDirect<SettingPanelMultiItem, ICommand>(
                nameof(OnRemoveAction),
                o => o.OnRemoveAction,
                (o, v) => o.OnRemoveAction = v);

        public ICommand OnRemoveAction
        {
            get => _onRemoveAction;
            set => SetAndRaise(OnRemoveActionProperty, ref _onRemoveAction, value);
        }

        private ICommand _onFoldAction;

        public static readonly DirectProperty<SettingPanelMultiItem, ICommand> OnFoldActionProperty =
            AvaloniaProperty.RegisterDirect<SettingPanelMultiItem, ICommand>(
                nameof(OnFoldAction),
                o => o.OnFoldAction,
                (o, v) => o.OnFoldAction = v);

        public ICommand OnFoldAction
        {
            get => _onFoldAction;
            set => SetAndRaise(OnFoldActionProperty, ref _onFoldAction, value);
        }

// String property
        private string _title = string.Empty;

        public static readonly DirectProperty<SettingPanelMultiItem, string> TitleProperty =
            AvaloniaProperty.RegisterDirect<SettingPanelMultiItem, string>(
                nameof(Title),
                o => o.Title,
                (o, v) => o.Title = v);

        public string Title
        {
            get => _title;
            set => SetAndRaise(TitleProperty, ref _title, value);
        }

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
                        if (!string.IsNullOrEmpty(item))
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
            contextMenuItemDel.Click += (e, c) => { listControl.Items.Remove(listControl.SelectedItem); };
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
                textBox.Error = attribute.Name + (string.IsNullOrEmpty(textBox.Text)
                    ? Application.Current.FindResource("CannotBeEmpty")
                    : Application.Current.FindResource("AlreadyExists"));

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