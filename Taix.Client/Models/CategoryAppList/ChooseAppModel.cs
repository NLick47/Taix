using Taix.Client.Controls.Select;
using Taix.Client.Shared.Models;

namespace Taix.Client.Models.CategoryAppList;

public class ChooseAppModel
{
    public bool IsChoosed { get; set; }
    public AppModel App { get; set; }
    public SelectItemModel Value { get; set; } = new();
    public bool Visibility { get; set; } = true;
}