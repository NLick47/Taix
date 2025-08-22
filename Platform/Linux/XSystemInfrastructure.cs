using SharedLibrary.Servicers;

namespace Linux;

public class XSystemInfrastructure : ISystemInfrastructure
{
    public (string ostype, string version) GetOSVersionName()
    {
        return (string.Empty, string.Empty);
    }

    public bool SetStartup(bool startup)
    {
        return false;
    }
}