using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.IO;
using System.Linq;

namespace UI
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
#if DEBUG
            DebugMovePlatfromDll();
#endif
            BuildAvaloniaApp()
           .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI();

#if DEBUG
        private static void DebugMovePlatfromDll()
        {
            string[] platforms = new []{"Win","Mac","Linux" };
            string directory = AppContext.BaseDirectory;

            //var platformDllsMissing = platforms
            //    .Where(x => !File.Exists(Path.Combine(directory,string.Concat(x,".dll")))).ToList();
            //if (platformDllsMissing.Count == 0) return;
            while (!string.IsNullOrEmpty(directory))
            {
                if (Directory.Exists(Path.Combine(directory, "Platform")))
                {
                    directory = Path.Combine(directory, "Platform");
                    break;
                }
                directory = Directory.GetParent(directory)?.FullName;
            }
            if(string.IsNullOrEmpty(directory))
            {
                throw new DirectoryNotFoundException(directory);
            }

            foreach (var platfromName in platforms)
            {
                var dll = string.Concat(platfromName, ".dll");
                var targetFilePath = Path.Combine(directory,platfromName,"bin","Debug","net8.0", dll);
                if(!File.Exists(targetFilePath)) throw new FileNotFoundException(targetFilePath);
                File.Copy(targetFilePath,Path.Combine(AppContext.BaseDirectory, dll),true);
            }
        }
#endif
    }
}
