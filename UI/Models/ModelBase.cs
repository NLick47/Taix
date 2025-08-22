using System.Collections.Generic;
using SharedLibrary;
using UI.Controls.Select;

namespace UI.Models;

public class ModelBase : UINotifyPropertyChanged
{
    private SelectItemModel ShowType_;

    public ModelBase()
    {
        ShowType = ShowTypeOptions[0];
    }

    /// <summary>
    ///     展示类型（0=应用/1=网站）
    /// </summary>
    public SelectItemModel ShowType
    {
        get => ShowType_;
        set
        {
            ShowType_ = value;
            OnPropertyChanged();
        }
    }


    /// <summary>
    ///     展示类型选项
    /// </summary>
    public List<SelectItemModel> ShowTypeOptions { get; } =
    [
        new()
        {
            Id = 0,
            Name = ResourceStrings.App
        },
        new()
        {
            Id = 1,
            Name = ResourceStrings.Website
        }
    ];

    public virtual void Dispose()
    {
    }
}