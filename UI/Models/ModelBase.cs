using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Select;

namespace UI.Models
{
    public class ModelBase : UINotifyPropertyChanged
    {
        private SelectItemModel ShowType_;
        /// <summary>
        /// 展示类型（0=应用/1=网站）
        /// </summary>
        public SelectItemModel ShowType { get { return ShowType_; } set { ShowType_ = value; OnPropertyChanged(); } }


        /// <summary>
        /// 展示类型选项
        /// </summary>
        public List<SelectItemModel> ShowTypeOptions { get; } = [
            new ()
                {
                    Id = 0,
                    Name = ResourceStrings.App
                },
                new ()
                {
                    Id = 1,
                    Name = ResourceStrings.Website
                }
        ];
        public ModelBase()
        {
            ShowType = ShowTypeOptions[0];
        }
        public virtual void Dispose() { }
    }
}
