using Core.Servicers.Interfaces;
using SharedLibrary.Librarys;

namespace Server;

public class Worker : IHostedService
{
   private readonly IMain _main;

   public Worker(IMain main)
   {
       _main = main;
   }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _main.RunAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _main.Stop();
        Logger.Save(true);
        return Task.CompletedTask;
    }
}