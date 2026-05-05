using System;
using System.Threading.Tasks;
using Taix.Client.Shared.Event;
using Taix.Client.Shared.Models.Config;

namespace Taix.Client.Shared.Servicers.Interfaces;

public interface IAppConfig
{
    Task LoadAsync();

    ConfigModel GetConfig();

    Task SaveAsync();

    event EventHandler<ConfigChangedEventArgs> ConfigChanged;
}
