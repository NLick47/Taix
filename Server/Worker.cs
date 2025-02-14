namespace Server;

public class MyBackgroundService : 
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
            
        return Task.CompletedTask;  
    }
}