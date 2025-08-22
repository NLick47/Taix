namespace SharedLibrary.Servicers;

public interface ISystemInfrastructure
{
    public bool SetStartup(bool startup);

    public (string ostype, string version) GetOSVersionName();
}