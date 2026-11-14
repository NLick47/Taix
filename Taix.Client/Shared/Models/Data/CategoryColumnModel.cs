namespace Taix.Client.Shared.Models.Data;

public class CategoryColumnModel
{
    public int CategoryID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? IconFile { get; set; }
    public double[] Values { get; set; } = [];
}
