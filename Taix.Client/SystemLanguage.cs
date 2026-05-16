using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Taix.Client.Logging;
using Taix.Client.Platform.Abstractions.Primitives;
using Taix.Client.Resources.Localization;
using Taix.Client.Shared.Event;
using Taix.Client.Shared.Servicers.Interfaces;

namespace Taix.Client;

public class SystemLanguage
{
    private static readonly Dictionary<CultureInfo, CultureCode> Cultures = new()
    {
        { new CultureInfo("zh-CN"), CultureCode.ZhCn },
        { new CultureInfo("en-US"), CultureCode.EnUs }
    };


    private static readonly Dictionary<CultureCode, Func<ResourceDictionary>> PageLocaleUri = new()
    {
        { CultureCode.ZhCn, () => new StringResourcesCn() },
        { CultureCode.EnUs, () => new StringResourcesEn() }
    };

    private static readonly Dictionary<CultureCode, CultureInfo> CultureCodeToCultureInfo = new()
    {
        { CultureCode.ZhCn, new CultureInfo("zh-CN") },
        { CultureCode.EnUs, new CultureInfo("en-US") }
    };

    private static bool _isInitialized;
    private static CultureCode _currentLanguage;

    public static CultureInfo CurrentCultureInfo => ConvertToCultureInfo(_currentLanguage);

    public static CultureCode CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value) SetCurrentSystemLanguage(value);
        }
    }

    private static IAppConfig? _appConfig;
    private static EventHandler<ConfigChangedEventArgs>? _configChangedHandler;

    /// <summary>
    /// 将 SystemLanguage 与配置系统关联，后续配置中的语言变更会自动同步。
    /// </summary>
    public static void AttachConfig(IAppConfig appConfig)
    {
        if (_appConfig != null && _configChangedHandler != null)
        {
            _appConfig.ConfigChanged -= _configChangedHandler;
        }

        _appConfig = appConfig;
        _configChangedHandler = (s, e) =>
        {
            if (e.HasChange("General.Language"))
            {
                try
                {
                    CurrentLanguage = (CultureCode)e.NewConfig.General.Language;
                }
                catch (Exception ex)
                {
                    Logger.Error($"切换语言失败: {ex.Message}", ex);
                }
            }
        };
        _appConfig.ConfigChanged += _configChangedHandler;
    }

    public static CultureInfo GetCurrentSystemLanguage()
    {
        if (Cultures.TryGetValue(CultureInfo.CurrentUICulture, out var culture)) return CultureInfo.CurrentUICulture;

        return new CultureInfo("zh-CN");
    }

    public static CultureInfo ConvertToCultureInfo(CultureCode culture)
    {
        if (culture == CultureCode.Auto) return GetCurrentSystemLanguage();
        if (CultureCodeToCultureInfo.TryGetValue(culture, out var cultureInfo)) return cultureInfo;
        throw new ArgumentException("Invalid CultureCode");
    }

    public static void InitializeLanguage(CultureCode culture)
    {
        if (culture == CultureCode.Auto) culture = Cultures[GetCurrentSystemLanguage()];
        if (_isInitialized) throw new Exception("Language has been initialized");
        _isInitialized = true;
        SetCurrentSystemLanguage(culture);
    }

    private static void SetCurrentSystemLanguage(CultureCode culture)
    {
        if (culture == CultureCode.Auto) culture = Cultures[GetCurrentSystemLanguage()];
        if (culture == _currentLanguage) return;

        if (PageLocaleUri.TryGetValue(culture, out var factory))
        {
            var pageLocale = factory();
            foreach (var pageLocaleItem in pageLocale)
                Application.Current.Resources[pageLocaleItem.Key] = pageLocaleItem.Value;
            _currentLanguage = culture;
        }
        else
        {
            throw new ArgumentException("Invalid CultureCode");
        }
    }
}