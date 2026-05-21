using Docked_AI.Features.Pages.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Docked_AI.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// WebView 实例管理器，用于跟踪和限制同时打开的 WebView 数量
    /// 负责自动驱逐最旧的 WebView 以保持数量限制
    /// </summary>
    public static class WebViewManager
    {
        private static readonly Dictionary<string, WeakReference<WebBrowserPage>> _activeWebViews = new();
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
                    CleanupDeadReferences();
                    return _activeWebViews.Count;
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
                CleanupDeadReferences();
                return _activeWebViews.Count < MaxCount;
            }
        }

        /// <summary>
        /// 请求链接 WebView（统一入口，自动处理驱逐）
        /// </summary>
        /// <param name="instanceId">实例唯一标识符</param>
        /// <param name="page">WebBrowserPage 实例</param>
        /// <returns>链接结果</returns>
        public static WebViewLinkResult RequestLink(string instanceId, WebBrowserPage page)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                throw new ArgumentException("Instance ID cannot be null or empty", nameof(instanceId));
            }

            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            lock (_lock)
            {
                CleanupDeadReferences();

                // 如果已经链接，直接返回成功
                if (_activeWebViews.ContainsKey(instanceId))
                {
                    System.Diagnostics.Debug.WriteLine($"[WebViewManager] WebView 已链接: {instanceId}");
                    return new WebViewLinkResult { Success = true, AlreadyLinked = true };
                }

                // ⭐ 立即注册新的（不管是否超限）
                _activeWebViews[instanceId] = new WeakReference<WebBrowserPage>(page);
                System.Diagnostics.Debug.WriteLine($"[WebViewManager] 链接 WebView: {instanceId}, 当前数量: {_activeWebViews.Count}/{MaxCount}");

                // 如果超限，异步驱逐最旧的
                if (_activeWebViews.Count > MaxCount)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebViewManager] 超出限制，异步驱逐最旧的 WebView");

                    // 找到最旧的 WebView（排除刚注册的）
                    var oldestEntry = FindOldestWebView(instanceId);
                    if (oldestEntry != null)
                    {
                        var oldKey = oldestEntry.Value.Key;
                        var oldPageRef = oldestEntry.Value.Value;

                        // ⭐ 立即从字典中移除（这样 DisposeWebView 中的 Unlink 就不会重复）
                        _activeWebViews.Remove(oldKey);
                        System.Diagnostics.Debug.WriteLine($"[WebViewManager] 已从字典移除: {oldKey}, 当前数量: {_activeWebViews.Count}/{MaxCount}");

                        // 异步清理资源（完全不阻塞）
                        _ = System.Threading.Tasks.Task.Run(() =>
                        {
                            try
                            {
                                if (oldPageRef.TryGetTarget(out var oldPage))
                                {
                                    oldPage.DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                                    {
                                        try
                                        {
                                            // ⭐ 注意：DisposeWebView 内部会调用 Unlink，但因为已经从字典移除，所以不会有问题
                                            oldPage.DisposeWebView();
                                            System.Diagnostics.Debug.WriteLine($"[WebViewManager] 异步驱逐完成: {oldKey}");
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[WebViewManager] 异步驱逐时出错: {ex.Message}");
                                        }
                                    });
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[WebViewManager] 旧页面已被 GC 回收: {oldKey}");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[WebViewManager] 异步驱逐失败: {ex.Message}");
                            }
                        });

                        return new WebViewLinkResult { Success = true, AlreadyLinked = false, EvictedOldest = true };
                    }
                }

                return new WebViewLinkResult { Success = true, AlreadyLinked = false };
            }
        }

        /// <summary>
        /// 取消链接 WebView
        /// </summary>
        /// <param name="instanceId">实例唯一标识符</param>
        public static void Unlink(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return;
            }

            lock (_lock)
            {
                bool removed = _activeWebViews.Remove(instanceId);
                if (removed)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebViewManager] 取消链接 WebView: {instanceId}, 当前数量: {_activeWebViews.Count}/{MaxCount}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WebViewManager] 尝试取消链接不存在的 WebView: {instanceId}");
                }
            }
        }

        /// <summary>
        /// 检查指定的 WebView 是否已链接
        /// </summary>
        public static bool IsLinked(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return false;
            }

            lock (_lock)
            {
                CleanupDeadReferences();
                return _activeWebViews.ContainsKey(instanceId);
            }
        }

        /// <summary>
        /// 获取所有活跃的 WebView ID 列表（用于调试）
        /// </summary>
        public static string[] GetActiveWebViewIds()
        {
            lock (_lock)
            {
                CleanupDeadReferences();
                return _activeWebViews.Keys.ToArray();
            }
        }

        /// <summary>
        /// 清除所有链接的 WebView（用于重置或测试）
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _activeWebViews.Clear();
                System.Diagnostics.Debug.WriteLine("[WebViewManager] 已清除所有 WebView 链接");
            }
        }

        /// <summary>
        /// 诊断当前 WebView 链接状态（用于调试）
        /// </summary>
        public static void DiagnoseState()
        {
            lock (_lock)
            {
                CleanupDeadReferences();
                System.Diagnostics.Debug.WriteLine("========== WebView 状态诊断 ==========");
                System.Diagnostics.Debug.WriteLine($"当前链接数量: {_activeWebViews.Count}/{MaxCount}");
                System.Diagnostics.Debug.WriteLine($"可以创建新实例: {CanCreateNew()}");
                
                if (_activeWebViews.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("已链接的 WebView ID:");
                    foreach (var kvp in _activeWebViews)
                    {
                        bool isAlive = kvp.Value.TryGetTarget(out _);
                        System.Diagnostics.Debug.WriteLine($"  - {kvp.Key} (alive: {isAlive})");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("没有已链接的 WebView");
                }
                
                System.Diagnostics.Debug.WriteLine("=====================================");
            }
        }

        /// <summary>
        /// 清理已失效的弱引用
        /// </summary>
        private static void CleanupDeadReferences()
        {
            var deadKeys = _activeWebViews
                .Where(kvp => !kvp.Value.TryGetTarget(out _))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in deadKeys)
            {
                _activeWebViews.Remove(key);
                System.Diagnostics.Debug.WriteLine($"[WebViewManager] 清理僵尸链接: {key}");
            }
        }

        /// <summary>
        /// 找到最旧的 WebView（排除指定的 ID）
        /// </summary>
        private static KeyValuePair<string, WeakReference<WebBrowserPage>>? FindOldestWebView(string excludeId)
        {
            // 简单策略：返回第一个不是 excludeId 的
            // 更复杂的策略可以基于访问时间戳
            foreach (var kvp in _activeWebViews)
            {
                if (kvp.Key != excludeId && kvp.Value.TryGetTarget(out _))
                {
                    return kvp;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// WebView 链接结果
    /// </summary>
    public class WebViewLinkResult
    {
        /// <summary>
        /// 是否成功链接
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 是否已经链接过（重复链接）
        /// </summary>
        public bool AlreadyLinked { get; set; }

        /// <summary>
        /// 是否驱逐了最旧的 WebView
        /// </summary>
        public bool EvictedOldest { get; set; }

        /// <summary>
        /// 错误消息（如果失败）
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
