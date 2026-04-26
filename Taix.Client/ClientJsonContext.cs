using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Taix.Client.Servicers.Updater;
using Taix.Client.Shared.Models.Config;

namespace Taix.Client;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ConfigModel))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(ObservableCollection<string>))]
[JsonSerializable(typeof(GithubRelease.GithubModel))]
[JsonSerializable(typeof(GithubRelease.GithubAssetsModel))]
public partial class ClientJsonContext : JsonSerializerContext { }
