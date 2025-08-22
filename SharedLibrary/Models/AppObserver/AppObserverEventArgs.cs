namespace SharedLibrary.Models.AppObserver;

public class AppObserverEventArgs
{
    public AppObserverEventArgs(string processName, string description, string file, IntPtr handle)
    {
        ProcessName = processName;
        Description = description;
        File = file;
        Handle = handle;
    }

    public string ProcessName { get; }

    public string Description { get; }

    public string File { get; }

    public IntPtr Handle { get; }
}