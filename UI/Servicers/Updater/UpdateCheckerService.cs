using Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace UI.Servicers.Updater
{
    public class UpdateCheckerService
    {
        private readonly IServiceProvider _serviceProvider;

        public UpdateCheckerService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task CheckForUpdatesAsync()
        {
            var release = new GithubRelease("https://api.github.com/repos/nlick47/taix/releases/latest", Assembly.GetExecutingAssembly().GetName().Version.ToString());
            var info = await release.GetRequest();
            if (info != null)
            {
                var uiService = _serviceProvider.GetService(typeof(IUIServicer)) as IUIServicer;
                if (uiService != null)
                {
                    var result = await uiService.ShowConfirmDialogAsync(
                        Application.Current.Resources["NewVersionAvailable"] as string,
                        Application.Current.Resources["WantGoDownloadPage"] as string);

                    if (result)
                    {
                        Process.Start(new ProcessStartInfo("https://github.com/NLick47/Taix/releases/latest") { UseShellExecute = true });
                    }
                }
            }
        }
    }
}
