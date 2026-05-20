using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Models.Data;

namespace Taix.Client;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ConfigModel))]
[JsonSerializable(typeof(GeneralModel))]
[JsonSerializable(typeof(BehaviorModel))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(ObservableCollection<string>))]
[JsonSerializable(typeof(DailyLogExportRow))]
[JsonSerializable(typeof(List<DailyLogExportRow>))]
[JsonSerializable(typeof(AppSummaryExportRow))]
[JsonSerializable(typeof(List<AppSummaryExportRow>))]
[JsonSerializable(typeof(DailySummaryExportRow))]
[JsonSerializable(typeof(List<DailySummaryExportRow>))]
[JsonSerializable(typeof(WebLogExportRow))]
[JsonSerializable(typeof(List<WebLogExportRow>))]
[JsonSerializable(typeof(WindowStateModel))]
public partial class ClientJsonContext : JsonSerializerContext { }
