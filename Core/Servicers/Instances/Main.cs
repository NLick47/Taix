using SharedLibrary.Enums;
using SharedLibrary.Event;
using SharedLibrary.Librarys;
using SharedLibrary.Servicers;
using Core.Librarys;
using Core.Librarys.Browser;
using Core.Librarys.Browser.Favicon;
using Core.Librarys.SQLite;
using Core.Models;
using Core.Models.Config;
using Core.Models.Config.Link;
using Core.Servicers.Interfaces;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Microsoft.EntityFrameworkCore;

namespace Core.Servicers.Instances
{
    public class Main : IMain
    {
        private readonly IAppObserver _appObserver;
        private readonly IData _data;
        private readonly ISleepdiscover _sleepdiscover;
        private readonly IAppConfig _appConfig;
        private readonly IAppData _appData;
        private readonly ICategorys _categories;
        private readonly IWebFilter _webFilter;
        private readonly IAppTimerServicer _appTimer;
        private readonly IWebServer _webServer;
        private readonly IWebData _webData;

        private readonly ISystemInfrastructure _systemInfrastructure;
        //  忽略的进程
        private readonly HashSet<string> DefaultIgnoreProcess = new() {
            "Tai",
            "Taix",
            "SearchHost",
            "Taskmgr",
            "ApplicationFrameHost",
            "StartMenuExperienceHost",
            "ShellExperienceHost",
            "OpenWith",
            "Updater",
            "LockApp",
            "dwm",
            "SystemSettingsAdminFlows"
        };

        /// <summary>
        /// 睡眠状态
        /// </summary>
        private SleepStatus sleepStatus;

        /// <summary>
        /// app config
        /// </summary>
        private ConfigModel config;

        public event EventHandler OnUpdateTime;
        public event EventHandler OnStarted;
        public event EventHandler OnConfigLoaded;

        /// <summary>
        /// 忽略进程缓存列表
        /// </summary>
        private List<string> _ignoreProcessCacheList;

        /// <summary>
        /// 配置正则忽略进程列表
        /// </summary>
        private List<string> _configIgnoreProcessRegxList;
        /// <summary>
        /// 配置忽略进程名称列表
        /// </summary>
        private List<string> _configIgnoreProcessList;
        //  更新应用日期
        private DateTime _updadteAppDateTime = DateTime.Now.Date;
        //  已经更新过的应用列表
        private List<string> _updatedAppList = new List<string>();
        private List<string> _configProcessNameWhiteList, _configProcessRegexWhiteList;
        public Main(
            IAppObserver appObserver,
            IData data,
            ISleepdiscover sleepdiscover,
            IAppConfig appConfig,
            IDateTimeObserver dateTimeObserver,
            IAppData appData, ICategorys categories,
            IWebFilter webFilter,
            IAppTimerServicer appTimer,
            IWebServer webServer,
            ISystemInfrastructure systemInfrastructure,
            IWebData webData)
        {
            _appObserver = appObserver;
            _data = data;
            _sleepdiscover = sleepdiscover;
            _appConfig = appConfig;
            _appData = appData;
            _categories = categories;
            _webFilter = webFilter;
            _appTimer = appTimer;
            _webServer = webServer;
            _webData = webData;
            _systemInfrastructure = systemInfrastructure;
            _ignoreProcessCacheList = new List<string>();
            _configIgnoreProcessRegxList = new List<string>();
            _configIgnoreProcessList = new List<string>();
            _configProcessNameWhiteList = new List<string>();
            _configProcessRegexWhiteList = new List<string>();

            if (!Design.IsDesignMode)
            {
                sleepdiscover.SleepStatusChanged += Sleepdiscover_SleepStatusChanged;
                appConfig.ConfigChanged += AppConfig_ConfigChanged;
                _appTimer.OnAppDurationUpdated += _appTimer_OnAppDurationUpdated;
                WebSocketEvent.OnWebLog += WebSocketEvent_OnWebLog;
            }
        }

        private void AppConfig_ConfigChanged(ConfigModel oldConfig, ConfigModel newConfig)
        {
            if (oldConfig != newConfig)
            {
                _systemInfrastructure.SetStartup(newConfig.General.IsStartatboot);
                //  更新忽略规则
                UpdateConfigIgnoreProcess();

                //  更新白名单
                UpdateConfigProcessWhiteList();

                //  处理web记录功能启停
                HandleWebServiceConfig();

                
            }
        }

        public async Task Run()
        {

            try
            {
                CreateDirectory();
                using TaiDbContext taiDb = new TaiDbContext();
                var tableExists = taiDb.Database
                    .ExecuteSqlRaw("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'") == 1;
                string _dbFilePath = Path.Combine(FileHelper.GetRootDirectory(), "Data", "data.db");
                bool fileExistsOrEmpty = File.Exists(_dbFilePath) && new FileInfo(_dbFilePath).Length == 0;
                if (fileExistsOrEmpty || tableExists)
                {
                    await taiDb.Database.MigrateAsync();
                }
                //  加载app信息
                await _appData.LoadAsync();

                // 加载分类信息
                await _categories.LoadAsync();

                AppState.IsLoading = false;


                _appConfig.Load();
                config = _appConfig.GetConfig();
                OnConfigLoaded?.Invoke(this,EventArgs.Empty);

                if (!Design.IsDesignMode)
                {
                    UpdateConfigIgnoreProcess();
                    UpdateConfigProcessWhiteList();
                    //  初始化过滤器
                    _webFilter.Init();

                    //  启动主服务
                    Start();
                }
                
                OnStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception e)
            {
                AppState.IsLoading = false;
                throw;
            }
        }
        public void Start()
        {
            //  appTimer必须比Observer先启动*
            _appTimer.Start();
            _appObserver.Start();
            if (config.General.IsWebEnabled)
            {
                _webServer.Start();
            }
            if (config.Behavior.IsSleepWatch)
            {
                //  启动睡眠监测
                _sleepdiscover.Start();
            }
        }
        public void Stop()
        {
            _appObserver.Stop();
            _appTimer.Stop();
            _webServer.Stop();
        }
        public void Exit()
        {
            _appObserver?.Stop();
        }

        /// <summary>
        /// 创建程序目录
        /// </summary>
        private void CreateDirectory()
        {
            string dir = Path.Combine(FileHelper.GetRootDirectory(), "Data");
            Directory.CreateDirectory(dir);
        }

        private void UpdateConfigIgnoreProcess()
        {
            if (config == null)
            {
                return;
            }
            _configIgnoreProcessList.Clear();
            _configIgnoreProcessRegxList.Clear();
            _ignoreProcessCacheList.Clear();

            _configIgnoreProcessList = config.Behavior.IgnoreProcessList.Where(m => !IsRegex(m)).ToList();
            _configIgnoreProcessRegxList = config.Behavior.IgnoreProcessList.Where(m => IsRegex(m)).ToList();
        }

        private void UpdateConfigProcessWhiteList()
        {
            if (config == null)
            {
                return;
            }
            _configProcessNameWhiteList.Clear();
            _configProcessRegexWhiteList.Clear();

            _configProcessNameWhiteList = config.Behavior.ProcessWhiteList.Where(m => !IsRegex(m)).ToList();
            _configProcessRegexWhiteList = config.Behavior.ProcessWhiteList.Where(m => IsRegex(m)).ToList();
        }

        private bool IsRegex(string str)
        {
            return Regex.IsMatch(str, @"[\.|\*|\?|\{|\\|\[|\^|\|]");
        }

        private void Sleepdiscover_SleepStatusChanged(SleepStatus sleepStatus)
        {
            this.sleepStatus = sleepStatus;

            Logger.Info($"[{sleepStatus}]");
            if (sleepStatus == SleepStatus.Sleep)
            {
                //  进入睡眠状态
                Debug.WriteLine("进入睡眠状态");

                //  通知sokcet客户端
                _webServer?.SendMsg("sleep");
                //  停止服务
                Stop();

                //  更新时间
                UpdateAppDuration();
            }
            else
            {
                //  从睡眠状态唤醒
                Debug.WriteLine("从睡眠状态唤醒");

                _webServer?.SendMsg("wake");

                Start();
            }
        }


        /// <summary>
        /// 检查应用是否需要记录数据
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="description"></param>
        /// <param name="file"></param>
        private bool IsCheckApp(string processName, string description, string file)
        {
            if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(processName) || DefaultIgnoreProcess.Contains(processName) || _ignoreProcessCacheList.Contains(processName))
            {
                return false;
            }

            //  从名称判断
            if (_configIgnoreProcessList.Contains(processName))
            {
                return false;
            }

            //  正则表达式
            foreach (string reg in _configIgnoreProcessRegxList)
            {
                if (RegexHelper.IsMatch(processName, reg) || RegexHelper.IsMatch(file, reg))
                {
                    _ignoreProcessCacheList.Add(processName);
                    return false;
                }
            }

            //  应用白名单过滤
            if (config.Behavior.IsWhiteList && config.Behavior.ProcessWhiteList.Count > 0)
            {
                bool isWhite = false;
                //  通过进程名称判断
                if (_configProcessNameWhiteList.Contains(processName))
                {
                    isWhite = true;
                }
                else
                {
                    //  进程名称中匹配不到时尝试正则表达式
                    foreach (string reg in _configProcessRegexWhiteList)
                    {
                        if (RegexHelper.IsMatch(processName, reg) || RegexHelper.IsMatch(file, reg))
                        {
                            isWhite = true;
                            break;
                        }
                    }
                }
                //  白名单中找不到时不统计
                Debug.WriteLine("白名单过滤结果：" + processName + " -> " + isWhite);
                if (!isWhite) return false;
            }

            AppModel app = _appData.GetApp(processName);
            if (app == null)
            {
                //  记录应用信息

                //  提取icon
                string iconFile = Iconer.ExtractFromFile(file, processName, description);

                _appData.AddApp(new AppModel()
                {
                    Name = processName,
                    Description = description,
                    File = file,
                    CategoryID = 0,
                    IconFile = iconFile,
                });
            }
            else
            {
                if (_updadteAppDateTime != DateTime.Now.Date)
                {
                    _updadteAppDateTime = DateTime.Now.Date;
                    _updatedAppList.Clear();
                }

                if (!_updatedAppList.Contains(processName))
                {
                    //  更新应用信息
                    app.IconFile = Iconer.ExtractFromFile(file, processName, description);

                    if (app.Description != description)
                    {
                        app.Description = description;
                    }
                    if (app.File != file)
                    {
                        app.File = file;
                    }
                    _appData.UpdateApp(app);
                    _updatedAppList.Add(processName);
                }
            }

            return true;
        }

        private void HandleLinks(string processName, int seconds, DateTime time)
        {
            Task.Run(async () =>
            {
                try
                {
                    List<LinkModel> links = config.Links != null ? config.Links : new List<LinkModel>();
                    foreach (LinkModel link in links)
                    {
                        if (link.ProcessList != null
                            && link.ProcessList.Count >= 2
                            && link.ProcessList.Contains(processName))
                        {
                            //  属于关联进程
                            foreach (string linkProcess in link.ProcessList)
                            {
                                if (linkProcess != processName)
                                {
                                    if (IsProcessRuning(linkProcess))
                                    {
                                        //  同步更新
                                        await _data.UpdateAppDurationAsync(linkProcess, seconds, time);
                                    }

                                }
                            }
                            break;
                        }
                    }

                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message + "，关联进程更新错误，Process Name: " + processName + "，Time: " + seconds);
                }
            });
        }

        #region 判断进程是否在运行中
        /// <summary>
        /// 判断进程是否在运行中
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        private bool IsProcessRuning(string processName)
        {
            Process[] process = Process.GetProcessesByName(processName);
            return process != null && process.Length > 0;
        }
        #endregion

        #region 处理网站数据记录配置项开关
        /// <summary>
        /// 处理网站数据记录配置项开关
        /// </summary>
        private void HandleWebServiceConfig()
        {
            if (config == null)
            {
                return;
            }

            if (config.General.IsWebEnabled)
            {
                _webServer.Start();
            }
            else
            {
                _webServer.Stop();
            }
        }
        #endregion

        private void _appTimer_OnAppDurationUpdated(object sender, AppDurationUpdatedEventArgs e)
        {
            UpdateAppDuration(e);
        }

        private void UpdateAppDuration()
        {
            UpdateAppDuration(_appTimer.GetAppDuration());
        }
        private async void UpdateAppDuration(AppDurationUpdatedEventArgs e)
        {
            if (e == null) return;

            try
            {
                var app = e.App;
                int duration = e.Duration;
                DateTime startTime = e.ActiveTime;

                bool isCheck = IsCheckApp(app.Process, app.Description, app.ExecutablePath);
                if (isCheck)
                {
                    //  更新统计时长
                    await _data.UpdateAppDurationAsync(app.Process, duration, startTime);
                    //  关联进程更新
                    HandleLinks(app.Process, duration, startTime);
                    OnUpdateTime?.Invoke(this, null);
                    //  自动分类
                    DispatchCateogry(app.Process, app.ExecutablePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        #region 浏览器记录
        private void WebSocketEvent_OnWebLog(Models.WebPage.NotifyWeb args)
        {
            try
            {
                if (_webFilter.IsIgnore(args.Url))
                {
                    Debug.WriteLine($"URL已被过滤，{args.Url}");
                    return;
                }

                //  记录数据
                var site = new Models.WebPage.Site()
                {
                    Url = args.Url,
                    Title = args.Title
                };

                //  处理图标
                Task.Run(async () =>
                {
                    await _webData.AddUrlBrowseTimeAsync(site, args.Duration, args.ActiveDateTime);
                    string saveName = UrlHelper.GetName(args.Url) + DateTime.Now.ToString("yyyyMM");
                    string path = await FaviconDownloader.DownloadAsync(args.Icon, saveName);
                    await _webData.UpdateUrlFaviconAsync(site, path);
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }
        #endregion

        #region 自动分类
        /// <summary>
        /// 自动分类
        /// </summary>
        /// <param name="processName_">进程名称</param>
        private void DispatchCateogry(string processName_, string executablePath_)
        {
            try
            {
                AppModel app = _appData.GetApp(processName_);
                if (app != null)
                {
                    var categoryList = _categories.GetCategories().Where(c => c.IsDirectoryMath && c.DirectoryList.Count > 0).ToList();
                    CategoryModel mathCategory = null;
                    foreach (var category in categoryList)
                    {
                        if (mathCategory != null)
                        {
                            break;
                        }
                        foreach (var item in category.DirectoryList)
                        {
                            string path = item.Replace("\\", "\\\\");
                            if (Regex.IsMatch(executablePath_, @"^" + path))
                            {
                                mathCategory = category;
                                Debug.WriteLine("匹配成功：" + category.Name);
                                break;
                            }
                        }

                    }
                    if (mathCategory != null)
                    {
                        //  匹配成功
                        app.Category = mathCategory;
                        app.CategoryID = mathCategory.ID;
                        _appData.UpdateApp(app);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }
        #endregion
    }
}
