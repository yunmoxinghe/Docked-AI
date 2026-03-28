using Docked_AI.Features.MainWindowContent.ContentArea;
using Docked_AI.Features.MainWindowContent.NavigationBar;
using Docked_AI.Features.Pages.Home;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Docked_AI.Features.MainWindowContent.Linker
{
    public sealed partial class Linker : UserControl
    {
        public event EventHandler? DockToggleRequested;

        public Linker()
        {
            InitializeComponent();
            ContentHost.Navigate(typeof(HomePage));
            NavBar.NavigationRequested += OnNavigationRequested;
            NavBar.DockToggleRequested += OnDockToggleRequested;
        }

        private void OnNavigationRequested(object? sender, NavigationRequest request)
        {
            ContentHost.Navigate(request.PageType, request.Parameter);
        }

        private void OnDockToggleRequested(object? sender, EventArgs e)
        {
            DockToggleRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
