namespace Taix.Client.Shared.Models.Data;

public class CategoryWithCountModel
{
    public int ID { get; set; }
    public string? Name { get; set; }
    public string? IconFile { get; set; }
    public string? Color { get; set; }
    public bool IsDirectoryMatch { get; set; }
    public bool IsSystem { get; set; }
    public string? Directories { get; set; }
    public int Count { get; set; }
}
