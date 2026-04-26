using Docked_AI.Features.MainWindowContent.ContentArea;
using Docked_AI.Features.Pages.Home;
using Docked_AI.Features.Pages.New;
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

        public Linker()
        {
            InitializeComponent();
            TopAppBarService.Register(ContentHost);
            ContentHost.Navigate(typeof(HomePage));
            ContentHost.Navigated += ContentHost_Navigated;
            ContentHost.PageCloseRequested += OnPageCloseRequested;
            NavBar.NavigationRequested += OnNavigationRequested;
            NavBar.DockToggleRequested += OnDockToggleRequested;
            NavBar.WindowStateToggleRequested += OnWindowStateToggleRequested;
            NavBar.ShortcutRemoved += OnShortcutRemoved;
            NavBar.WebAppRestartRequested += OnWebAppRestartRequested;
            
            // 订阅 AI 实验室设置变化事件
            Pages.Settings.SettingsPage.AILabSettingsChanged += OnAILabSettingsChanged;
        }

        private void OnAILabSettingsChanged(object? sender, EventArgs e)
        {
            // 通知导航栏更新 AI 导航项的可见性
            NavBar.UpdateAINavigationItemVisibility();
        }

        private void OnPageCloseRequested(object? sender, string shortcutId)
        {
            System.Diagnostics.Debug.WriteLine($"[Linker] 收到页面关闭请求: {shortcutId}");
            
            // 清除缓存和注销 WebView
            ContentHost.RemoveCachedPage(shortcutId);
            
            // 导航回主页
            ContentHost.Navigate(typeof(HomePage));
            NavBar.SelectHomeItem();
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
        }

        private void OnNavigationRequested(object? sender, NavRequest request)
        {
            ContentHost.Navigate(request.PageType, request.Parameter);
        }

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
