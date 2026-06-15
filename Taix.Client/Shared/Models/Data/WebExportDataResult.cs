using System.Collections.Generic;
using Taix.Client.Shared.Models.Db;

namespace Taix.Client.Shared.Models.Data;

public class WebExportDataResult
{
    public List<WebBrowseLogModel> Logs { get; set; } = new();
}
