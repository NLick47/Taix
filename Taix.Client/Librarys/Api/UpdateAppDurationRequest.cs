using System;

namespace Taix.Client.Librarys.Api;

internal sealed class UpdateAppDurationRequest
{
    public string ProcessName { get; set; } = string.Empty;
    public int Duration { get; set; }
    public DateTime StartDateTime { get; set; }
}
