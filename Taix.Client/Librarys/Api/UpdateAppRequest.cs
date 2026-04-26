namespace Taix.Client.Librarys.Api;

internal sealed class UpdateAppRequest
{
    public int ID { get; set; }
    public string? Name { get; set; }
    public string? Alias { get; set; }
    public string? Description { get; set; }
    public string? File { get; set; }
    public string? IconFile { get; set; }
    public int CategoryID { get; set; }
    public int TotalTime { get; set; }
}
