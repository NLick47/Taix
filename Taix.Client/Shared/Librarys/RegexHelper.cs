using System.Text.RegularExpressions;

namespace Taix.Client.Shared.Librarys;

public static class RegexHelper
{
    /// <summary>
    ///     使用正则表达式进行匹配
    /// </summary>
    /// <param name="input">输入字符串</param>
    /// <param name="pattern">正则表达式模式</param>
    /// <returns>匹配成功返回True</returns>
    public static bool IsMatch(string input, string pattern)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(pattern))
            return false;

        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
