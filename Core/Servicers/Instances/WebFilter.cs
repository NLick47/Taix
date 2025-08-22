using System.Text.RegularExpressions;
using Core.Librarys;
using Core.Librarys.Browser;
using Core.Models.Config;
using Core.Servicers.Interfaces;

namespace Core.Servicers.Instances;

public class WebFilter : IWebFilter
{
    private readonly IAppConfig _appConfig;

    //  默认忽略的规则
    private readonly string[] _ignoreRegexArr = { "^chrome://(.*)", "^edge://(.*)", "^view-source:(.*)" };
    private List<string> _ignoreURLCache;

    public WebFilter(IAppConfig appConfig_)
    {
        _appConfig = appConfig_;
    }

    public void Init()
    {
        _ignoreURLCache = new List<string>();
        _appConfig.ConfigChanged += _appConfig_ConfigChanged;
    }

    public bool IsIgnore(string url_)
    {
        if (string.IsNullOrEmpty(url_)) return true;

        if (_ignoreURLCache.Contains(url_)) return true;

        if (IsDomainIgnore(url_))
        {
            _ignoreURLCache.Add(url_);
            return true;
        }

        if (IsRegexIgnore(url_))
        {
            _ignoreURLCache.Add(url_);
            return true;
        }

        return false;
    }

    public bool IsDomainIgnore(string url_)
    {
        url_ = UrlHelper.GetDomain(url_);

        var config = _appConfig.GetConfig();
        var ignoreURLList = config.Behavior.IgnoreURLList;

        return ignoreURLList.Contains(url_);
    }

    public bool IsRegexIgnore(string url_)
    {
        var config = _appConfig.GetConfig();
        var ignoreURLList = config.Behavior.IgnoreURLList;

        //  从正则表达式验证过滤
        var regexList = ignoreURLList.Where(m => Regex.IsMatch(m, @"[\*|\$|\!|\(|\)|\{|\\|\[|\^|\|]")).ToList();
        regexList.AddRange(_ignoreRegexArr);

        foreach (var reg in regexList)
            if (RegexHelper.IsMatch(url_, reg))
                return true;

        return false;
    }

    private void _appConfig_ConfigChanged(ConfigModel oldConfig, ConfigModel newConfig)
    {
        if (oldConfig.Behavior.IgnoreURLList != newConfig.Behavior.IgnoreURLList) _ignoreURLCache.Clear();
    }
}