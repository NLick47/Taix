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
        public static string Refresh => GetStringValue("Refresh");
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
        public static string Year => GetStringValue("Year");
        public static string Month => GetStringValue("Month");
        public static string Day => GetStringValue("Day");
        public static string Hour => GetStringValue("Hour");
        public static string Monday => GetStringValue("Monday");
        public static string Tuesday => GetStringValue("Tuesday");
        public static string Wednesday => GetStringValue("Wednesday");
        public static string Thursday => GetStringValue("Thursday");
        public static string Friday => GetStringValue("Friday");
        public static string Saturday => GetStringValue("Saturday");
        public static string Sunday => GetStringValue("Sunday");
        public static string StartApplication => GetStringValue("StartApplication");
        public static string CopyApplicationProcessName => GetStringValue("CopyApplicationProcessName");
        public static string CopyApplicationFilePath => GetStringValue("CopyApplicationFilePath");
        public static string OpenApplicationDirectory => GetStringValue("OpenApplicationDirectory");
        public static string ClearStatistics => GetStringValue("ClearStatistics");
        public static string SetCategory => GetStringValue("SetCategory");
        public static string EditAlias => GetStringValue("EditAlias");
        public static string RemovedApplicationFromWhitelist => GetStringValue("RemovedApplicationFromWhitelist");
        public static string AddedToWhitelist => GetStringValue("AddedToWhitelist");
        public static string UpdateAlias => GetStringValue("UpdateAlias");
        public static string EnterAlias => GetStringValue("EnterAlias");
        public static string AliasMaxLengthTip => GetStringValue("AliasMaxLengthTip");
        public static string AliasUpdated => GetStringValue("AliasUpdated");
        public static string IgnoreThisApplication => GetStringValue("IgnoreThisApplication");
        public static string AddWhitelist => GetStringValue("AddWhitelist");
        public static string AddAssociation => GetStringValue("AddAssociation");
        public static string OperationCompleted => GetStringValue("OperationCompleted");
        public static string ApplicationFileExist => GetStringValue("ApplicationFileExist");
        public static string Unignore => GetStringValue("Unignore");
        public static string RemoveWhitelist => GetStringValue("RemoveWhitelist");
        public static string AssociationSuccessful => GetStringValue("AssociationSuccessful");
        public static string AssociationConfigurationNotExist => GetStringValue("AssociationConfigurationNotExist");
        public static string IgnoringApplicationCancelled => GetStringValue("IgnoringApplicationCancelled");
        public static string ApplicationNowIgnored => GetStringValue("ApplicationNowIgnored");
        public static string OpenWebsite => GetStringValue("OpenWebsite");
       
        public static string UnignoreSite => GetStringValue("UnignoreSite");
        public static string IgnoreSite => GetStringValue("IgnoreSite");
        public static string UnignoredDomain => GetStringValue("UnignoredDomain");
        public static string IgnoredDomain => GetStringValue("IgnoredDomain");


        private static string GetStringValue(string key)
        {
            return Application.Current.FindResource(key) as string;
        }
    }
}
