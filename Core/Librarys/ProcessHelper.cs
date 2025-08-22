using System.Diagnostics;
using SharedLibrary.Librarys;

namespace Core.Librarys;

public class ProcessHelper
{
    public static bool Run(string filename, string[] args)
    {
        try
        {
            var arguments = "";
            foreach (var arg in args) arguments += $"\"{arg}\" ";
            arguments = arguments.Trim();
            using var process = new Process();
            var startInfo = new ProcessStartInfo(filename, arguments);
            process.StartInfo = startInfo;
            process.Start();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
            return false;
        }
    }
}