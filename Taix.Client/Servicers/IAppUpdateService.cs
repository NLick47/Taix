using System.Collections.Generic;
using System.Threading.Tasks;

namespace Taix.Client.Servicers;

public interface IAppUpdateService
{
    Task UpdateCategoryAsync(int appId, int categoryId);
    
    Task ClearCategoryAsync(int appId);
    
    Task UpdateAliasAsync(int appId, string? alias);
    
    Task UpdateDescriptionAsync(int appId, string? description);
    
    Task UpdateCategoryBatchAsync(IEnumerable<int> appIds, int categoryId);
}