using Docked_AI.Features.MainWindowContent.ContentArea;
using Docked_AI.Features.Pages.Home;
using Docked_AI.Features.Pages.New;
using Docked_AI.Features.Pages.Settings;
using Docked_AI.Features.Pages.WebApp;
using Docked_AI.Features.Pages.WebApp.Browser;
using Docked_AI.Features.Pages.WebApp.Shared;
using Docked_AI.Features.UnifiedCalls.TopAppBar;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using NavBarControl = Docked_AI.Features.MainWindowContent.NavigationBar.NavigationBar;
using NavRequest = Docked_AI.Features.MainWindowContent.NavigationBar.NavigationRequest;

namespace Docked_AI.Features.MainWindowContent.Linker
{
    public sealed partial class Linker : UserControl
    {
        public event EventHandler? DockToggleRequested;
        public event EventHandler? WindowStateToggleRequested;

        public NavBarControl NavBarInstance => NavBar;

        // 导航历史由 ContentHost 的 Frame.BackStack 内置管理，无需自定义栈
        // 注意：此字段用于跟踪后退导航状态，虽然当前未读取，但保留用于未来扩展
#pragma warning disable CS0414
        private bool _isNavigatingBack = false;
#pragma warning restore CS0414
        private bool _isNavigationBarOnLeft;
        private bool _isPinnedOrMaximized;
        private double _contentTopMargin = 6;
        private double _contentOutsideMargin = 4;
        private const double CompactContentOutsideMargin = 4;
        private const double ContentNavigationSideMargin = 0;
        private const double NavigationGap = 2;
        private bool _isContentLoaded = false;

        public Linker()
        {
            InitializeComponent();
            TopAppBarService.Register(ContentHost);
            UnifiedCalls.ContentArea.ContentAreaService.Register(ContentHost);
            
            // ⭐ 不在构造函数中导航到首页，延迟到 LoadContent() 调用
            // ContentHost.Navigate(typeof(HomePage));
            
            ContentHost.Navigated += ContentHost_Navigated;
            ContentHost.CachedPageNavigated += ContentHost_CachedPageNavigated;
            ContentHost.PageCloseRequested += OnPageCloseRequested;
            NavBar.NavigationRequested += OnNavigationRequested;
            NavBar.DockToggleRequested += OnDockToggleRequested;
            NavBar.WindowStateToggleRequested += OnWindowStateToggleRequested;
            NavBar.ShortcutRemoved += OnShortcutRemoved;
            NavBar.WebAppRestartRequested += OnWebAppRestartRequested;
            NavBar.BackRequested += OnBackRequested;
            ContentHost.TopBarDoubleTapped += OnTopBarDoubleTapped;
            
            // 订阅 AI 实验室设置变化事件
            Pages.Settings.SettingsPage.AILabSettingsChanged += OnAILabSettingsChanged;
            Pages.Settings.SettingsPage.DockSideSettingsChanged += OnDockSideSettingsChanged;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
            ApplyNavigationBarPlacement();
        }

        /// <summary>
        /// 加载内容（延迟初始化，在启动屏幕结束后调用）
        /// </summary>
        public void LoadContent()
        {
            if (_isContentLoaded)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine("[Linker] Loading content...");
            
            // 导航到首页
            ContentHost.Navigate(typeof(HomePage));
            _isContentLoaded = true;
            
            System.Diagnostics.Debug.WriteLine("[Linker] Content loaded successfully");
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnifiedCalls.ContentArea.ContentAreaService.Unregister();
            Pages.Settings.SettingsPage.AILabSettingsChanged -= OnAILabSettingsChanged;
            Pages.Settings.SettingsPage.DockSideSettingsChanged -= OnDockSideSettingsChanged;
            SizeChanged -= OnSizeChanged;
            Unloaded -= OnUnloaded;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateContentOutsideMargin(e.NewSize.Width);
            ApplyContentHostMargin();
        }

        private void OnAILabSettingsChanged(object? sender, EventArgs e)
        {
            // 通知导航栏更新 AI 导航项的可见性
            NavBar.UpdateAINavigationItemVisibility();
        }

        private void OnDockSideSettingsChanged(object? sender, EventArgs e)
        {
            ApplyNavigationBarPlacement();
        }

        private void ApplyNavigationBarPlacement()
        {
            bool placeOnLeft =
                ExperimentalSettings.DockSide == WindowDockSide.Left &&
                ExperimentalSettings.PlaceNavigationBarOnLeftWhenDockedLeft;

            _isNavigationBarOnLeft = placeOnLeft;
            LeftNavigationColumn.Width = new GridLength(placeOnLeft ? 48 : 0);
            LeftNavigationGapColumn.Width = new GridLength(placeOnLeft ? NavigationGap : 0);
            RightNavigationGapColumn.Width = new GridLength(placeOnLeft ? 0 : NavigationGap);
            RightNavigationColumn.Width = new GridLength(placeOnLeft ? 0 : 48);

            Grid.SetColumn(NavBar, placeOnLeft ? 0 : 4);
            NavBar.SetNavigationBarPlacement(placeOnLeft);
            UpdateContentOutsideMargin(ActualWidth);
            ApplyContentHostMargin();
        }

        private void ApplyContentHostMargin()
        {
            double leftMargin = _isNavigationBarOnLeft ? ContentNavigationSideMargin : _contentOutsideMargin;
            double rightMargin = _isNavigationBarOnLeft ? _contentOutsideMargin : ContentNavigationSideMargin;
            ContentHost.Margin = new Thickness(leftMargin, _contentTopMargin, rightMargin, 4);
            System.Diagnostics.Debug.WriteLine(
                $"[Linker] NavigationBarOnLeft={_isNavigationBarOnLeft}, ContentHost.Margin={ContentHost.Margin}");
        }

        private void UpdateContentOutsideMargin(double width)
        {
            if (_isPinnedOrMaximized)
            {
                _contentOutsideMargin = CompactContentOutsideMargin;
                return;
            }

            _contentOutsideMargin = width >= 1200 ? 24 : width >= 700 ? 16 : CompactContentOutsideMargin;
        }

        private void OnBackRequested(object? sender, EventArgs e)
        {
            if (!ContentHost.CanGoBack) return;

            _isNavigatingBack = true;
            // 官方推荐：不传参数，Frame 自动使用反向动画
            ContentHost.GoBack();
        }

        private void OnPageCloseRequested(object? sender, string shortcutId)
        {
            System.Diagnostics.Debug.WriteLine($"[Linker] 收到页面关闭请求: {shortcutId}");
            
            // 诊断：关闭前的状态
            WebViewManager.DiagnoseState();
            
            ContentHost.RemoveCachedPage(shortcutId);
            ContentHost.Navigate(typeof(HomePage));
            NavBar.SelectHomeItem();
            NavBar.UpdateBackButtonVisibility(false);
            
            // 诊断：关闭后的状态
            WebViewManager.DiagnoseState();
        }

        private void OnShortcutRemoved(object? sender, string shortcutId)
        {
            // 清除对应的缓存页面
            ContentHost.RemoveCachedPage(shortcutId);
        }

        private async void OnWebAppRestartRequested(object? sender, string shortcutId)
        {
            System.Diagnostics.Debug.WriteLine($"[Linker] 收到重启请求: {shortcutId}");
            await ContentHost.RestartCurrentTabAsync();
        }

        private void ContentHost_Navigated(object? sender, NavigationEventArgs e)
        {
            SyncNavigationBarSelection(e.SourcePageType, e.Parameter);
            _isNavigatingBack = false;
            NavBar.UpdateBackButtonVisibility(ContentHost.CanGoBack);
            // 顶栏返回按钮由 ContentArea 内部在 Navigated 时自动刷新，无需在此处理
            SyncTopAppBarVisibility(e.SourcePageType);
        }

        private void ContentHost_CachedPageNavigated(object? sender, (Type PageType, object? Parameter) e)
        {
            SyncNavigationBarSelection(e.PageType, e.Parameter);
            _isNavigatingBack = false;
            NavBar.UpdateBackButtonVisibility(ContentHost.CanGoBack);
            // 顶栏返回按钮由 ContentArea 内部在 CachedPageNavigated 时自动刷新，无需在此处理
            SyncTopAppBarVisibility(e.PageType);
        }

        /// <summary>
        /// 同步顶部应用栏的可见性
        /// </summary>
        private void SyncTopAppBarVisibility(Type pageType)
        {
            // WebBrowserPage 现在使用统一顶部栏，不需要特殊处理
            // 顶部栏的显示由页面自己控制
        }

        private void OnNavigationRequested(object? sender, NavRequest request)
        {
            ContentHost.Navigate(request.PageType, request.Parameter);
        }

        private void PushCurrentPageToHistory() { } // 已废弃，由 Frame.BackStack 内置管理

        private void OnDockToggleRequested(object? sender, EventArgs e)
        {
            DockToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnWindowStateToggleRequested(object? sender, EventArgs e)
        {
            WindowStateToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnTopBarDoubleTapped(object? sender, EventArgs e)
        {
            WindowStateToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        public void NavigateToNewPage(string url)
        {
            System.Diagnostics.Debug.WriteLine($"Linker.NavigateToNewPage called with URL: {url}");
            ContentHost.Navigate(typeof(NewPage), url);
            NavBar.SelectNewPageItem();
        }

        public void UpdateContentCornerRadius(bool isPinned)
        {
            ContentHost.SetCornerRadius(isPinned);
        }

        public void UpdateContentTopMargin(bool isPinnedOrMaximized)
        {
            _isPinnedOrMaximized = isPinnedOrMaximized;
            _contentTopMargin = isPinnedOrMaximized ? 4 : 6;
            UpdateContentOutsideMargin(ActualWidth);
            ApplyContentHostMargin();
        }

        public void SyncNavigationBarSelection(Type pageType, object? parameter)
        {
            if (pageType == typeof(WebBrowserPage) && parameter is WebAppShortcut shortcut)
            {
                NavBar.SelectWebAppItem(shortcut.Id);
            }
            else if (pageType == typeof(HomePage))
            {
                NavBar.SelectHomeItem();
            }
        }

        /// <summary>
        /// 重启当前标签页
        /// </summary>
        public async System.Threading.Tasks.Task RestartCurrentTabAsync()
        {
            await ContentHost.RestartCurrentTabAsync();
        }
    }
}
