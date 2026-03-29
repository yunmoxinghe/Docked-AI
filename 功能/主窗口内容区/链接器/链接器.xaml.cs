using Docked_AI.Features.MainWindowContent.ContentArea;
using Docked_AI.Features.Pages.Home;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using NavBarControl = Docked_AI.Features.MainWindowContent.NavigationBar.NavigationBar;
using NavRequest = Docked_AI.Features.MainWindowContent.NavigationBar.NavigationRequest;

namespace Docked_AI.Features.MainWindowContent.Linker
{
    public sealed partial class Linker : UserControl
    {
        public event EventHandler? DockToggleRequested;

        public NavBarControl NavBarInstance => NavBar;

        public Linker()
        {
            InitializeComponent();
            ContentHost.Navigate(typeof(HomePage));
            NavBar.NavigationRequested += OnNavigationRequested;
            NavBar.DockToggleRequested += OnDockToggleRequested;
        }

        private void OnNavigationRequested(object? sender, NavRequest request)
        {
            ContentHost.Navigate(request.PageType, request.Parameter);
        }

        private void OnDockToggleRequested(object? sender, EventArgs e)
        {
            DockToggleRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
