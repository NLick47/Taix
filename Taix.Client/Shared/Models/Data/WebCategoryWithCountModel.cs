namespace Taix.Client.Shared.Models.Data;

public class WebCategoryWithCountModel
{
    public int ID { get; set; }
    public string? Name { get; set; }
    public string? IconFile { get; set; }
    public string? Color { get; set; }
    public bool IsUrlMatch { get; set; }
    public string? UrlPatterns { get; set; }
    public bool IsSystem { get; set; }
    public int Count { get; set; }
}
