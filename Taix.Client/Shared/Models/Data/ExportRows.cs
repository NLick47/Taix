namespace Taix.Client.Shared.Models.Data;

public class DailyLogExportRow
{
    public string Date { get; set; } = string.Empty;
    public string App { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class AppSummaryExportRow
{
    public string App { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TotalDuration { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Percentage { get; set; } = string.Empty;
}

public class DailySummaryExportRow
{
    public string Date { get; set; } = string.Empty;
    public string TotalDuration { get; set; } = string.Empty;
}

public class WebLogExportRow
{
    public string Time { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
