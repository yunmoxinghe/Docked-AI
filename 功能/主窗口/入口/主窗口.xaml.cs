using Docked_AI.Features.MainWindow.State;
using Docked_AI.Features.MainWindow.Visibility;
using Docked_AI.Features.MainWindowContent.Linker;
using Docked_AI.Features.MainWindowContent.NavigationBar;
using Microsoft.UI.Xaml;
using System.ComponentModel;

namespace Docked_AI
{
    public sealed partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly WindowHostController _windowController;

        public bool IsWindowVisible => _viewModel.IsWindowVisible;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainWindowViewModel();
            if (Content is FrameworkElement rootElement)
            {
                rootElement.DataContext = _viewModel;
            }

            _windowController = new WindowHostController(this, _viewModel);

            if (RootGrid.Children.Count > 0 && RootGrid.Children[0] is Linker linker)
            {
                linker.DockToggleRequested += OnDockToggleRequested;
            }

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsDockPinned))
            {
                UpdateDockToggleIcon(_viewModel.IsDockPinned);
            }
        }

        private void UpdateDockToggleIcon(bool isPinned)
        {
            if (RootGrid.Children.Count > 0 && 
                RootGrid.Children[0] is Linker linker)
            {
                linker.NavBarInstance.UpdateDockToggleIcon(isPinned);
            }
        }

        public void ToggleWindow()
        {
            _windowController.ToggleWindow();
        }

        public void TogglePinnedDock()
        {
            _windowController.TogglePinnedDock();
        }

        private void OnDockToggleRequested(object? sender, System.EventArgs e)
        {
            TogglePinnedDock();
        }
    }
}
