using DockedTools.Features.Pages.Settings;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - WebView配置模块
    /// 包含右键菜单配置、性能设置、脚本注入等
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        // ⚠️ OnWinUIContextMenuSettingsChanged、UpdateContextMenuConfiguration、UpdateContextMenuForWebView已移至 网页浏览页面.ContextMenu.cs

        private void OnWebViewPerformanceSettingsChanged(object? sender, EventArgs e)
        {
            // 性能设置改变时，应用新设置
            // 注意：某些设置需要重启 WebView 才能生效（如浏览器参数）
            ApplyMemoryModeSettings();
            
            System.Diagnostics.Debug.WriteLine("[OnWebViewPerformanceSettingsChanged] 性能设置已更新，某些设置需要重新加载页面才能生效");
        }

        // ⚠️ UpdateContextMenuConfiguration、UpdateContextMenuForWebView已移至 网页浏览页面.ContextMenu.cs

        // ⚠️ EnsureTintScriptInstalledAsync已移至 网页浏览页面.WebView.cs
    }
}
