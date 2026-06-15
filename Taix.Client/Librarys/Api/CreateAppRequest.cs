namespace Taix.Client.Librarys.Api;

internal sealed class CreateAppRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? File { get; set; }
    public string? IconFile { get; set; }
    public int CategoryID { get; set; }
}
