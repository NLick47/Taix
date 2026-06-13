namespace Taix.Client.Shared.Models;

public static class AppModelExtensions
{
    public static string GetDisplayName(this AppModel app)
    {
        if (!string.IsNullOrEmpty(app.Alias))
            return app.Alias;

#if MACOS
        if (!string.IsNullOrEmpty(app.Name))
            return app.Name;

        return app.Description ?? "Unknown";
#else
        if (!string.IsNullOrEmpty(app.Description))
            return app.Description;

        return app.Name ?? "Unknown";
#endif
    }
}
