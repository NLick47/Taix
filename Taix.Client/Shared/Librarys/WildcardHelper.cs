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
            return WildcardMatch(input, pattern, ignoreCase);
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


    private static bool WildcardMatch(string input, string pattern, bool ignoreCase)
    {
        var inputIndex = 0;
        var patternIndex = 0;
        var starIndex = -1;
        var matchIndex = 0;

        while (inputIndex < input.Length)
        {
            if (patternIndex < pattern.Length)
            {
                var patternChar = pattern[patternIndex];

                if (patternChar == '*')
                {
                    starIndex = patternIndex;
                    matchIndex = inputIndex;
                    patternIndex++;
                    continue;
                }

                var inputChar = input[inputIndex];
                var charsMatch = ignoreCase
                    ? char.ToLowerInvariant(patternChar) == char.ToLowerInvariant(inputChar)
                    : patternChar == inputChar;

                if (patternChar == '?' || charsMatch)
                {
                    patternIndex++;
                    inputIndex++;
                    continue;
                }
            }

            if (starIndex != -1)
            {
                patternIndex = starIndex + 1;
                matchIndex++;
                inputIndex = matchIndex;
                continue;
            }

            return false;
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}
