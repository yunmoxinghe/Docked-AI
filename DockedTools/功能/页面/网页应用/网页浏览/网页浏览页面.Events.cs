using DockedTools.Features.UnifiedCalls.AsyncSafety;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 事件处理器模块
    /// 包含页面生命周期、WebView导航、按钮点击等所有事件处理逻辑
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        // 页面关闭请求事件
        public event EventHandler<string>? PageCloseRequested;

        /// <summary>
        /// ⭐ 任务 6.3：WebBrowserPage_Loaded 事件入口（委托到异步实现）
        /// </summary>
        private async void WebBrowserPage_Loaded(object sender, RoutedEventArgs e)
        {
            AsyncSafety.Run(
                async () => await WebBrowserPageLoadedAsync(sender, e),
                "WebBrowserPage",
                "Loaded");
        }

        /// <summary>
        /// ⭐ 任务 6.3：WebBrowserPage_Loaded 异步实现
        /// </summary>
        private async Task WebBrowserPageLoadedAsync(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] Loaded 事件触发");
            
            // Loaded 事件只负责初始化 WebView，不干预导航和链接管理
            // 链接管理由 INavigationAware.OnNavigatedTo 负责
            
            await EnsureWebViewInitializedAsync();
            TryNavigatePendingUri();
        }

        private void WebBrowserPage_Unloaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] Unloaded 事件触发");
            
            // 取消订阅更新事件
            Shared.WebAppUpdateService.UpdateCompleted -= OnWebAppUpdated;
            
            // 延迟检查：如果页面真的被移除了（不在可视树中），才清理
            // 使用 DispatcherQueue 延迟执行，让 Loaded 事件有机会先触发
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                // 检查页面是否还在可视树中
                if (XamlRoot == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 页面已从可视树移除，但由于缓存机制不清理顶部栏");
                    // 注意：即使页面从可视树移除，也不清理顶部栏
                    // 因为页面可能被缓存，稍后会恢复
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 页面仍在可视树中，Unloaded 是误触发");
                }
            });
        }

        /// <summary>
        /// 系统主题切换时的回调
        /// </summary>
        private void OnSystemThemeChanged(FrameworkElement sender, object args)
        {
            System.Diagnostics.Debug.WriteLine("═══════════════════════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("[WebBrowserPage] ✅✅✅ ActualThemeChanged 事件触发！");
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 当前 ActualTheme: {ActualTheme}");
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] WebView 状态: CoreWebView2={(WebView?.CoreWebView2 != null ? "✓" : "✗")}, IsReady={_isWebViewReady}");
            System.Diagnostics.Debug.WriteLine("═══════════════════════════════════════════════════════");
            
            // 重新从主题资源获取颜色
            UpdateForegroundColorsFromTheme();
            
            // ✅ 立即更新 TopAppBar 的前景色（包括关闭按钮等）
            TopAppBarService.SetForeground(_topBarForegroundBrush);
            System.Diagnostics.Debug.WriteLine("[WebBrowserPage] TopAppBar 前景色已更新");
            
            // ✅ 核心修复：系统主题切换后，WebView2 内部的网页会自动响应（CSS prefers-color-scheme），
            // 但不会触发 NavigationCompleted 事件，所以我们需要手动触发完整的取色逻辑
            
            if (WebView?.CoreWebView2 != null && _isWebViewReady)
            {
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] WebView 已就绪，强制重新提取网页主题色");
                
                // ✅ 重置取色状态，让取色逻辑重新执行
                _hasReceivedFirstTint = false;
                _hasAppliedThemeColor = false;
                
                // ⭐ 任务 6.4：使用 AsyncSafety 包装 DispatcherQueue.TryEnqueue 中的 async lambda
                AsyncSafety.TryEnqueue(
                    DispatcherQueue,
                    async () =>
                    {
                        System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 等待 500ms 让 WebView2 完成主题切换...");
                        
                        // 等待网页重新渲染（prefers-color-scheme CSS 生效）
                        await Task.Delay(500);
                        
                        System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 开始执行主题切换后的取色");
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 取色前背景色: Top={_topBarBackgroundBrush.Color}, Bottom={_bottomBarBackgroundBrush.Color}");
                        
                        // ✅ 步骤1：尝试 meta theme-color
                        await TryApplyThemeColorAsync();
                        
                        // ✅ 步骤2：如果没有 theme-color，使用脚本采样
                        if (!_hasAppliedThemeColor)
                        {
                            System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 没有 theme-color，触发脚本采样取色");
                            await Task.Delay(100);
                            await TriggerTintSamplingAsync();
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 取色完成后背景色: Top={_topBarBackgroundBrush.Color}, Bottom={_bottomBarBackgroundBrush.Color}");
                    },
                    "WebBrowserPage",
                    "ThemeChanged");
            }
            else
            {
                // WebView 还没准备好，只更新前景色
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] ⚠️ WebView 未就绪，仅更新前景色");
            }
        }

        private void OnWinUIContextMenuSettingsChanged(object? sender, EventArgs e)
        {
            // 设置改变时，更新右键菜单配置
            bool useWinUIContextMenu = ExperimentalSettings.EnableWinUIContextMenu;
            
            // 更新 WebView 的配置
            if (WebView?.CoreWebView2 != null)
            {
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = !useWinUIContextMenu;
                UpdateContextMenuForWebView(WebView, useWinUIContextMenu);
            }
        }

        private void OnWebViewPerformanceSettingsChanged(object? sender, EventArgs e)
        {
            // 性能设置改变时，应用新设置
            // 注意：某些设置需要重启 WebView 才能生效（如浏览器参数）
            ApplyMemoryModeSettings();
            
            System.Diagnostics.Debug.WriteLine("[OnWebViewPerformanceSettingsChanged] 性能设置已更新，某些设置需要重新加载页面才能生效");
        }

        /// <summary>
        /// 处理网页应用配置更新事件
        /// </summary>
        private async void OnWebAppUpdated(object? sender, Shared.WebAppUpdateEventArgs e)
        {
            // 只处理当前快捷方式的更新
            if (_currentShortcut == null || e.AppId != _currentShortcut.Id)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 收到更新通知: {e.AppId}, 类型: {e.UpdateType}");

            // 如果按钮配置有变化，重新加载快捷方式并刷新按钮
            if (e.UpdateType.HasFlag(Shared.WebAppUpdateType.ButtonConfig))
            {
                await DispatcherQueue.EnqueueAsync(async () =>
                {
                    try
                    {
                        // 重新加载快捷方式数据
                        var shortcuts = await Shared.WebAppShortcutStore.LoadAsync();
                        var updatedShortcut = shortcuts.FirstOrDefault(s => s.Id == e.AppId);
                        
                        if (updatedShortcut != null)
                        {
                            _currentShortcut = updatedShortcut;
                            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 快捷方式已重新加载: {updatedShortcut.Name}");
                            
                            // 重新设置顶部栏（刷新按钮）
                            SetupTopBar();
                            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 按钮配置已刷新");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 更新按钮配置失败: {ex.Message}");
                    }
                });
            }

            // 如果名称或图标有变化，更新标题栏
            if (e.UpdateType.HasFlag(Shared.WebAppUpdateType.Name) || 
                e.UpdateType.HasFlag(Shared.WebAppUpdateType.Icon))
            {
                await DispatcherQueue.EnqueueAsync(async () =>
                {
                    try
                    {
                        var shortcuts = await Shared.WebAppShortcutStore.LoadAsync();
                        var updatedShortcut = shortcuts.FirstOrDefault(s => s.Id == e.AppId);
                        
                        if (updatedShortcut != null)
                        {
                            _currentShortcut = updatedShortcut;
                            UpdateTopBarContent();
                            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 标题栏已更新");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 更新标题栏失败: {ex.Message}");
                    }
                });
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // ✅ 修复：不在导航开始时重置取色状态
            // 改为在导航完成后再重置，避免脚本注入前状态被清空
            // _hasReceivedFirstTint = false;
            // _hasAppliedThemeColor = false;
            
            // 显示加载条
            DispatcherQueue.TryEnqueue(() =>
            {
                if (LoadingProgressBar != null)
                {
                    LoadingProgressBar.IsIndeterminate = true;
                    LoadingProgressBar.Visibility = Visibility.Visible;
                }
            });
        }

        /// <summary>
        /// ⭐ 任务 6.3：CoreWebView2_NavigationCompleted 事件入口（委托到异步实现）
        /// </summary>
        private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            AsyncSafety.Run(
                async () => await CoreWebView2NavigationCompletedAsync(sender, e),
                "WebBrowserPage",
                "NavigationCompleted");
        }


        /// <summary>
        /// ⭐ 任务 6.3：CoreWebView2_NavigationCompleted 异步实现
        /// </summary>
        private async Task CoreWebView2NavigationCompletedAsync(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            UpdateNavigationButtonStates();
            
            // ⭐ 任务 3.4：导航成功后重置无响应计数器
            _unresponsiveCount = 0;
            
            // ✅ 修复：在导航完成时重置取色状态
            // 确保新页面能重新取色
            _hasReceivedFirstTint = false;
            _hasAppliedThemeColor = false;
            System.Diagnostics.Debug.WriteLine("[CoreWebView2_NavigationCompleted] 取色状态已重置");
            
            // 平滑隐藏加载条：先停止动画，等待当前周期完成，再隐藏
            await HideLoadingProgressBarSmoothlyAsync();
            
            // ✅ 修复：等待页面渲染完成后再取色
            // 延迟 300ms 确保 DOM 完全加载和渲染（原来 200ms 不够）
            await Task.Delay(300);
            
            // 分层取色策略：优先使用 theme-color
            await TryApplyThemeColorAsync();
            
            // ✅ 修复：如果没有 theme-color，主动触发一次采样取色
            if (!_hasAppliedThemeColor)
            {
                System.Diagnostics.Debug.WriteLine("[CoreWebView2_NavigationCompleted] 没有 theme-color，触发采样取色");
                
                // 再等待 100ms，确保脚本的 load 事件已触发
                await Task.Delay(100);
                await TriggerTintSamplingAsync();
            }
        }

        private void CoreWebView2_HistoryChanged(object? sender, object e)
        {
            UpdateNavigationButtonStates();
        }

        private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
        {
            if (WebView?.CoreWebView2 is null)
            {
                return;
            }

            string title = WebView.CoreWebView2.DocumentTitle ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                if (_currentShortcut is not null && !string.IsNullOrWhiteSpace(_currentShortcut.Name))
                {
                    if (_topBarTitle != null)
                    {
                        _topBarTitle.Text = _currentShortcut.Name;
                    }
                }

                return;
            }

            if (_topBarTitle != null)
            {
                _topBarTitle.Text = title;
            }
        }


        // ==================== 按钮点击事件 ====================

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebView != null && WebView.CanGoBack)
            {
                WebView.GoBack();
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebView != null && WebView.CanGoForward)
            {
                WebView.GoForward();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            TryReloadWebView();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 关闭当前页面
            if (_currentShortcut != null)
            {
                System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 用户点击关闭按钮: {_currentShortcut.Id}");
                
                // 触发关闭事件，通知导航栏和内容区域
                PageCloseRequested?.Invoke(this, _currentShortcut.Id);
            }
        }

        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            Uri? uri = WebView?.Source;
            if (uri is null)
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(uri.AbsoluteUri);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }

        private async void OpenExternalButton_Click(object sender, RoutedEventArgs e)
        {
            Uri? uri = WebView?.Source;
            if (uri is null)
            {
                return;
            }

            var dialog = CreateExternalOpenDialog(uri);
            var result = await InAppDialogService.ShowAsync(dialog, this);
            if (result == ContentDialogResult.Primary)
            {
                await Windows.System.Launcher.LaunchUriAsync(uri);
            }
        }
    }
}
