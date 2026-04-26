using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Taix.Client.Platform.Abstractions.Primitives;
using Taix.Client.Resources.Localization;

namespace Taix.Client;

public class SystemLanguage
{
    private static readonly Dictionary<CultureInfo, CultureCode> Cultures = new()
    {
        { new CultureInfo("zh-CN"), CultureCode.ZhCn },
        { new CultureInfo("en-US"), CultureCode.EnUs }
    };


    private static readonly Dictionary<CultureCode, ResourceDictionary> PageLocaleUri = new()
    {
        { CultureCode.ZhCn, new StringResourcesCn() },
        { CultureCode.EnUs, new StringResourcesEn() }
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

    public static event EventHandler CurrentLanguageChanged;

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

        if (PageLocaleUri.TryGetValue(culture, out var pageLocale))
        {
            foreach (var pageLocaleItem in pageLocale)
                Application.Current.Resources[pageLocaleItem.Key] = pageLocaleItem.Value;
            _currentLanguage = culture;
            CurrentLanguageChanged?.Invoke(null, EventArgs.Empty);
        }
        else
        {
            throw new ArgumentException("Invalid CultureCode");
        }
    }
}