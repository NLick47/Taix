namespace Taix.Client.Shared.Models.Data;

public class DailyLogExportRow
{
    public string Date { get; set; } = string.Empty;
    public string App { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class HoursLogExportRow
{
    public string TimePeriod { get; set; } = string.Empty;
    public string App { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class WebLogExportRow
{
    public string Time { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
}
