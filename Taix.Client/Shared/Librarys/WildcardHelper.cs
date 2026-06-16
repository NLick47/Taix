using System;
using System.Text.RegularExpressions;

namespace Taix.Client.Shared.Librarys;

public static class WildcardHelper
{
    private static readonly char[] RegexMetaChars = ['^', '$', '[', ']', '(', ')', '{', '}', '|', '+', '\\'];


    public static bool IsMatch(string input, string pattern, bool ignoreCase = true)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(pattern))
            return false;

        if (IsRegexPattern(pattern))
        {
            return RegexMatch(input, pattern, ignoreCase);
        }

        if (pattern.Contains('*') || pattern.Contains('?'))
        {

            return WildcardRegexMatch(input, pattern, ignoreCase);
        }

        return ContainsMatch(input, pattern, ignoreCase);
    }


    private static bool IsRegexPattern(string pattern)
    {
        return pattern.IndexOfAny(RegexMetaChars) >= 0;
    }


    private static bool RegexMatch(string input, string pattern, bool ignoreCase)
    {
        try
        {
            var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            return Regex.IsMatch(input, pattern, options, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            return ContainsMatch(input, pattern, ignoreCase);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }


    private static bool ContainsMatch(string input, string pattern, bool ignoreCase)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return input.Contains(pattern, comparison);
    }


    private static bool WildcardRegexMatch(string input, string pattern, bool ignoreCase)
    {
        try
        {
            var regexPattern = WildcardToRegex(pattern, ignoreCase);
            return Regex.IsMatch(input, regexPattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            return ContainsMatch(input, pattern, ignoreCase);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static string WildcardToRegex(string pattern, bool ignoreCase)
    {
        var result = new System.Text.StringBuilder(pattern.Length * 2);

        if (ignoreCase)
            result.Append("(?i)^");
        else
            result.Append("^");

        foreach (var c in pattern)
        {
            switch (c)
            {
                case '*':
                    result.Append(".*");
                    break;
                case '?':
                    result.Append('.');
                    break;
                case '.' or '^' or '$' or '+' or '[' or ']' or '(' or ')' or '{' or '}' or '\\' or '|':
                    result.Append('\\');
                    result.Append(c);
                    break;
                default:
                    result.Append(c);
                    break;
            }
        }

        result.Append('$');
        return result.ToString();
    }
}
