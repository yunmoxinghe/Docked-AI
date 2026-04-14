using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Docked_AI.Features.MainWindowContent.ContentArea
{
    /// <summary>
    /// 页面缓存管理器，用于缓存已创建的页面实例，实现快速切换
    /// 使用 LRU（最近最少使用）策略自动管理缓存
    /// </summary>
    public class PageCacheManager
    {
        private readonly Dictionary<string, Page> _cachedPages = new();
        private readonly LinkedList<string> _accessOrder = new(); // 记录访问顺序，最新的在前面
        private readonly Dictionary<string, LinkedListNode<string>> _accessNodes = new();
        private readonly int _maxCacheSize;
        private string? _currentPageKey;

        public PageCacheManager(int maxCacheSize = 20)
        {
            _maxCacheSize = maxCacheSize;
        }

        /// <summary>
        /// 获取或创建页面实例
        /// </summary>
        /// <param name="pageType">页面类型</param>
        /// <param name="parameter">导航参数</param>
        /// <param name="cacheKey">缓存键（如果为 null 则不缓存）</param>
        /// <returns>页面实例</returns>
        public Page GetOrCreatePage(Type pageType, object? parameter, string? cacheKey)
        {
            // 如果没有缓存键，直接创建新实例
            if (string.IsNullOrEmpty(cacheKey))
            {
                return CreatePageInstance(pageType);
            }

            // 检查缓存中是否已存在
            if (_cachedPages.TryGetValue(cacheKey, out Page? cachedPage))
            {
                System.Diagnostics.Debug.WriteLine($"[PageCacheManager] 使用缓存页面: {cacheKey}");
                _currentPageKey = cacheKey;
                
                // 更新访问顺序（移到最前面）
                UpdateAccessOrder(cacheKey);
                
                return cachedPage;
            }

            // 创建新实例并缓存
            var newPage = CreatePageInstance(pageType);
            AddPageToCache(cacheKey, newPage);
            
            return newPage;
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

            // 如果已存在，更新访问顺序
            if (_cachedPages.ContainsKey(cacheKey))
            {
                UpdateAccessOrder(cacheKey);
                return;
            }

            // 检查缓存大小限制
            if (_cachedPages.Count >= _maxCacheSize)
            {
                RemoveLeastRecentlyUsedPage();
            }

            _cachedPages[cacheKey] = page;
            _currentPageKey = cacheKey;
            
            // 添加到访问顺序列表（最前面）
            var node = _accessOrder.AddFirst(cacheKey);
            _accessNodes[cacheKey] = node;
            
            System.Diagnostics.Debug.WriteLine($"[PageCacheManager] 添加页面到缓存: {cacheKey}, 当前缓存数: {_cachedPages.Count}");
        }

        /// <summary>
        /// 更新页面的访问顺序（移到最前面）
        /// </summary>
        private void UpdateAccessOrder(string cacheKey)
        {
            if (_accessNodes.TryGetValue(cacheKey, out var node))
            {
                _accessOrder.Remove(node);
                var newNode = _accessOrder.AddFirst(cacheKey);
                _accessNodes[cacheKey] = newNode;
            }
        }

        /// <summary>
        /// 移除最近最少使用的页面
        /// </summary>
        private void RemoveLeastRecentlyUsedPage()
        {
            if (_accessOrder.Last != null)
            {
                string lruKey = _accessOrder.Last.Value;
                RemovePage(lruKey);
                System.Diagnostics.Debug.WriteLine($"[PageCacheManager] 缓存已满，自动移除最久未使用的页面: {lruKey}");
                
                // 触发页面移除事件
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
            if (_cachedPages.Remove(cacheKey))
            {
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
            _cachedPages.Clear();
            _accessOrder.Clear();
            _accessNodes.Clear();
            _currentPageKey = null;
            System.Diagnostics.Debug.WriteLine("[PageCacheManager] 已清除所有缓存");
        }

        /// <summary>
        /// 获取当前缓存的页面数量
        /// </summary>
        public int CachedPageCount => _cachedPages.Count;

        /// <summary>
        /// 获取所有缓存的页面键
        /// </summary>
        public IEnumerable<string> GetCachedPageKeys() => _cachedPages.Keys.ToArray();

        /// <summary>
        /// 获取按LRU顺序排列的缓存页面键（从最新到最旧）
        /// </summary>
        public IEnumerable<string> GetCachedPageKeysInLRUOrder() => _accessOrder.ToArray();

        /// <summary>
        /// 检查指定页面是否已缓存
        /// </summary>
        public bool IsPageCached(string cacheKey)
        {
            bool cached = _cachedPages.ContainsKey(cacheKey);
            System.Diagnostics.Debug.WriteLine($"[PageCacheManager] IsPageCached({cacheKey}): {cached}");
            return cached;
        }

        /// <summary>
        /// 获取缓存的页面实例（不更新访问顺序）
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <returns>页面实例，如果不存在则返回 null</returns>
        public Page? GetCachedPage(string cacheKey)
        {
            _cachedPages.TryGetValue(cacheKey, out Page? page);
            return page;
        }

        private Page CreatePageInstance(Type pageType)
        {
            if (!typeof(Page).IsAssignableFrom(pageType))
            {
                throw new ArgumentException($"Type {pageType.Name} is not a Page", nameof(pageType));
            }

            var instance = Activator.CreateInstance(pageType);
            if (instance is not Page page)
            {
                throw new InvalidOperationException($"Failed to create instance of {pageType.Name}");
            }

            return page;
        }
    }
}
