using Infrastructure.Librarys;
using Infrastructure.Servicers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        public bool SetAutoStartInRegistry()
        {
            try
            {
                var AppName = "Taix";
                var Key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (Key == null)
                {
                    Key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                }

                var AppPath = string.Concat(AppDomain.CurrentDomain.BaseDirectory, "Taix.exe --selfStart");
                var ExistingValue = Key.GetValue(AppName) as string;

                if (ExistingValue != $"\"{AppPath}\"")
                {
                    Key.SetValue(AppName, $"\"{AppPath}\"");
                }
            }
            catch (Exception e)
            {
                Logger.Error("SetAutoStartInRegistry" + e.Message);
                return false;
            }
            return true;
        }
    }
}
