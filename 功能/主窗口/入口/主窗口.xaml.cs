using Docked_AI.Features.MainWindow.State;
using Docked_AI.Features.MainWindow.Visibility;
using Docked_AI.Features.MainWindowContent.Linker;
using Docked_AI.Features.MainWindowContent.NavigationBar;
using Docked_AI.Features.Pages.New;
using Docked_AI.Features.Tray;
using Microsoft.UI.Xaml;
using System.ComponentModel;

namespace Docked_AI
{
    public sealed partial class MainWindow : Window, IWindowToggle
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly WindowHostController _windowController;

        public WindowState CurrentWindowState => _viewModel.CurrentState;

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
            
            // 监听窗口关闭事件以清理订阅
            this.Closed += OnWindowClosed;
        }

        private void OnAppWindowChanged(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
        {
            if (args.DidPresenterChange || args.DidSizeChange)
            {
                UpdateWindowStateIcon();
                UpdateContentTopMargin();
                
                // Sync OS window state changes to StateManager
                if (args.DidPresenterChange)
                {
                    WindowState osState = DetermineOSWindowState();
                    _windowController.SyncFromOSWindowState(osState);
                }
            }
        }
        
        private WindowState DetermineOSWindowState()
        {
            if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                return presenter.State switch
                {
                    Microsoft.UI.Windowing.OverlappedPresenterState.Maximized => WindowState.Maximized,
                    Microsoft.UI.Windowing.OverlappedPresenterState.Restored => WindowState.Windowed,
                    Microsoft.UI.Windowing.OverlappedPresenterState.Minimized => WindowState.Hidden,
                    _ => _viewModel.CurrentState // Fallback to current state
                };
            }
            
            return _viewModel.CurrentState; // Fallback to current state
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

        private async void OnWindowStateToggleRequested(object? sender, System.EventArgs e)
        {
            // 使用 WindowHostController 的 ToggleMaximize 方法
            _windowController.ToggleMaximize();
        }

        public void ToggleWindowState()
        {
            // 委托给 WindowHostController
            _windowController.ToggleMaximize();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.CurrentState))
            {
                var currentState = _viewModel.CurrentState;
                bool isPinned = currentState == WindowState.Pinned;
                
                UpdateDockToggleIcon(isPinned);
                UpdateContentCornerRadius(isPinned);
                UpdateContentTopMargin();
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

        private void UpdateContentTopMargin()
        {
            bool isMaximized = false;
            if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                isMaximized = presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;
            }

            var currentState = _viewModel.CurrentState;
            bool isPinnedOrMaximized = currentState == WindowState.Pinned || isMaximized;

            if (RootGrid.Children.Count > 0 && 
                RootGrid.Children[0] is Linker linker)
            {
                linker.UpdateContentTopMargin(isPinnedOrMaximized);
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

        private async void OnDockToggleRequested(object? sender, System.EventArgs e)
        {
            // 使用 WindowHostController 的 TogglePinnedDock 方法
            // StateManager 会自动处理从 Maximized 到 Pinned 的转换
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
        
        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            // Unsubscribe from all events to prevent memory leaks
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            this.AppWindow.Changed -= OnAppWindowChanged;
            this.Closed -= OnWindowClosed;
            
            if (RootGrid.Children.Count > 0 && RootGrid.Children[0] is Linker linker)
            {
                linker.DockToggleRequested -= OnDockToggleRequested;
                linker.WindowStateToggleRequested -= OnWindowStateToggleRequested;
            }
        }
    }
}
