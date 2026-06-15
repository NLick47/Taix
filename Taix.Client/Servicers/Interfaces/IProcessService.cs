namespace Taix.Client.Servicers.Interfaces;

public interface IProcessService
{
    void OpenUrl(string url);
    void OpenFile(string filePath);
    void OpenDirectory(string filePath);
}
