using System;

namespace Taix.Client;

public static class ServerEndpoints
{
    public const string DefaultServerUrl = "http://127.0.0.1:37091";

    public const string PluginUiPath = "/ui/";

    public static string ServerUrl =>
        Environment.GetEnvironmentVariable("TAIX_SERVER") is { Length: > 0 } env ? env : DefaultServerUrl;

    public static string PluginUiUrl => ServerUrl.TrimEnd('/') + PluginUiPath;
}
