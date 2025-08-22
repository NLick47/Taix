namespace Core.Models.WebPage;

public class Site
{
    public static Site Empty = new()
    {
        Title = string.Empty,
        Url = string.Empty
    };

    public Site(Site site = null)
    {
        if (site != null)
        {
            Title = site.Title;
            Url = site.Url;
        }
    }

    public string Title { get; set; }
    public string Url { get; set; }

    public override string ToString()
    {
        return $"Title:{Title},Url:{Url}";
    }
}