namespace Taix.Client.Shared.Models.Config;

public readonly record struct WindowSnapshot(
    double? X,
    double? Y,
    double Width,
    double Height,
    WindowStateKind State)
{
    public bool IsValid
    {
        get
        {
            if (Width <= 0 || Height <= 0 || !double.IsFinite(Width) || !double.IsFinite(Height)) return false;
            if (X is { } x && !double.IsFinite(x)) return false;
            if (Y is { } y && !double.IsFinite(y)) return false;
            return true;
        }
    }
}
