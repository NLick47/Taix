namespace Taix.Client.Librarys.Api;

internal sealed class ApiResponse<T>
{
    public int Code { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}
