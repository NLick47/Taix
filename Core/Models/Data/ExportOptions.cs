using System.Globalization;

namespace Core.Models.Data;

public class ExportOptions
{
    public required string FileNamePrefix { get; set; }
    public required CultureInfo Culture { get; set; }
    public required string UncategorizedLabel { get; set; }
    
    public required  WebsiteExportConfig Website { get; set; }
    
    public required  AppExportConfig Application { get; set; }
    
    
    public  class WebsiteExportConfig
    {
        public required string SheetName { get; set; }
        public required string[] Columns { get; set; }
        public required string StatisticsLabel { get; set; }
    }

    public  class AppExportConfig
    {
        public required string DailySheetName { get; set; }
        public required string TimePeriodSheetName { get; set; }
        public required string[] DailyColumns { get; set; }
        public required string[] TimePeriodColumns { get; set; }
        public required string StatisticsLabel { get; set; }
    }
}
