using SharedLibrary.Librarys;
using SharedLibrary.Servicers;
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
            const string appName = "Taix";
            const string runKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

            try
            {
                using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(runKeyPath, true)
                    ?? Registry.CurrentUser.CreateSubKey(runKeyPath))
                {
                    string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Taix.exe");

                    string expectedValue = $"\"{exePath}\" --selfStart";

                    string currentValue = runKey.GetValue(appName) as string;

                    if (currentValue != expectedValue)
                    {
                        runKey.SetValue(appName, expectedValue, RegistryValueKind.String);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"SetAutoStartInRegistry failed: {ex.Message}");
                return false;
            }
        }
    }
}
