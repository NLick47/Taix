using System.Threading.Tasks;

namespace UI.Servicers;

public interface IMainServicer
{
    Task Start(bool isSelfStart);

    void DesignStart();
}