using UI.Base.Color;
using UI.Controls.Base;
using UI.Models;

namespace UI.Controls.Navigation.Models;

public class NavigationItemModel : UINotifyPropertyChanged
{
    private string _title;

    private string _uri;

    private IconTypes SelectedIcon_ = IconTypes.None;
    public int ID { get; set; }

    public string Title
    {
        get => _title;
        set
        {
            if (value != _title)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    ///     未选择时默认图标
    /// </summary>
    public IconTypes UnSelectedIcon { get; set; }

    /// <summary>
    ///     选中后图标
    /// </summary>
    public IconTypes SelectedIcon
    {
        get
        {
            if (SelectedIcon_ == IconTypes.None) return UnSelectedIcon;

            return SelectedIcon_;
        }
        set => SelectedIcon_ = value;
    }

    public ColorTypes IconColor { get; set; }
    public string BadgeText { get; set; }

    public string Uri
    {
        get => _uri;
        set
        {
            if (value != _uri)
            {
                _uri = value;
                OnPropertyChanged();
            }
        }
    }
}