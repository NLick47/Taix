namespace Taix.Client.Logging;

public class FileLoggerOptions
{
    public string LogDirectory { get; set; } = "Logs";

    public int MaxLogFileAgeDays { get; set; } = 30;

    public double AutoSaveInterval { get; set; } = 1000 * 60 * 5;

    public int SaveThreshold { get; set; } = 50;

    public bool WriteToConsole { get; set; } = true;
}
