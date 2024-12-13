using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Window;
using UI.Views;

namespace UI.Servicers
{
    public class UIServicer : IUIServicer
    {
        private readonly MainWindow _window;

        public UIServicer(MainWindow window)
        {
            this._window = window;
        }

        public Task<bool> ShowConfirmDialogAsync(string title_, string message_)
        {
            return _window.ShowConfirmDialogAsync(title_, message_);
        }

        public Task<string> ShowInputModalAsync(string title_, string placeholder_, string value_ = null, Func<string, bool> validFnc_ = null)
        {
            return _window.ShowInputModalAsync(title_, placeholder_, value_, validFnc_);
        }
    }
}
