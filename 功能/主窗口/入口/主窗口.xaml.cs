using Docked_AI.Features.MainWindow.State;
using Docked_AI.Features.MainWindow.Visibility;
using Docked_AI.Features.MainWindowContent.Linker;
using Docked_AI.Features.Tray;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace Docked_AI
{
    public sealed partial class MainWindow : Window, IWindowToggle
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly WindowHostController _windowController;
        private readonly Linker? _linker;

        public WindowState CurrentWindowState => _viewModel.CurrentState;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainWindowViewModel();
            if (Content is FrameworkElement rootElement)
            {
                rootElement.DataContext = _viewModel;
            }

            _linker = RootGrid.Children.OfType<Linker>().FirstOrDefault();
            _windowController = new WindowHostController(this, _viewModel);

            SubscribeToLinkerEvents();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            AppWindow.Changed += OnAppWindowChanged;
            Closed += OnWindowClosed;

            RefreshViewModelDrivenState();
            RefreshWindowChromeState();
        }

        private void SubscribeToLinkerEvents()
        {
            if (_linker is null)
            {
                Debug.WriteLine("MainWindow: Linker not found in RootGrid.");
                return;
            }

            _linker.DockToggleRequested += OnDockToggleRequested;
            _linker.WindowStateToggleRequested += OnWindowStateToggleRequested;
        }

        private void UnsubscribeFromLinkerEvents()
        {
            if (_linker is null)
            {
                return;
            }

            _linker.DockToggleRequested -= OnDockToggleRequested;
            _linker.WindowStateToggleRequested -= OnWindowStateToggleRequested;
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (!args.DidPresenterChange && !args.DidSizeChange)
            {
                return;
            }

            RefreshWindowChromeState();

            if (args.DidPresenterChange)
            {
                _windowController.SyncFromOSWindowState(DetermineOSWindowState());
            }
        }

        private WindowState DetermineOSWindowState()
        {
            return AppWindow.Presenter is OverlappedPresenter presenter
                ? presenter.State switch
                {
                    OverlappedPresenterState.Maximized => WindowState.Maximized,
                    OverlappedPresenterState.Restored => WindowState.Windowed,
                    OverlappedPresenterState.Minimized => WindowState.Hidden,
                    _ => _viewModel.CurrentState
                }
                : _viewModel.CurrentState;
        }

        private bool IsWindowMaximized()
        {
            return AppWindow.Presenter is OverlappedPresenter
            {
                State: OverlappedPresenterState.Maximized
            };
        }

        private void RefreshWindowChromeState()
        {
            UpdateWindowStateIcon();
            UpdateContentTopMargin();
        }

        private void RefreshViewModelDrivenState()
        {
            bool isPinned = _viewModel.CurrentState == WindowState.Pinned;
            UpdateDockToggleIcon(isPinned);
            UpdateContentCornerRadius(isPinned);
            UpdateContentTopMargin();
        }

        private void UpdateWindowStateIcon()
        {
            _linker?.NavBarInstance.UpdateWindowStateIcon(IsWindowMaximized());
        }

        public void ToggleWindowState()
        {
            _windowController.ToggleMaximize();
        }

        private void OnWindowStateToggleRequested(object? sender, System.EventArgs e)
        {
            _windowController.ToggleMaximize();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.CurrentState))
            {
                RefreshViewModelDrivenState();
            }
        }

        private void UpdateDockToggleIcon(bool isPinned)
        {
            _linker?.NavBarInstance.UpdateDockToggleIcon(isPinned);
        }

        private void UpdateContentCornerRadius(bool isPinned)
        {
            _linker?.UpdateContentCornerRadius(isPinned);
        }

        private void UpdateContentTopMargin()
        {
            bool isPinnedOrMaximized = _viewModel.CurrentState == WindowState.Pinned || IsWindowMaximized();
            _linker?.UpdateContentTopMargin(isPinnedOrMaximized);
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
            Debug.WriteLine($"MainWindow.NavigateToNewPage called with URL: {url}");

            if (_linker is null)
            {
                Debug.WriteLine("MainWindow.NavigateToNewPage aborted: Linker not found.");
                return;
            }

            _linker.NavigateToNewPage(url);
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            AppWindow.Changed -= OnAppWindowChanged;
            Closed -= OnWindowClosed;
            UnsubscribeFromLinkerEvents();
        }
    }
}
