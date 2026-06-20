namespace Taix.Client.Shared.Models.Config;

public class ConfigModel
{
    public const int CurrentVersion = 3;

    public int Version { get; set; }

    public GeneralModel General { get; set; } = new();

    public BehaviorModel Behavior { get; set; } = new();

    public ShortcutModel Shortcut { get; set; } = new();
}
