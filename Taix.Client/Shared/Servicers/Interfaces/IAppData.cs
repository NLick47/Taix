using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Taix.Client.Shared.Models;

namespace Taix.Client.Shared.Servicers.Interfaces;

public interface IAppData
{
    Task<AppModel?> GetAppAsync(int id, CancellationToken cancellationToken = default);

    Task<AppModel?> GetAppByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AppModel>> GetAllAppsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AppModel>> GetAppsByCategoryIDAsync(int categoryID, CancellationToken cancellationToken = default);

    Task<int> GetAppCountByCategoryIDAsync(int categoryID);

    Task AddAppAsync(AppModel app, CancellationToken cancellationToken = default);

    Task UpdateAppAsync(AppModel app, CancellationToken cancellationToken = default);
}
