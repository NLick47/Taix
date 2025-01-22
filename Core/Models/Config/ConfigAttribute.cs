using SharedLibrary.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.Config
{
 
    [AttributeUsage(AttributeTargets.Class |
 AttributeTargets.Constructor |
 AttributeTargets.Field |
 AttributeTargets.Method |
 AttributeTargets.Property, AllowMultiple = true)]
 
    public class ConfigAttribute : System.Attribute
    {

        public string Name;
        public string Description;
        public string ToggleTrueText;
        public string ToggleFalseText;
        public string Group;
        public int Index;
        public bool IsName;
        public string Placeholder;
        public bool IsCanRepeat;
        public bool IsCanImportExport;
        public string Options;
        public bool IsBeta;
        public CultureCode CultureCode = CultureCode.ZhCn;
        public bool OptionsChangedRefresh = false;
        public ConfigAttribute()
        {
            ToggleTrueText ??= "开";
            ToggleFalseText ??= "关";
            IsCanRepeat = true;
            Index = 0;
            IsName = false;
            IsCanImportExport = false;
            Options = string.Empty;
            IsBeta = false;
        }


    }
}
