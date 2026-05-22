using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Docked_AI.Features.MainWindowContent.ContentArea
{
    /// <summary>
    /// 页面缓存管理器，用于缓存已创建的页面实例，实现快速切换
    /// 使用 LRU（最近最少使用）策略自动管理缓存
    /// 线程安全：所有公共方法使用锁保护
    /// AOT 兼容：使用工厂模式替代反射创建页面实例
    /// </summary>
    public class PageCacheManager
    {
        private readonly Dictionary<string, Page> _cachedPages = new();
        private readonly LinkedList<string> _accessOrder = new(); // 记录访问顺序，最新的在前面
        private readonly Dictionary<string, LinkedListNode<string>> _accessNodes = new();
        private readonly int _maxCacheSize;
        private string? _currentPageKey;
        private readonly object _cacheLock = new(); // 线程安全锁
        
        // AOT 兼容：页面工厂字典（避免使用反射）
        private static readonly Dictionary<Type, Func<Page>> _pageFactories = new()
        {
            { typeof(Pages.Home.HomePage), () => new Pages.Home.HomePage() },
            { typeof(Pages.New.NewPage), () => new Pages.New.NewPage() },
            { typeof(Pages.AI.AIPage), () => new Pages.AI.AIPage() },
            { typeof(Pages.Settings.SettingsPage), () => new Pages.Settings.SettingsPage() },
            { typeof(Pages.Lab.LabPage), () => new Pages.Lab.LabPage() },
            { typeof(Pages.WebApp.Browser.WebBrowserPage), () => new Pages.WebApp.Browser.WebBrowserPage() },
            { typeof(Pages.WebApp.WebAppPage), () => new Pages.WebApp.WebAppPage() }
        };

        public PageCacheManager(int maxCacheSize = 20)
        {
            _maxCacheSize = maxCacheSize;
        }

        /// <summary>
        /// 获取或创建页面实例（AOT 兼容版本）
        /// </summary>
        /// <param name="pageType">页面类型</param>
        /// <param name="parameter">导航参数</param>
        /// <param name="cacheKey">缓存键（如果为 null 则不缓存）</param>
        /// <returns>页面实例</returns>
        public Page GetOrCreatePage(Type pageType, object? parameter, string? cacheKey)
        {
            // 如果没有缓存键，直接创建新实例（不需要锁）
            if (string.IsNullOrEmpty(cacheKey))
            {
                return CreatePageInstance(pageType);
            }

            lock (_cacheLock)
            {
                // 检查缓存中是否已存在
                if (_cachedPages.TryGetValue(cacheKey, out Page? cachedPage))
                {
                    System.Diagnostics.Debug.WriteLine($"[PageCacheManager] 使用缓存页面: {cacheKey}");
                    _currentPageKey = cacheKey;
                    
                    // 更新访问顺序（移到最前面）
                    UpdateAccessOrderUnsafe(cacheKey);
                    
                    return cachedPage;
                }

                // 创建新实例并缓存
                var newPage = CreatePageInstance(pageType);
                AddPageToCacheUnsafe(cacheKey, newPage);
                
                return newPage;
            }
        }

        /// <summary>
        /// 将已存在的页面实例添加到缓存
        /// </summary>
        public void AddPageToCache(string cacheKey, Page page)
        {
            if (string.IsNullOrEmpty(cacheKey))
            {
                throw new ArgumentException("Cache key cannot be null or empty", nameof(cacheKey));
            }

            lock (_cacheLock)
            {
                AddPageToCacheUnsafe(cacheKey, page);
            }
        }

        /// <summary>
        /// 将已存在的页面实例添加到缓存（不加锁，内部使用）
        /// </summary>
        private void AddPageToCacheUnsafe(string cacheKey, Page page)
        {
            // 如果已存在，更新访问顺序
            if (_cachedPages.ContainsKey(cacheKey))
            {
                UpdateAccessOrderUnsafe(cacheKey);
                return;
            }

            // 检查缓存大小限制
            if (_cachedPages.Count >= _maxCacheSize)
            {
                RemoveLeastRecentlyUsedPageUnsafe();
            }

            _cachedPages[cacheKey] = page;
            _currentPageKey = cacheKey;
            
            // 添加到访问顺序列表（最前面）
            var node = _accessOrder.AddFirst(cacheKey);
            _accessNodes[cacheKey] = node;
            
            System.Diagnostics.Debug.WriteLine($"[PageCacheManager] 添加页面到缓存: {cacheKey}, 当前缓存数: {_cachedPages.Count}");
        }

        /// <summary>
        /// 更新页面的访问顺序（移到最前面，不加锁，内部使用）
        /// </summary>
        private void UpdateAccessOrderUnsafe(string cacheKey)
        {
            if (_accessNodes.TryGetValue(cacheKey, out var node))
            {
                _accessOrder.Remove(node);
                var newNode = _accessOrder.AddFirst(cacheKey);
                _accessNodes[cacheKey] = newNode;
            }
        }

        /// <summary>
        /// 移除最近最少使用的页面（不加锁，内部使用）
        /// </summary>
        private void RemoveLeastRecentlyUsedPageUnsafe()
        {
            if (_accessOrder.Last != null)
            {
                string lruKey = _accessOrder.Last.Value;
                RemovePageUnsafe(lruKey);
                System.Diagnostics.Debug.WriteLine($"[PageCacheManager] 缓存已满，自动移除最久未使用的页面: {lruKey}");
                
                // 触发页面移除事件（在锁外触发，避免死锁）
                // 注意：事件处理器不应该回调到 PageCacheManager 的公共方法
                PageAutoRemoved?.Invoke(this, lruKey);
            }
        }

        /// <summary>
        /// 页面被自动移除事件（LRU 策略）
        /// </summary>
        public event EventHandler<string>? PageAutoRemoved;

        /// <summary>
        /// 移除指定的缓存页面
        /// </summary>
        public bool RemovePage(string cacheKey)
        {
            lock (_cacheLock)
            {
                return RemovePageUnsafe(cacheKey);
            }
        }

        /// <summary>
        /// 移除指定的缓存页面（不加锁，内部使用）
        /// </summary>
        private bool RemovePageUnsafe(string cacheKey)
        {
            if (_cachedPages.TryGetValue(cacheKey, out Page? page))
            {
                // 如果是 WebBrowserPage，调用其清理方法
                if (page is Pages.WebApp.Browser.WebBrowserPage webBrowserPage)
                {
                    try
                    {
                        webBrowserPage.DisposeWebView();
                        System.Diagnostics.Debug.WriteLine($"[PageCacheManager] 已调用 WebBrowserPage.DisposeWebView: {cacheKey}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PageCacheManager] 调用 DisposeWebView 失败: {ex.Message}");
                    }
                }
                
                _cachedPages.Remove(cacheKey);
                
                // 从访问顺序中移除
                if (_accessNodes.TryGetValue(cacheKey, out var node))
                {
                    _accessOrder.Remove(node);
                    _accessNodes.Remove(cacheKey);
                }
                
                System.Diagnostics.Debug.WriteLine($"[PageCacheManager] 移除缓存页面: {cacheKey}");
                
                if (_currentPageKey == cacheKey)
                {
                    _currentPageKey = null;
                }
                
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cachedPages.Clear();
                _accessOrder.Clear();
                _accessNodes.Clear();
                _currentPageKey = null;
                System.Diagnostics.Debug.WriteLine("[PageCacheManager] 已清除所有缓存");
            }
        }

        /// <summary>
        /// 获取当前缓存的页面数量
        /// </summary>
        public int CachedPageCount
        {
            get
            {
                lock (_cacheLock)
                {
                    return _cachedPages.Count;
                }
            }
        }

        /// <summary>
        /// 获取所有缓存的页面键
        /// </summary>
        public IEnumerable<string> GetCachedPageKeys()
        {
            lock (_cacheLock)
            {
                return _cachedPages.Keys.ToArray();
            }
        }

        /// <summary>
        /// 获取按LRU顺序排列的缓存页面键（从最新到最旧）
        /// </summary>
        public IEnumerable<string> GetCachedPageKeysInLRUOrder()
        {
            lock (_cacheLock)
            {
                return _accessOrder.ToArray();
            }
        }

        /// <summary>
        /// 检查指定页面是否已缓存
        /// </summary>
        public bool IsPageCached(string cacheKey)
        {
            lock (_cacheLock)
            {
                bool cached = _cachedPages.ContainsKey(cacheKey);
                System.Diagnostics.Debug.WriteLine($"[PageCacheManager] IsPageCached({cacheKey}): {cached}");
                return cached;
            }
        }

        /// <summary>
        /// 获取缓存的页面实例（不更新访问顺序）
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <returns>页面实例，如果不存在则返回 null</returns>
        public Page? GetCachedPage(string cacheKey)
        {
            lock (_cacheLock)
            {
                _cachedPages.TryGetValue(cacheKey, out Page? page);
                return page;
            }
        }

        /// <summary>
        /// 创建页面实例（AOT 兼容：使用工厂模式而非反射）
        /// </summary>
        private Page CreatePageInstance(Type pageType)
        {
            if (!typeof(Page).IsAssignableFrom(pageType))
            {
                throw new ArgumentException($"Type {pageType.Name} is not a Page", nameof(pageType));
            }

            // AOT 兼容：使用预注册的工厂函数创建实例
            if (_pageFactories.TryGetValue(pageType, out var factory))
            {
                var page = factory();
                System.Diagnostics.Debug.WriteLine($"[PageCacheManager] 使用工厂创建页面: {pageType.Name}");
                return page;
            }

            // 降级方案：如果类型未注册，抛出异常（而非使用反射）
            throw new InvalidOperationException(
                $"页面类型 {pageType.Name} 未在 PageCacheManager 中注册。" +
                $"请在 _pageFactories 字典中添加该类型的工厂函数以支持 Native AOT 编译。");
        }
        
        /// <summary>
        /// 注册自定义页面工厂（用于扩展支持新页面类型）
        /// </summary>
        public static void RegisterPageFactory(Type pageType, Func<Page> factory)
        {
            if (!typeof(Page).IsAssignableFrom(pageType))
            {
                throw new ArgumentException($"Type {pageType.Name} is not a Page", nameof(pageType));
            }
            
            _pageFactories[pageType] = factory;
            System.Diagnostics.Debug.WriteLine($"[PageCacheManager] 注册页面工厂: {pageType.Name}");
        }
    }
}
