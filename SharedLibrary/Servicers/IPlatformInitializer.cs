using Microsoft.Extensions.DependencyInjection;

namespace SharedLibrary.Servicers;

public interface IPlatformInitializer
{
    void Initialize(IServiceCollection services);
}