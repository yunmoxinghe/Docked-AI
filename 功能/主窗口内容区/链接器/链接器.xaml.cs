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

        public Linker()
        {
            InitializeComponent();
            TopAppBarService.Register(ContentHost);
            ContentHost.Navigate(typeof(HomePage));
            ContentHost.Navigated += ContentHost_Navigated;
            ContentHost.CachedPageNavigated += ContentHost_CachedPageNavigated;
            ContentHost.PageCloseRequested += OnPageCloseRequested;
            NavBar.NavigationRequested += OnNavigationRequested;
            NavBar.DockToggleRequested += OnDockToggleRequested;
            NavBar.WindowStateToggleRequested += OnWindowStateToggleRequested;
            NavBar.ShortcutRemoved += OnShortcutRemoved;
            NavBar.WebAppRestartRequested += OnWebAppRestartRequested;
            NavBar.BackRequested += OnBackRequested;
            
            // 订阅 AI 实验室设置变化事件
            Pages.Settings.SettingsPage.AILabSettingsChanged += OnAILabSettingsChanged;
        }

        private void OnAILabSettingsChanged(object? sender, EventArgs e)
        {
            // 通知导航栏更新 AI 导航项的可见性
            NavBar.UpdateAINavigationItemVisibility();
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
            ContentHost.RemoveCachedPage(shortcutId);
            ContentHost.Navigate(typeof(HomePage));
            NavBar.SelectHomeItem();
            NavBar.UpdateBackButtonVisibility(false);
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
        }

        private void ContentHost_CachedPageNavigated(object? sender, (Type PageType, object? Parameter) e)
        {
            SyncNavigationBarSelection(e.PageType, e.Parameter);
            _isNavigatingBack = false;
            NavBar.UpdateBackButtonVisibility(ContentHost.CanGoBack);
            // 顶栏返回按钮由 ContentArea 内部在 CachedPageNavigated 时自动刷新，无需在此处理
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
            double topMargin = isPinnedOrMaximized ? 4 : 6;
            var currentMargin = ContentHost.Margin;
            ContentHost.Margin = new Thickness(currentMargin.Left, topMargin, currentMargin.Right, currentMargin.Bottom);
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
