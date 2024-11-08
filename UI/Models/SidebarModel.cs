using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.ViewModels;

namespace UI.Models
{
    public class SidebarModel
    {
        public string Title { get; set; }

        public string IconPath { get; set; }

        public ViewModelBase NavigationViewModel { get; set; }
    }
}
