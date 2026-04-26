using System.Text.Json;

namespace Taix.Client.Shared.Librarys;

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
