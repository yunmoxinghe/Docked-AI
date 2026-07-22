using System;
using Microsoft.Web.WebView2.Core;

namespace DockedTools.功能.WebView备份.Services;

/// <summary>
/// 全局 WebView2 实例管理服务
/// 用于在应用中共享当前活跃的 WebView2 实例
/// </summary>
public static class GlobalWebViewService
{
    private static CoreWebView2? _currentWebView2;

    /// <summary>
    /// 当前活跃的 WebView2 实例
    /// </summary>
    public static CoreWebView2? CurrentWebView2
    {
        get => _currentWebView2;
        set
        {
            _currentWebView2 = value;
            System.Diagnostics.Debug.WriteLine($"[GlobalWebViewService] WebView2 已{(value == null ? "清除" : "设置")}");
        }
    }

    /// <summary>
    /// 检查是否有可用的 WebView2 实例
    /// </summary>
    public static bool HasActiveWebView => _currentWebView2 != null;

    /// <summary>
    /// 清除当前 WebView2 引用
    /// </summary>
    public static void Clear()
    {
        _currentWebView2 = null;
        System.Diagnostics.Debug.WriteLine("[GlobalWebViewService] WebView2 引用已清除");
    }
}
