using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Librarys
{
    public static class PlatformInfo
    {
        public static string GetPlatformName()
        {
            string[] platforms = new[] { "Win", "Mac", "Linux" };
            string platformName = string.Empty;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                platformName = platforms[0];
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                platformName = platforms[1];
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                platformName = platforms[2];
            }
            return platformName;
        }
    }
}
