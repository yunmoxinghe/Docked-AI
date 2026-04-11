using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.Pages.Test
{
    /// <summary>
    /// WebView2 圆角测试页面
    /// 测试使用 Border 和 Grid 包住 WebView2 并应用 8px 圆角
    /// </summary>
    public sealed partial class WebView2RoundedCornerTestPage : Page
    {
        public WebView2RoundedCornerTestPage()
        {
            this.InitializeComponent();
            this.Loaded += WebView2RoundedCornerTestPage_Loaded;
        }

        private async void WebView2RoundedCornerTestPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 确保 WebView2 已初始化
            try
            {
                await WebViewInBorder.EnsureCoreWebView2Async();
                await WebViewInGrid.EnsureCoreWebView2Async();
                await WebViewRecommended.EnsureCoreWebView2Async();
                await WebViewNoBorder.EnsureCoreWebView2Async();
                await WebViewTransparent.EnsureCoreWebView2Async();
            }
            catch (System.Exception ex)
            {
                // 处理初始化错误
                var dialog = new ContentDialog
                {
                    Title = "WebView2 初始化失败",
                    Content = $"错误信息: {ex.Message}",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private void RefreshAll_Click(object sender, RoutedEventArgs e)
        {
            // 刷新所有 WebView2
            WebViewInBorder?.Reload();
            WebViewInGrid?.Reload();
            WebViewRecommended?.Reload();
            WebViewNoBorder?.Reload();
            WebViewTransparent?.Reload();
        }

        private void NavigateToTest_Click(object sender, RoutedEventArgs e)
        {
            // 导航到一个测试页面，便于观察圆角效果
            string testUrl = "https://www.example.com";
            
            if (WebViewInBorder?.CoreWebView2 != null)
                WebViewInBorder.CoreWebView2.Navigate(testUrl);
            
            if (WebViewInGrid?.CoreWebView2 != null)
                WebViewInGrid.CoreWebView2.Navigate(testUrl);
            
            if (WebViewRecommended?.CoreWebView2 != null)
                WebViewRecommended.CoreWebView2.Navigate(testUrl);
            
            if (WebViewNoBorder?.CoreWebView2 != null)
                WebViewNoBorder.CoreWebView2.Navigate(testUrl);
            
            if (WebViewTransparent?.CoreWebView2 != null)
                WebViewTransparent.CoreWebView2.Navigate(testUrl);
        }
    }
}
