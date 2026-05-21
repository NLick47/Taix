using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace Taix.Client.Servicers.Updater;

public class GithubRelease
{
    private readonly string githubUrl;
    private readonly string nowVersion;
    private static readonly HttpClient _httpClient;

    public GithubRelease(string githubUrl, string nowVersion)
    {
        this.githubUrl = githubUrl;
        this.nowVersion = nowVersion;
        Info = new VersionInfo();
    }

    static GithubRelease()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/68.0.3440.106 Safari/537.36");
    }

    public VersionInfo Info { get; set; }

    public bool IsCanUpdate()
    {
        return !(nowVersion == Info.Version);
    }

    public async Task<VersionInfo?> GetRequestAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(githubUrl);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();

            var data = JsonSerializer.Deserialize<GithubModel>(body, ClientJsonContext.Default.GithubModel);

            Info.IsPre = data.prerelease;
            Info.Title = data.name;
            Info.Version = data.tag_name.Replace("v", string.Empty);
            Info.DownloadUrl = data.assets.FirstOrDefault()?.browser_download_url;
            Info.HtmlUrl = data.html_url;
            Info.Content = data.body;

            return Info;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public class VersionInfo
    {
        /// <summary>
        /// 版本标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 版本号
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// 是否是预览版本
        /// </summary>
        public bool IsPre { get; set; }

        /// <summary>
        /// 下载路径
        /// </summary>
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// 版本更新内容网页链接
        /// </summary>
        public string? HtmlUrl { get; set; }

        public string? Content { get; set; }
    }

    public class GithubModel
    {
        public string tag_name { get; set; }
        public string html_url { get; set; }
        public string name { get; set; }
        public bool prerelease { get; set; }
        public System.Collections.Generic.List<GithubAssetsModel> assets { get; set; }
        public string body { get; set; }
    }

    public class GithubAssetsModel
    {
        public string? browser_download_url { get; set; }
    }
}
