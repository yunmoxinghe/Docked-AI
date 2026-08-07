using DockedTools.Core.Async;
using Microsoft.Web.WebView2.Core;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 消息处理模块
    /// 包含WebView消息接收、解析、处理逻辑
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        /// <summary>
        /// ⭐ 任务 6.3：CoreWebView2_WebMessageReceived 事件入口（委托到异步实现）
        /// </summary>
        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            AsyncSafety.Run(
                async () => await CoreWebView2WebMessageReceivedAsync(sender, e),
                "WebBrowserPage",
                "WebMessageReceived");
        }

        /// <summary>
        /// ⭐ 任务 6.3：CoreWebView2_WebMessageReceived 异步实现
        /// </summary>
        private async Task CoreWebView2WebMessageReceivedAsync(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string json = e.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                if (!root.TryGetProperty("type", out JsonElement typeEl))
                {
                    return;
                }

                string messageType = typeEl.GetString() ?? string.Empty;

                // 处理 theme-color 消息（优先级最高）
                if (string.Equals(messageType, ThemeColorMessageType, StringComparison.Ordinal))
                {
                    if (root.TryGetProperty("color", out JsonElement colorEl) &&
                        TryParseCssColor(colorEl.GetString(), out var themeColor))
                    {
                        _hasAppliedThemeColor = true;
                        ApplyBarTint(isTop: true, themeColor);
                        ApplyBarTint(isTop: false, themeColor);
                    }
                    return;
                }

                // 处理采样颜色消息
                if (string.Equals(messageType, TintMessageType, StringComparison.Ordinal))
                {
                    // 如果已经应用了 theme-color，跳过采样颜色
                    if (_hasAppliedThemeColor)
                    {
                        return;
                    }

                    bool isTransparent = root.TryGetProperty("isTransparent", out JsonElement transparentEl) && 
                                        transparentEl.GetBoolean();

                    // 如果页面完全透明，尝试截图采样
                    if (isTransparent)
                    {
                        await TryScreenshotSamplingAsync();
                        return;
                    }

                    if (root.TryGetProperty("top", out JsonElement topEl) &&
                        TryParseCssColor(topEl.GetString(), out var topColor))
                    {
                        ApplyBarTint(isTop: true, topColor);
                    }

                    if (root.TryGetProperty("bottom", out JsonElement bottomEl) &&
                        TryParseCssColor(bottomEl.GetString(), out var bottomColor))
                    {
                        ApplyBarTint(isTop: false, bottomColor);
                    }
                }
            }
            catch
            {
                // Ignore malformed messages.
            }
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
    }
}
