using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using UI.Controls.Button;
using Avalonia;
using ReactiveUI;
using Core.Models.Config;
using System.Reflection;
using UI.Controls.Input;
using UI.Controls.List;
using Avalonia.Media;
using System.Collections;
using UI.Controls.Select;
using System.IO;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using SharedLibrary.Librarys;
using Newtonsoft.Json;
using UI.Controls.Window;
using UI.Servicers;
using UI.ViewModels;
using SharedLibrary;

namespace UI.Controls.SettingPanel
{
    public class SettingPanel : TemplatedControl
    {
        public struct Config
        {
            public ConfigAttribute Attribute { get; set; }
            public PropertyInfo PropertyInfo { get; set; }
        }

        private object _data;
        public static readonly DirectProperty<SettingPanel, object> DataProperty =
            AvaloniaProperty.RegisterDirect<SettingPanel, object>(
                nameof(Data),
                o => o.Data,
                (o, v) => o.Data = v);
        public object Data
        {
            get => _data;
            set => SetAndRaise(DataProperty, ref _data, value);
        }

        public SolidColorBrush SpliteLineBrush { get { return GetValue(SpliteLineBrushProperty); } set { SetValue(SpliteLineBrushProperty, value); } }
        public static readonly StyledProperty<SolidColorBrush> SpliteLineBrushProperty =
            AvaloniaProperty.Register<SettingPanel, SolidColorBrush>(nameof(SpliteLineBrush));

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == DataProperty ||
                change.Property == SpliteLineBrushProperty)
            {
                var control = change.Sender as SettingPanel;
                control.Render();
            }
        }

        private StackPanel Container;
        private object configData;
        private readonly string nosetGroupKey = "noset_group";
        private bool isCanRender = true;
        private Dictionary<string, List<Config>> configList;
        public Dictionary<string, List<string>> SettingData { get; set; }

        public SettingPanel()
        {
            SettingData = new Dictionary<string, List<string>>();
        }

        protected override Type StyleKeyOverride => typeof(SettingPanel);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            Container = e.NameScope.Find<StackPanel>("Container");

            isCanRender = true;
            Render();
        }

        private void Render()
        {
            if (!isCanRender)
            {
                isCanRender = true;
                return;
            }
        
            if (Container == null)
            {
                return;
            }
            configList = new Dictionary<string, List<Config>>();
            configList.Add(nosetGroupKey, new List<Config>());
            Container.Children.Clear();

            if (Data == null)
            {
                return;
            }
            if (Data is IEnumerable)
            {
                //  多例
                RenderMultiPanel();
            }
            else
            {
                //  单例
                RenderSinglePanel();
            }
        }

        private void RenderMultiPanel()
        {
            var dataList = Data as IEnumerable<object>;
            var list = dataList.ToList();
            var cacheList = new Dictionary<int, object>();

            var dataType = Data.GetType();
            var itemType = dataType.GenericTypeArguments[0];
            var test1 = dataType.GetGenericArguments()[0];

            var emptyData = Activator.CreateInstance(itemType);

            //  添加按钮
            var addBtn = new Button.Button();
            addBtn.Content = "新建项";
            addBtn.Width = 150;
            addBtn.Icon = Base.IconTypes.CalculatorAddition;
            addBtn.Click += (s, e) =>
            {
                int id = cacheList.Keys.Count > 0 ? cacheList.Keys.Max() + 1 : 1;

                cacheList.Add(id, emptyData);

                var panel = CreateMultiPanel(emptyData, id, cacheList);

                Container.Children.Insert(Container.Children.Count - 1, panel);

            };
            Container.Children.Add(addBtn);


            //  渲染数据
            for (int i = 0; i < list.Count; i++)
            {
                int id = cacheList.Keys.Count > 0 ? cacheList.Keys.Max() + 1 : 1;

                cacheList.Add(id, list[i]);
                var panel = CreateMultiPanel(list[i], id, cacheList);
                Container.Children.Insert(Container.Children.Count - 1, panel);
            }
        }

        private SettingPanelMultiItem CreateMultiPanel(object data, int id, Dictionary<int, object> cacheList)
        {
            var panel = new SettingPanelMultiItem();
            panel.Data = data;
            panel.Tag = id;
            panel.SettingData = SettingData;
            panel.DataChanged += (e, c) =>
            {
                int pid = (int)panel.Tag;

                if (cacheList.ContainsKey(pid))
                {
                    //  更新
                    cacheList[pid] = panel.Data;

                    var listType = typeof(List<>);
                    var constructedListType = listType.MakeGenericType(panel.Data.GetType());

                    var newList = (IList)Activator.CreateInstance(constructedListType);

                    foreach (var item in cacheList.Values)
                    {
                        newList.Add(item);
                    }

                    isCanRender = false;
                    Data = newList;
                }
            };
            panel.OnRemoveAction = ReactiveCommand.Create(() =>
            {
                cacheList.Remove(id);
                var listType = typeof(List<>);
                var constructedListType = listType.MakeGenericType(panel.Data.GetType());

                var newList = (IList)Activator.CreateInstance(constructedListType);
                foreach (var item in cacheList.Values)
                {
                    newList.Add(item);
                }

                Container.Children.Remove(panel);
                isCanRender = false;
                Data = newList;
            });
            return panel;
        }

        private void RenderSinglePanel()
        {
            configData = Activator.CreateInstance(Data.GetType());

            // 遍历方法特性
            foreach (PropertyInfo pi in Data.GetType().GetProperties())
            {
                foreach (Attribute attr in pi.GetCustomAttributes(true))
                {
                    if (attr is ConfigAttribute)
                    {
                        ConfigAttribute attribute = (ConfigAttribute)attr;
                        if (attribute.CultureCode != SystemLanguage.CurrentLanguage) continue;

                        var config = new Config()
                        {
                            Attribute = attribute,
                            PropertyInfo = pi
                        };
                        if (string.IsNullOrEmpty(attribute.Group))
                        {
                            //  未分组
                            configList[nosetGroupKey].Add(config);
                        }
                        else
                        {
                            if (!configList.ContainsKey(attribute.Group))
                            {
                                configList.Add(attribute.Group, new List<Config>());
                            }
                            configList[attribute.Group].Add(config);
                        }


                    }
                }
            }

            //  渲染已分组的项目
            foreach (var item in configList)
            {
                if (item.Key != nosetGroupKey)
                {
                    RenderGroup(item.Value, item.Key);
                }
            }

            //  渲染未分组项目
            RenderGroup(configList[nosetGroupKey]);
        }

        private void RenderGroup(List<Config> configList, string groupName = null)
        {
            if (configList == null || configList.Count == 0)
            {
                return;
            }
            var itemContainer = new StackPanel();

            var sortList = configList.OrderBy(m => m.Attribute.Index).ToList();
            for (int i = 0; i < sortList.Count; i++)
            {
                var config = sortList[i];

                var control = RenderConfigItem(config.Attribute, config.PropertyInfo);
                if (control != null)
                {
                    itemContainer.Children.Add(control);

                    if (i != sortList.Count - 1)
                    {
                        //  添加分割线
                        var spliteLine = new Border();
                        spliteLine.Height = 1;
                        spliteLine.Background = SpliteLineBrush;
                        itemContainer.Children.Add(spliteLine);
                    }
                }


            }

            var groupControl = GetCreateGroupContainer(itemContainer, groupName);
            Container.Children.Add(groupControl);
        }

        private StackPanel GetCreateGroupContainer(Control item, string groupName = null)
        {
            var container = new StackPanel();
            container.Margin = new Thickness(0, 10, 0, 20);
            if (groupName != null)
            {
                var groupNameControl = new TextBlock();
                groupNameControl.Text = groupName;
                groupNameControl.FontSize = 14;
                groupNameControl.Margin = new Thickness(0, 0, 0, 10);
                container.Children.Add(groupNameControl);
            }

            var border = new Border();
            border.Background = Background;
            border.CornerRadius = new CornerRadius(6);
            border.BorderBrush = BorderBrush;
            //border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ededed"));
            border.BorderThickness = new Thickness(1);
            border.Child = item;

            container.Children.Add(border);

            return container;
        }

        private Control RenderConfigItem(ConfigAttribute attribute, PropertyInfo pi)
        {
            Control uIElement = null;
            if (pi.PropertyType == typeof(bool))
            {
                uIElement = RenderBooleanConfigControl(attribute, pi);
            }
            else if (pi.PropertyType == typeof(List<string>))
            {
                uIElement = RenderListStringConfigControl(attribute, pi);
            }
            else if (pi.PropertyType == typeof(int))
            {
                uIElement = RenderOptionsConfigControl(attribute, pi);
            }
            else if (pi.PropertyType == typeof(string))
            {
                if (pi.Name.LastIndexOf("Color") != -1)
                {
                    uIElement = RenderColorConfigControl(attribute, pi);
                }
            }
            return uIElement;
        }

        public static object DeepCopy(object obj, Type type)
        {
            if (obj == null)
            {
                return null;
            }

            string json = JsonConvert.SerializeObject(obj); ;
            return JsonConvert.DeserializeObject(json, type);
        }

        private Control RenderColorConfigControl(ConfigAttribute configAttribute, PropertyInfo pi)
        {
            var control = new Base.ColorSelect();
            control.Color = (string)pi.GetValue(Data);
            control.OnSelected += (e, c) =>
            {
                pi.SetValue(configData, control.Color);
                isCanRender = false;
                Data = DeepCopy(configData, configData.GetType());
            };

            var item = new SettingPanelItem();
            item.Init(configAttribute, control);

            pi.SetValue(configData, pi.GetValue(Data));
            return item;
        }

        private Control RenderOptionsConfigControl(ConfigAttribute configAttribute, PropertyInfo pi)
        {
            var control = new Select.Select();
            var optionsArr = configAttribute.Options.Split('|');

            var options = new List<SelectItemModel>();
            for (int i = 0; i < optionsArr.Length; i++)
            {
                var name = optionsArr[i];
                options.Add(new SelectItemModel()
                {
                    Name = name,
                    Data = i
                });
            }
            control.Options = options;
            control.IsShowIcon = false;
            control.SelectedItem = options[(int)pi.GetValue(Data)];
            control.Tag = this;
            control.OnSelectedItemChanged += (e, c) =>
            {
                pi.SetValue(configData, control.SelectedItem.Data);
                Data = DeepCopy(configData, configData.GetType());
                if (configAttribute.OptionsChangedRefresh)
                {
                    isCanRender = true;
                    Render();
                }
                else
                {
                    isCanRender = false;
                }
            
            };


            var item = new SettingPanelItem();
            item.Init(configAttribute, control);
            pi.SetValue(configData, pi.GetValue(Data));
            return item;
        }

        private Control RenderBooleanConfigControl(ConfigAttribute configAttribute, PropertyInfo pi)
        {
            var inputControl = new Toggle.Toggle();
            inputControl.OnText = configAttribute.ToggleTrueText;
            inputControl.OffText = configAttribute.ToggleFalseText;
            inputControl.ToggleChanged += (e, c) =>
            {
                pi.SetValue(configData, inputControl.IsChecked);

                isCanRender = false;
                Data = DeepCopy(configData, configData.GetType());
            };

            inputControl.IsChecked = (bool)pi.GetValue(Data);

            var item = new SettingPanelItem();
            item.Init(configAttribute, inputControl);

            pi.SetValue(configData, pi.GetValue(Data));
            return item;
        }
        private Control RenderListStringConfigControl(ConfigAttribute configAttribute, PropertyInfo pi)
        {
            var list = pi.GetValue(Data) as List<string>;

            var listControl = new BaseList();
            listControl.MaxHeight = 200;
            listControl.Loaded += (sender, args) =>
            {
                listControl.Items.CollectionChanged += (o, e) =>
                {
                    var newData = new List<string>();
                    foreach (var item in listControl.Items)
                    {
                        newData.Add(item.ToString());
                    }
                    pi.SetValue(configData, newData);
                    Data = DeepCopy(configData, configData.GetType());
                   
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
            var contextMenuItemCopy = new MenuItem();
            contextMenuItemCopy.Header = Application.Current.FindResource("CopyContent");
            contextMenuItemCopy.Click += (e, c) =>
            {
                var clipboard = TopLevel.GetTopLevel(this)!.Clipboard!;
                clipboard.SetTextAsync(listControl.SelectedItem)
                .ConfigureAwait(false).GetAwaiter();
            };
            contextMenu.Items.Add(contextMenuItemCopy);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(contextMenuItemDel);
            listControl.ContextMenu = contextMenu;


            //  添加输入框
            var addInputBox = new InputBox();
            addInputBox.GotFocus += (e, c) =>
            {
                var box = e as InputBox;
                box?.HideError();
            };
            addInputBox.Placeholder = configAttribute.Placeholder;
            addInputBox.Margin = new Thickness(0, 0, 10, 0);


            //添加
            var addBtn = new Button.Button();
            //addBtn.Margin = new Thickness(15, 0, 15, 10);
            addBtn.Content =  Application.Current.FindResource("Add");

            addBtn.Click += (e, c) =>
            {
                if (string.IsNullOrEmpty(addInputBox.Text) || list.Contains(addInputBox.Text))
                {
                    addInputBox.Error = configAttribute.Name + (string.IsNullOrEmpty(addInputBox.Text) ? Application.Current.FindResource("CannotBeEmpty") 
                        : Application.Current.FindResource("AlreadyExists"));
                    addInputBox.ShowError();
                    return;
                }
                //list.Add(addInputBox.Text);
                listControl.Items.Add(addInputBox.Text);
                addInputBox.Text = String.Empty;



            };
            pi.SetValue(configData, list);

            IconButton moreActionBtn = new IconButton();
            moreActionBtn.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            moreActionBtn.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            moreActionBtn.Margin = new Thickness(0, 5, 5, 0);
            moreActionBtn.Icon = Base.IconTypes.More;

            var moreActionMenu = new ContextMenu();
            moreActionMenu.HorizontalOffset = -15;
            moreActionMenu.VerticalOffset = 10;
            moreActionBtn.PointerPressed += (e, c) =>
            {
                if (!moreActionMenu.IsOpen)
                {
                    moreActionMenu.Open(e as Control);
                }
                else
                {
                    moreActionMenu.Close();
                }
            };
            bool isHasMoreAction = false;
            if (configAttribute.IsCanImportExport)
            {
                //  允许导入导出
                isHasMoreAction = true;

                //  导入操作
                var importMenuItem = new MenuItem();
                importMenuItem.Header = Application.Current.FindResource("Import");
                importMenuItem.Click += async (e, c) =>
                {
                    var storage = TopLevel.GetTopLevel(this).StorageProvider;
                    var result = await storage.OpenFilePickerAsync(new ()
                    {
                        AllowMultiple = false,
                        FileTypeFilter =
                        [
                            new FilePickerFileType("Json")
                            {
                                Patterns = [ "*.json"]
                            }
                        ]
                    });
                    if (result != null && result.Count > 0)
                    {
                        var view = ServiceLocator.GetRequiredService<MainViewModel>();
                        try
                        {
                            var data = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(result.First().Path.LocalPath))!;
                            if (data == null)
                            {
                                view.Toast(Application.Current.FindResource("InvalidFileContent") as string,ToastType.Error);
                                return;
                            }

                           var isConfirm = await ServiceLocator.GetRequiredService<IUIServicer>().ShowConfirmDialogAsync(
                                Application.Current.FindResource("PleaseNote") as string,
                                Application.Current.FindResource("ImportingOverwriteOriginal") as string);

                            if (isConfirm)
                            {
                                pi.SetValue(configData, data);
                                listControl.Items.Clear();
                                foreach (string item in data)
                                {
                                    listControl.Items.Add(item);
                                }
                                view.Toast(Application.Current.FindResource("ImportCompleted") as string,ToastType.Success);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"导入配置“{configAttribute.Name}”时失败：{ex.Message}");
                            view.Toast(Application.Current.FindResource("ImportFailed") as string, ToastType.Error);
                        }
                    }
                };

                //  导出操作
                var exportMenuItem = new MenuItem();
                exportMenuItem.Header = Application.Current.FindResource("Export");
                exportMenuItem.Click += async (e, c) =>
                {
                    var view = ServiceLocator.GetRequiredService<MainViewModel>();
                    try
                    {
                        var deskLifettime =
                            Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                        var storage = deskLifettime.MainWindow.StorageProvider;
                        var result = await storage.SaveFilePickerAsync(new()
                        {
                            DefaultExtension = "json",
                        });
                        if (result != null)
                        {
                            File.WriteAllText(result.Path.LocalPath, JsonConvert.SerializeObject(listControl.Items));
                        }
                    }
                    catch (Exception ex)
                    { 
                        Logger.Error($"导出配置“{configAttribute.Name}”时失败：{ex.Message}");
                       view.Toast(Application.Current.FindResource("ExportFailed") as string, ToastType.Error);
                    }
                };


                moreActionMenu.Items.Add(importMenuItem);
                moreActionMenu.Items.Add(exportMenuItem);
            }


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

            //  标题和说明

            var description = new TextBlock();
            description.Text = configAttribute.Description;
            description.Margin = new Thickness(10, 10, 10, 0);
            description.Foreground = new SolidColorBrush(Color.Parse("#989CA1"));
            var container = new StackPanel();

            var head = new Grid();
            head.ColumnDefinitions.Add(
                 new ColumnDefinition()
                 {
                     Width = new GridLength(10, GridUnitType.Star)
                 });
            head.ColumnDefinitions.Add(
               new ColumnDefinition()
               {
                   Width = new GridLength(2, GridUnitType.Star)
               });
            Grid.SetColumn(description, 0);
            head.Children.Add(description);

            //  更多操作按钮
            if (isHasMoreAction)
            {
                Grid.SetColumn(moreActionBtn, 1);
                head.Children.Add(moreActionBtn);
            }

            container.Children.Add(head);
            container.Children.Add(inputPanel);
            container.Children.Add(listControl);
            return container;
        }

    }
}
