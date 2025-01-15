using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI
{
    public static class ResourceStrings
    {

        public static string Daily => GetStringValue("Daily");
        public static string Weekly => GetStringValue("Weekly");
        public static string Monthly => GetStringValue("Monthly");
        public static string Yearly => GetStringValue("Yearly");
        public static string LastWeek => GetStringValue("LastWeek");
        public static string ThisWeek => GetStringValue("ThisWeek");
        public static string DefaultView => GetStringValue("DefaultView");
        public static string SummaryView => GetStringValue("SummaryView");
        public static string CategoryView => GetStringValue("CategoryView");
        public static string App => GetStringValue("App");
        public static string Website => GetStringValue("Website");
        public static string Uncategorized => GetStringValue("Uncategorized");
        public static string General => GetStringValue("General");
        public static string Behavior => GetStringValue("Behavior");
        public static string Data => GetStringValue("Data");
        public static string About => GetStringValue("About");
      

        private static string GetStringValue(string key)
        {
            return Application.Current.FindResource(key) as string;
        }
    }
}
