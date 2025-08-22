using Core.Models.WebPage;

namespace Core.Librarys;

public static class SiteCache
{
    private static readonly List<Site> sites = new();
    private static readonly int maxNum = 100;

    public static Site Get(string key)
    {
        return sites.Where(m => m.Title.Equals(key)).FirstOrDefault();
    }

    public static List<Site> GetAll()
    {
        return sites;
    }

    public static void Set(string key, string value)
    {
        if (Get(key) == null)
        {
            sites.Add(new Site
            {
                Title = key,
                Url = value
            });

            if (sites.Count > maxNum) sites.RemoveAt(0);
        }
    }
}