using System;

namespace Docked_AI.Features.Tray
{
    public class SystemTrayIconEventArgs : EventArgs
    {
        internal SystemTrayIconEventArgs() { }
        public Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase? Flyout { get; set; }
        public bool Handled { get; set; }
    }
}
