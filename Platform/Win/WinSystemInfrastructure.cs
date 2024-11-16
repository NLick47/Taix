using Infrastructure.Servicers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Win
{
    public class WinSystemInfrastructure : ISystemInfrastructure
    {
        public (string ostype, string version) GetOSVersionName()
        {
            return (string.Empty, string.Empty);
        }

        public bool SetStartup(bool startup = true)
        {
            string appName = "UI";
            string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                key.SetValue(appName, "\"" + appPath + "\"");
            }
            catch (Exception e)
            {

                return false;
            }
            return true;
        }
    }
}
