namespace Taix.Client.Librarys.Api;

public class UpdateSitesCategoryRequest
{
    public int[] SiteIds { get; set; } = [];
    public int CategoryId { get; set; }
}
