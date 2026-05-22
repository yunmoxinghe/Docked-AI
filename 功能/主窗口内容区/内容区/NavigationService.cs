using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Docked_AI.Features.MainWindowContent.ContentArea
{
    /// <summary>
    /// 导航服务：统一管理页面导航、缓存和生命周期
    /// 遵循 WinUI 3 最佳实践，支持 LRU 缓存和自定义导航动画
    /// </summary>
    public class NavigationService
    {
        private readonly Frame _frame;
        private readonly PageCacheManager _cacheManager;
        private Page? _currentPage;
        private Type? _currentPageType;
        private object? _currentPageParameter;

        /// <summary>
        /// 当前显示的页面类型
        /// </summary>
        public Type? CurrentPageType => _currentPageType;

        /// <summary>
        /// 当前显示的页面参数
        /// </summary>
        public object? CurrentPageParameter => _currentPageParameter;

        /// <summary>
        /// 当前显示的页面实例
        /// </summary>
        public Page? CurrentPage => _currentPage;

        /// <summary>
        /// 是否可以返回
        /// </summary>
        public bool CanGoBack => _frame.CanGoBack;

        /// <summary>
        /// BackStack 深度
        /// </summary>
        public int BackStackDepth => _frame.BackStackDepth;

        /// <summary>
        /// 导航完成事件（包括缓存导航）
        /// </summary>
        public event EventHandler<NavigationEventArgs>? Navigated;

        /// <summary>
        /// 缓存页面导航完成事件（缓存命中时 Frame 不触发 Navigated，由此事件补充通知）
        /// </summary>
        public event EventHandler<(Type PageType, object? Parameter)>? CachedPageNavigated;

        public NavigationService(Frame frame, PageCacheManager cacheManager)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));

            _frame.Navigated += OnFrameNavigated;
        }

        /// <summary>
        /// 导航到指定页面
        /// </summary>
        /// <param name="pageType">页面类型</param>
        /// <param name="parameter">导航参数</param>
        /// <param name="transitionInfo">导航动画（可选）</param>
        /// <returns>是否成功导航</returns>
        public bool Navigate(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type pageType,
            object? parameter = null,
            NavigationTransitionInfo? transitionInfo = null)
        {
            if (pageType == null)
            {
                throw new ArgumentNullException(nameof(pageType));
            }

            System.Diagnostics.Debug.WriteLine($"[NavigationService] Navigate 被调用: {pageType.Name}");

            // 生成缓存键
            string? cacheKey = GenerateCacheKey(pageType, parameter);
            System.Diagnostics.Debug.WriteLine($"[NavigationService] 缓存键: {cacheKey ?? "null"}");

            // 检查是否已缓存
            if (!string.IsNullOrEmpty(cacheKey) && _cacheManager.IsPageCached(cacheKey))
            {
                return NavigateToCachedPage(pageType, parameter, cacheKey);
            }
            else
            {
                return NavigateToNewPage(pageType, parameter, transitionInfo);
            }
        }

        /// <summary>
        /// 导航到缓存页面
        /// </summary>
        private bool NavigateToCachedPage(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type pageType,
            object? parameter,
            string cacheKey)
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationService] 页面已缓存，直接使用: {cacheKey}");

            // 调用当前页面的 OnNavigatedFrom
            if (_currentPage is INavigationAware currentNavigationAware)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] 调用当前页面的 INavigationAware.OnNavigatedFrom");
                currentNavigationAware.OnNavigatedFrom();
            }

            // 把当前页手动加入 BackStack，模拟 Frame.Navigate 的行为
            if (_frame.Content is Page currentPage && _currentPageType != null)
            {
                _frame.BackStack.Add(new PageStackEntry(_currentPageType, _currentPageParameter, null));
                System.Diagnostics.Debug.WriteLine($"[NavigationService] 当前页面已加入 BackStack: {_currentPageType.Name}");
            }

            // 从缓存获取页面
            Page cachedPage = _cacheManager.GetOrCreatePage(pageType, parameter, cacheKey);

            // 直接设置内容（跳过 Frame 导航）
            _frame.Content = cachedPage;
            _currentPage = cachedPage;
            _currentPageType = pageType;
            _currentPageParameter = parameter;

            System.Diagnostics.Debug.WriteLine($"[NavigationService] 已设置缓存页面到 Frame.Content，BackStack 深度: {_frame.BackStackDepth}");

            // 手动调用 OnNavigatedTo
            if (cachedPage is INavigationAware navigationAware)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] 调用 INavigationAware.OnNavigatedTo");
                navigationAware.OnNavigatedTo(parameter);
            }

            // 触发缓存导航事件
            CachedPageNavigated?.Invoke(this, (pageType, parameter));

            return true;
        }

        /// <summary>
        /// 导航到新页面（使用 Frame.Navigate）
        /// </summary>
        private bool NavigateToNewPage(Type pageType, object? parameter, NavigationTransitionInfo? transitionInfo)
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationService] 首次导航，使用 Frame.Navigate: {pageType.Name}");

            bool result;
            if (transitionInfo != null)
            {
                result = _frame.Navigate(pageType, parameter, transitionInfo);
            }
            else
            {
                result = _frame.Navigate(pageType, parameter);
            }

            if (result)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Frame.Navigate 成功");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Frame.Navigate 失败");
            }

            return result;
        }

        /// <summary>
        /// 返回上一页
        /// </summary>
        /// <returns>是否成功返回</returns>
        public bool GoBack()
        {
            if (!_frame.CanGoBack)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] 无法返回，BackStack 为空");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[NavigationService] GoBack 被调用，BackStack 深度: {_frame.BackStackDepth}");

            // 获取 BackStack 中的上一页信息
            var backEntry = _frame.BackStack[_frame.BackStack.Count - 1];
            Type pageType = backEntry.SourcePageType;
            object? parameter = backEntry.Parameter;

            // AOT 兼容性检查：确保类型有无参构造函数
            if (!typeof(Page).IsAssignableFrom(pageType))
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] 类型 {pageType.Name} 不是 Page，使用 Frame.GoBack()");
                _frame.GoBack();
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"[NavigationService] 返回到页面: {pageType.Name}");

            // 生成缓存键
            string? cacheKey = GenerateCacheKey(pageType, parameter);

            // 检查是否已缓存
            if (!string.IsNullOrEmpty(cacheKey) && _cacheManager.IsPageCached(cacheKey))
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] 使用缓存页面返回: {cacheKey}");

                // 移除 BackStack 中的最后一项（因为我们要手动导航）
                _frame.BackStack.RemoveAt(_frame.BackStack.Count - 1);

                // 调用当前页面的 OnNavigatedFrom
                if (_currentPage is INavigationAware currentNavigationAware)
                {
                    System.Diagnostics.Debug.WriteLine($"[NavigationService] 调用当前页面的 INavigationAware.OnNavigatedFrom");
                    currentNavigationAware.OnNavigatedFrom();
                }

                // 从缓存获取页面（使用已缓存的实例，不需要 AOT 标记）
                Page? cachedPage = _cacheManager.GetCachedPage(cacheKey);
                if (cachedPage == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[NavigationService] 缓存页面不存在，使用 Frame.GoBack()");
                    _frame.GoBack();
                    return true;
                }

                // 直接设置内容
                _frame.Content = cachedPage;
                _currentPage = cachedPage;
                _currentPageType = pageType;
                _currentPageParameter = parameter;

                System.Diagnostics.Debug.WriteLine($"[NavigationService] 已设置缓存页面到 Frame.Content，BackStack 深度: {_frame.BackStackDepth}");

                // 手动调用 OnNavigatedTo
                if (cachedPage is INavigationAware navigationAware)
                {
                    System.Diagnostics.Debug.WriteLine($"[NavigationService] 调用 INavigationAware.OnNavigatedTo");
                    navigationAware.OnNavigatedTo(parameter);
                }

                // 触发缓存导航事件
                CachedPageNavigated?.Invoke(this, (pageType, parameter));

                return true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] 页面未缓存，使用 Frame.GoBack");
                _frame.GoBack();
                return true;
            }
        }

        /// <summary>
        /// Frame 导航完成事件处理
        /// </summary>
        private void OnFrameNavigated(object sender, NavigationEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationService] Frame.Navigated 事件触发: {e.SourcePageType.Name}");

            // Frame 导航完成后，将页面加入缓存
            if (_frame.Content is Page page)
            {
                string? cacheKey = GenerateCacheKey(e.SourcePageType, e.Parameter);

                if (!string.IsNullOrEmpty(cacheKey))
                {
                    _cacheManager.AddPageToCache(cacheKey, page);
                    System.Diagnostics.Debug.WriteLine($"[NavigationService] 页面已缓存: {cacheKey}");
                }

                _currentPage = page;
                _currentPageType = e.SourcePageType;
                _currentPageParameter = e.Parameter;
            }

            // 触发导航事件
            Navigated?.Invoke(this, e);
        }

        /// <summary>
        /// 生成缓存键
        /// </summary>
        private string? GenerateCacheKey(Type pageType, object? parameter)
        {
            // WebBrowserPage 使用 shortcut.Id 作为缓存键
            if (pageType.Name == "WebBrowserPage" && parameter is Pages.WebApp.Shared.WebAppShortcut shortcut)
            {
                return $"WebBrowser_{shortcut.Id}";
            }

            // 其他页面不缓存（每次都创建新实例）
            return null;
        }

        /// <summary>
        /// 清除所有导航历史
        /// </summary>
        public void ClearBackStack()
        {
            _frame.BackStack.Clear();
            System.Diagnostics.Debug.WriteLine($"[NavigationService] BackStack 已清除");
        }

        /// <summary>
        /// 获取缓存管理器（用于外部访问）
        /// </summary>
        public PageCacheManager CacheManager => _cacheManager;
    }
}
