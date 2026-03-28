using Docked_AI.Features.MainWindowContent.ContentArea;
using Docked_AI.Features.MainWindowContent.NavigationBar;
using Docked_AI.Features.Pages.Home;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.MainWindowContent.Linker
{
    public sealed partial class Linker : UserControl
    {
        public Linker()
        {
            InitializeComponent();
            ContentHost.Navigate(typeof(HomePage));
            NavBar.NavigationRequested += OnNavigationRequested;
        }

        private void OnNavigationRequested(object? sender, NavigationRequest request)
        {
            ContentHost.Navigate(request.PageType, request.Parameter);
        }
    }
}
