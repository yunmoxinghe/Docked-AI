using Docked_AI.Features.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Docked_AI.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// WebView 实例管理器，用于跟踪和限制同时打开的 WebView 数量
    /// </summary>
    public static class WebViewManager
    {
        private static readonly HashSet<string> _activeWebViewIds = new();
        private static readonly object _lock = new();

        /// <summary>
        /// 获取当前活跃的 WebView 数量
        /// </summary>
        public static int ActiveCount
        {
            get
            {
                lock (_lock)
                {
                    return _activeWebViewIds.Count;
                }
            }
        }

        /// <summary>
        /// 获取最大允许的 WebView 数量
        /// </summary>
        public static int MaxCount => ExperimentalSettings.MaxWebViewCount;

        /// <summary>
        /// 检查是否可以创建新的 WebView
        /// </summary>
        public static bool CanCreateNew()
        {
            lock (_lock)
            {
                return _activeWebViewIds.Count < MaxCount;
            }
        }

        /// <summary>
        /// 注册一个新的 WebView 实例
        /// </summary>
        /// <param name="instanceId">实例唯一标识符</param>
        /// <returns>如果注册成功返回 true，如果已达到限制返回 false</returns>
        public static bool RegisterWebView(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                throw new ArgumentException("Instance ID cannot be null or empty", nameof(instanceId));
            }

            lock (_lock)
            {
                if (_activeWebViewIds.Count >= MaxCount)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebViewManager] 已达到 WebView 数量限制: {MaxCount}");
                    return false;
                }

                bool added = _activeWebViewIds.Add(instanceId);
                if (added)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebViewManager] 注册 WebView: {instanceId}, 当前数量: {_activeWebViewIds.Count}/{MaxCount}");
                }
                return added;
            }
        }

        /// <summary>
        /// 注销一个 WebView 实例
        /// </summary>
        /// <param name="instanceId">实例唯一标识符</param>
        public static void UnregisterWebView(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return;
            }

            lock (_lock)
            {
                bool removed = _activeWebViewIds.Remove(instanceId);
                if (removed)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebViewManager] 注销 WebView: {instanceId}, 当前数量: {_activeWebViewIds.Count}/{MaxCount}");
                }
            }
        }

        /// <summary>
        /// 检查指定的 WebView 是否已注册
        /// </summary>
        public static bool IsRegistered(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return false;
            }

            lock (_lock)
            {
                return _activeWebViewIds.Contains(instanceId);
            }
        }

        /// <summary>
        /// 获取所有活跃的 WebView ID 列表（用于调试）
        /// </summary>
        public static string[] GetActiveWebViewIds()
        {
            lock (_lock)
            {
                return _activeWebViewIds.ToArray();
            }
        }

        /// <summary>
        /// 清除所有注册的 WebView（用于重置或测试）
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _activeWebViewIds.Clear();
                System.Diagnostics.Debug.WriteLine("[WebViewManager] 已清除所有 WebView 注册");
            }
        }
    }
}
