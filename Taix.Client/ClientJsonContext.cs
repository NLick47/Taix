using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Taix.Client.Servicers.Updater;
using Taix.Client.Shared.Models.Config;
using Taix.Client.Shared.Models.Config.Link;
using Taix.Client.Shared.Models.Data;

namespace Taix.Client;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ConfigModel))]
[JsonSerializable(typeof(GeneralModel))]
[JsonSerializable(typeof(BehaviorModel))]
[JsonSerializable(typeof(LinkModel))]
[JsonSerializable(typeof(List<LinkModel>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(ObservableCollection<string>))]
[JsonSerializable(typeof(GithubRelease.GithubModel))]
[JsonSerializable(typeof(GithubRelease.GithubAssetsModel))]
[JsonSerializable(typeof(DailyLogExportRow))]
[JsonSerializable(typeof(List<DailyLogExportRow>))]
[JsonSerializable(typeof(HoursLogExportRow))]
[JsonSerializable(typeof(List<HoursLogExportRow>))]
[JsonSerializable(typeof(WebLogExportRow))]
[JsonSerializable(typeof(List<WebLogExportRow>))]
[JsonSerializable(typeof(WindowStateModel))]
public partial class ClientJsonContext : JsonSerializerContext { }
