using Avalonia;
using Avalonia.Controls;
using Core.Enums;
using System.Globalization;
using UI.Resources.Localization;

namespace SharedLibrary
{
    public class SystemLanguage
    {
        private static readonly Dictionary<CultureInfo, CultureCode> _cultures = new()
    {
        { new CultureInfo("zh-CN"), CultureCode.ZhCn },
        { new CultureInfo("en-US"), CultureCode.EnUs }
    };


        private static readonly Dictionary<CultureCode, ResourceDictionary> _pageLocaleUri = new()
    {
        { CultureCode.ZhCn, new StringResourcesCn() },
        { CultureCode.EnUs, new StringResourcesEn() },
    };

        private static readonly Dictionary<CultureCode, CultureInfo> _cultureCodeToCultureInfo = new()
    {
        { CultureCode.ZhCn, new CultureInfo("zh-CN") },
        { CultureCode.EnUs, new CultureInfo("en-US") }
    };

        private static bool _isInitialized = false;
        private static CultureCode _currentLanguage;

        public static CultureInfo GetCurrentSystemLanguage()
        {
            if (_cultures.TryGetValue(CultureInfo.CurrentUICulture, out var culture))
            {
                return CultureInfo.CurrentUICulture;
            }
            else
            {
                return new CultureInfo("zh-CN");
            }
        }

        public static CultureInfo ConvertToCultureInfo(CultureCode culture)
        {
            if (culture == CultureCode.Auto)
            {
                return GetCurrentSystemLanguage();
            }
            if (_cultureCodeToCultureInfo.TryGetValue(culture, out var cultureInfo))
            {
                return cultureInfo;
            }
            throw new ArgumentException("Invalid CultureCode");
        }

        public static CultureInfo CurrentCultureInfo { get => ConvertToCultureInfo(_currentLanguage); }
        public static CultureCode CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    SetCurrentSystemLanguage(value);
                }
            }
        }

        public static void InitializeLanguage(CultureCode culture)
        {
            if (culture == CultureCode.Auto)
            {
                culture = _cultures[GetCurrentSystemLanguage()];
            }
            if (_isInitialized)
            {
                throw new Exception("Language has been initialized");
            }
            _isInitialized = true;
            SetCurrentSystemLanguage(culture);
        }

        private static void SetCurrentSystemLanguage(CultureCode culture)
        {
            if (culture == CultureCode.Auto)
            {
                culture = _cultures[GetCurrentSystemLanguage()];
            }
            if(culture == _currentLanguage) return; 

            if ( _pageLocaleUri.TryGetValue(culture, out var pageLocale))
            {
                foreach (var pageLocaleItem in pageLocale)
                {
                    Application.Current.Resources[pageLocaleItem.Key] = pageLocaleItem.Value;
                }
                _currentLanguage = culture;
            }
            else
            {
                throw new ArgumentException("Invalid CultureCode");
            }
        }
    }
}
