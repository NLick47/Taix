namespace Taix.Client.Shared.Models.Data;

public class BatchCategoryRequest
{
    public int[] AppIds { get; set; } = [];
    public int CategoryId { get; set; }
}
