using DockedTools.Features.Pages.Settings;
using DockedTools.Features.Localization;
using DockedTools.Features.UnifiedCalls.InAppDialog;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;
using Windows.System;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 新窗口请求处理模块
    /// 处理 target="_blank" 等新窗口打开请求
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        /// <summary>
        /// 处理新窗口请求事件
        /// 根据用户设置决定如何打开链接
        /// </summary>
        private async void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            try
            {
                var behavior = ExperimentalSettings.LinkOpenBehavior;
                var uri = e.Uri;
                
                System.Diagnostics.Debug.WriteLine($"[NewWindowRequested] 收到新窗口请求: {uri}, 行为: {behavior}, 用户触发: {e.IsUserInitiated}");
                
                // 阻止默认行为
                e.Handled = true;
                
                switch (behavior)
                {
                    case LinkOpenBehavior.Ask:
                        // 每次询问
                        await HandleAskBehaviorAsync(uri, e.IsUserInitiated);
                        break;
                    
                    case LinkOpenBehavior.SystemBrowser:
                        // 在系统默认浏览器打开
                        await LaunchSystemBrowserAsync(uri);
                        break;
                    
                    case LinkOpenBehavior.WebViewWindow:
                        // 在 WebView 窗口内打开
                        NavigateToUrl(uri);
                        break;
                    
                    default:
                        System.Diagnostics.Debug.WriteLine($"[NewWindowRequested] 未知行为: {behavior}");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NewWindowRequested] 处理失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 处理"每次询问"行为
        /// </summary>
        private async Task HandleAskBehaviorAsync(string uri, bool isUserInitiated)
        {
            try
            {
                // 非用户触发的弹窗(可能是广告)直接阻止
                if (!isUserInitiated)
                {
                    System.Diagnostics.Debug.WriteLine($"[HandleAskBehavior] 阻止非用户触发的弹窗: {uri}");
                    return;
                }
                
                // 确保在 UI 线程上执行
                if (!DispatcherQueue.HasThreadAccess)
                {
                    await DispatcherQueue.EnqueueAsync(async () => await ShowAskDialogAsync(uri));
                }
                else
                {
                    await ShowAskDialogAsync(uri);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HandleAskBehavior] 处理失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 显示询问对话框
        /// </summary>
        private async Task ShowAskDialogAsync(string uri)
        {
            // 创建复选框
            var rememberCheckBox = new CheckBox
            {
                Content = LocalizationHelper.GetString("LinkOpen_RememberChoice"),
                Margin = new Microsoft.UI.Xaml.Thickness(0, 12, 0, 0)
            };
            
            // 创建 StackPanel 包含内容和复选框
            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock
            {
                Text = string.Format(LocalizationHelper.GetString("LinkOpen_AskDialog_Content"), uri),
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            });
            stackPanel.Children.Add(rememberCheckBox);
            
            var dialog = new UnifiedInAppDialog();
            dialog.Configure(
                LocalizationHelper.GetString("LinkOpen_AskDialog_Title"),
                stackPanel,
                primaryButtonText: LocalizationHelper.GetString("LinkOpen_SystemBrowser"),
                secondaryButtonText: LocalizationHelper.GetString("LinkOpen_WebViewWindow"),
                closeButtonText: LocalizationHelper.GetString("LinkOpen_Cancel")
            );
            
            var result = await InAppDialogService.ShowAsync(dialog, this);
            
            // 检查是否勾选"始终使用"
            bool rememberChoice = rememberCheckBox.IsChecked == true;
            
            if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                // 用户选择系统浏览器
                if (rememberChoice)
                {
                    ExperimentalSettings.LinkOpenBehavior = LinkOpenBehavior.SystemBrowser;
                    System.Diagnostics.Debug.WriteLine("[HandleAskBehavior] 已保存选择: SystemBrowser");
                }
                await LaunchSystemBrowserAsync(uri);
            }
            else if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Secondary)
            {
                // 用户选择 WebView 窗口
                if (rememberChoice)
                {
                    ExperimentalSettings.LinkOpenBehavior = LinkOpenBehavior.WebViewWindow;
                    System.Diagnostics.Debug.WriteLine("[HandleAskBehavior] 已保存选择: WebViewWindow");
                }
                NavigateToUrl(uri);
            }
            // 否则取消,不做任何操作
        }
        
        /// <summary>
        /// 在系统默认浏览器中打开链接
        /// </summary>
        private async Task LaunchSystemBrowserAsync(string uri)
        {
            try
            {
                if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? uriObj))
                {
                    bool success = await Launcher.LaunchUriAsync(uriObj);
                    System.Diagnostics.Debug.WriteLine($"[LaunchSystemBrowser] 启动系统浏览器: {uri}, 结果: {success}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[LaunchSystemBrowser] 无效的 URI: {uri}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LaunchSystemBrowser] 启动失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 在当前 WebView 窗口中导航到 URL
        /// </summary>
        private void NavigateToUrl(string uri)
        {
            try
            {
                if (WebView?.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.Navigate(uri);
                    System.Diagnostics.Debug.WriteLine($"[NavigateToUrl] 在 WebView 中打开: {uri}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[NavigateToUrl] WebView 未初始化");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigateToUrl] 导航失败: {ex.Message}");
            }
        }
    }
}
