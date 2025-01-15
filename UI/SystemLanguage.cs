using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Core.Enums;
using Core.Servicers.Interfaces;
using NPOI.SS.Formula.Functions;
using NPOI.XSSF.Streaming.Values;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Locale;
using UI.Resources.Localization;

namespace UI
{
    internal class SystemLanguage
    {
        private static readonly Dictionary<CultureInfo, CultureCode> _cultures = new()
        {
            { new CultureInfo("zh-CN"), CultureCode.ZhCn },
            { new CultureInfo("en-US"), CultureCode.EnUs }
        };

        private static readonly Dictionary<CultureCode, ResourceDictionary> _controlsLocaleUri = new()
        {
            { CultureCode.ZhCn,new zh_cn()},
            { CultureCode.EnUs,new en_us() },
        };

        private static readonly Dictionary<CultureCode, ResourceDictionary> _pageLocaleUri = new()
        {
            { CultureCode.ZhCn,new StringResourcesCn()},
            { CultureCode.EnUs,new StringResourcesEn() },
        };

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

        private static bool _isInitialized = false;

        private static CultureCode _currentLanguage;



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
        public static void InitializedLanguage(CultureCode culture)
        {
            if(culture == CultureCode.Auto)
            {
                var lan = GetCurrentSystemLanguage();
                culture = _cultures[lan];
            }
            if (_isInitialized)
            {
                throw new Exception("Language has been initialized");
            }
            _isInitialized = true;
            Application.Current.Resources.MergedDictionaries.Add(_pageLocaleUri[culture]);
            Application.Current.Resources.MergedDictionaries.Add(_controlsLocaleUri[culture]);
          
            _currentLanguage = culture;
        }




        public static void SetCurrentSystemLanguage(CultureCode culture)
        {
            if (_currentLanguage != culture)
            {
                if (culture == CultureCode.Auto)
                {
                    var lan = GetCurrentSystemLanguage();
                    culture = _cultures[lan];
                }

                foreach (var pageLocale in _pageLocaleUri[culture])
                {
                    Application.Current.Resources[pageLocale.Key] = pageLocale.Value;
                }
                foreach (var controlsLocale in _controlsLocaleUri[culture])
                {
                    Application.Current.Resources[controlsLocale.Key] = controlsLocale.Value;
                }
                _currentLanguage = culture;
            }
        }
    }
}
