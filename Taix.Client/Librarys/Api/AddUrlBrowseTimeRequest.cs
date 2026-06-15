using System;

namespace Taix.Client.Librarys.Api;

internal sealed class AddUrlBrowseTimeRequest
{
    public string? Url { get; set; }
    public string? Title { get; set; }
    public int Duration { get; set; }
    public DateTime? DateTime { get; set; }
}
