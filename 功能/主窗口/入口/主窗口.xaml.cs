using Docked_AI.Features.MainWindow.State;
using Docked_AI.Features.MainWindow.Visibility;
using Docked_AI.Features.MainWindowContent.Linker;
using Docked_AI.Features.MainWindowContent.NavigationBar;
using Docked_AI.Features.Pages.New;
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
                linker.WindowStateToggleRequested += OnWindowStateToggleRequested;
            }

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            
            // 监听窗口状态变化以更新图标
            this.AppWindow.Changed += OnAppWindowChanged;
        }

        private void OnAppWindowChanged(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
        {
            if (args.DidPresenterChange)
            {
                UpdateWindowStateIcon();
            }
        }

        private void UpdateWindowStateIcon()
        {
            bool isMaximized = false;
            if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                isMaximized = presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;
            }

            if (RootGrid.Children.Count > 0 && RootGrid.Children[0] is Linker linker)
            {
                linker.NavBarInstance.UpdateWindowStateIcon(isMaximized);
            }
        }

        private void OnWindowStateToggleRequested(object? sender, System.EventArgs e)
        {
            ToggleWindowState();
        }

        public void ToggleWindowState()
        {
            if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                if (presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
                {
                    presenter.Restore();
                }
                else
                {
                    presenter.Maximize();
                }
                UpdateWindowStateIcon();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsDockPinned))
            {
                UpdateDockToggleIcon(_viewModel.IsDockPinned);
                UpdateContentCornerRadius(_viewModel.IsDockPinned);
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

        private void UpdateContentCornerRadius(bool isPinned)
        {
            if (RootGrid.Children.Count > 0 && 
                RootGrid.Children[0] is Linker linker)
            {
                linker.UpdateContentCornerRadius(isPinned);
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

        public void NavigateToNewPage(string url)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow.NavigateToNewPage called with URL: {url}");
            System.Diagnostics.Debug.WriteLine($"RootGrid.Children.Count: {RootGrid.Children.Count}");
            
            if (RootGrid.Children.Count > 0 && RootGrid.Children[0] is Linker linker)
            {
                System.Diagnostics.Debug.WriteLine("Linker found, calling NavigateToNewPage");
                linker.NavigateToNewPage(url);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Linker NOT found!");
            }
        }
    }
}
