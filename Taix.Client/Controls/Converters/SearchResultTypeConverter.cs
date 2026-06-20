using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Taix.Client.Shared.Models.Search;

namespace Taix.Client.Controls.Converters;

public class SearchResultTypeConverter : IValueConverter
{
    public static readonly SearchResultTypeConverter Instance = new();

    public static readonly IValueConverter IsAppConverter = new IsTypeConverter(SearchResultType.App);
    public static readonly IValueConverter IsWebConverter = new IsTypeConverter(SearchResultType.WebSite);
    public static readonly IValueConverter IsCategoryAppConverter = new IsTypeConverter(SearchResultType.CategoryApp);
    public static readonly IValueConverter IsCategoryWebConverter = new IsTypeConverter(SearchResultType.CategoryWeb);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SearchResultType type) return value;

        var resourceKey = type switch
        {
            SearchResultType.App => "SearchTypeApp",
            SearchResultType.WebSite => "SearchTypeWeb",
            SearchResultType.CategoryApp => "SearchTypeCategoryApp",
            SearchResultType.CategoryWeb => "SearchTypeCategoryWeb",
            _ => null
        };

        if (resourceKey == null) return type.ToString();

        if (Application.Current?.Resources.TryGetResource(resourceKey, null, out var resource) == true
            && resource is string s)
        {
            return s;
        }

        return type.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private sealed class IsTypeConverter : IValueConverter
    {
        private readonly SearchResultType _expected;
        public IsTypeConverter(SearchResultType expected) => _expected = expected;
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is SearchResultType t && t == _expected;
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
