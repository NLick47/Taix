namespace Taix.Client.Librarys.Api;

internal sealed class UpdateCategoryRequest
{
    public int ID { get; set; }
    public string? Name { get; set; }
    public string? IconFile { get; set; }
    public string? Color { get; set; }
    public bool IsDirectoryMatch { get; set; }
    public string? Directories { get; set; }
}
