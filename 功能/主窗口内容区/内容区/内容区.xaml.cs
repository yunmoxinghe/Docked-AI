using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Numerics;
using Docked_AI.Features.Pages.WebApp.Shared;
using Docked_AI.Features.Pages.WebApp.Browser;
using Docked_AI.Features.Settings;
using Microsoft.UI.Xaml.Media.Animation;

namespace Docked_AI.Features.MainWindowContent.ContentArea
{
    public sealed partial class ContentArea : UserControl
    {
        private const float DefaultCornerRadius = 4f;
        private const float PinnedCornerRadius = 8f;
        private float _currentCornerRadius = DefaultCornerRadius;
        private CompositionRoundedRectangleGeometry? _clipGeometry;
        private CompositionRoundedRectangleGeometry? _gridClipGeometry;
        private readonly PageCacheManager _pageCacheManager;
        private Page? _currentPage;

        public event EventHandler<NavigationEventArgs>? Navigated;

        /// <summary>
        /// 获取覆盖层容器，用于添加通用控件和装饰
        /// </summary>
        public Grid OverlayContainer => OverlayLayer;

        public ContentArea()
        {
            InitializeComponent();
            _pageCacheManager = new PageCacheManager(maxCacheSize: 20);
            _pageCacheManager.PageAutoRemoved += OnPageAutoRemoved;
            ContentFrame.Navigated += ContentFrame_Navigated;
            ContentGrid.Loaded += ContentGrid_Loaded;
            
            // 初始化 Frame 动画
            UpdateFrameAnimation();
            
            // 订阅设置变化事件
            Pages.Settings.SettingsPage.FrameAnimationSettingsChanged += OnFrameAnimationSettingsChanged;
        }

        private void OnFrameAnimationSettingsChanged(object? sender, EventArgs e)
        {
            // 设置改变时更新 Frame 动画
            UpdateFrameAnimation();
        }

        private void UpdateFrameAnimation()
        {
            var animationType = ExperimentalSettings.FrameNavigationAnimation;
            var transitionInfo = GetNavigationTransitionInfo(animationType);
            
            // 更新 Frame 的 ContentTransitions
            var transition = new NavigationThemeTransition
            {
                DefaultNavigationTransitionInfo = transitionInfo
            };
            
            ContentFrame.ContentTransitions = new TransitionCollection { transition };
            
            System.Diagnostics.Debug.WriteLine($"[ContentArea] Frame 动画已更新为: {animationType}");
        }

        private NavigationTransitionInfo GetNavigationTransitionInfo(FrameAnimationType animationType)
        {
            return animationType switch
            {
                FrameAnimationType.None => new SuppressNavigationTransitionInfo(),
                FrameAnimationType.EntranceTransition => new EntranceNavigationTransitionInfo(),
                FrameAnimationType.SlideFromRight => new SlideNavigationTransitionInfo 
                { 
                    Effect = SlideNavigationTransitionEffect.FromRight 
                },
                FrameAnimationType.SlideFromLeft => new SlideNavigationTransitionInfo 
                { 
                    Effect = SlideNavigationTransitionEffect.FromLeft 
                },
                FrameAnimationType.SlideFromBottom => new SlideNavigationTransitionInfo 
                { 
                    Effect = SlideNavigationTransitionEffect.FromBottom 
                },
                FrameAnimationType.DrillIn => new DrillInNavigationTransitionInfo(),
                _ => new EntranceNavigationTransitionInfo()
            };
        }

        private void OnPageAutoRemoved(object? sender, string cacheKey)
        {
            System.Diagnostics.Debug.WriteLine($"[ContentArea] 页面被自动移除: {cacheKey}");
            
            // 从缓存键中提取 shortcutId
            if (cacheKey.StartsWith("WebBrowser_"))
            {
                string shortcutId = cacheKey.Substring("WebBrowser_".Length);
                
                // 获取页面实例进行清理
                var page = _pageCacheManager.GetCachedPage(cacheKey);
                if (page is WebBrowserPage webBrowserPage)
                {
                    // 触发页面清理逻辑（如果有的话）
                    System.Diagnostics.Debug.WriteLine($"[ContentArea] 清理被移除的 WebBrowserPage: {shortcutId}");
                }
                
                // 注销 WebView
                WebViewManager.UnregisterWebView(shortcutId);
                System.Diagnostics.Debug.WriteLine($"[ContentArea] 自动注销 WebView: {shortcutId}");
            }
        }

        private void ContentGrid_Loaded(object sender, RoutedEventArgs e)
        {
            // 为 ContentGrid 应用圆角裁切
            ApplyGridClip();
        }

        private void ApplyGridClip()
        {
            var visual = ElementCompositionPreview.GetElementVisual(ContentGrid);
            var compositor = visual.Compositor;
            
            _gridClipGeometry = compositor.CreateRoundedRectangleGeometry();
            _gridClipGeometry.CornerRadius = new Vector2(_currentCornerRadius, _currentCornerRadius);
            _gridClipGeometry.Offset = Vector2.Zero;
            _gridClipGeometry.Size = new Vector2((float)ContentGrid.ActualWidth, (float)ContentGrid.ActualHeight);
            
            visual.Clip = compositor.CreateGeometricClip(_gridClipGeometry);
            
            System.Diagnostics.Debug.WriteLine($"[ContentArea] Applied grid clip: Size={_gridClipGeometry.Size}, CornerRadius={_gridClipGeometry.CornerRadius}");
        }

        public void SetCornerRadius(bool isPinned)
        {
            _currentCornerRadius = isPinned ? PinnedCornerRadius : DefaultCornerRadius;
            ContentBorder.CornerRadius = new CornerRadius(_currentCornerRadius);
            
            // 更新 Frame 的裁切
            if (_clipGeometry != null)
            {
                _clipGeometry.CornerRadius = new Vector2(_currentCornerRadius, _currentCornerRadius);
            }
            
            // 更新 Grid 的裁切
            if (_gridClipGeometry != null)
            {
                _gridClipGeometry.CornerRadius = new Vector2(_currentCornerRadius, _currentCornerRadius);
            }
        }

        public void Navigate(Type pageType, object? parameter = null)
        {
            System.Diagnostics.Debug.WriteLine($"[ContentArea] Navigate 被调用: {pageType.Name}");
            
            // 生成缓存键
            string? cacheKey = GenerateCacheKey(pageType, parameter);
            System.Diagnostics.Debug.WriteLine($"[ContentArea] 缓存键: {cacheKey ?? "null"}");
            
            // 如果是 WebBrowserPage，检查 WebView 数量限制
            if (pageType == typeof(WebBrowserPage) && !string.IsNullOrEmpty(cacheKey))
            {
                string shortcutId = cacheKey.Substring("WebBrowser_".Length);
                
                // 如果该 WebView 已经注册，直接使用缓存
                if (WebViewManager.IsRegistered(shortcutId))
                {
                    System.Diagnostics.Debug.WriteLine($"[ContentArea] WebView 已注册，使用缓存");
                }
                // 如果未注册但已达到限制，需要先移除最久未使用的页面
                else if (!WebViewManager.CanCreateNew())
                {
                    System.Diagnostics.Debug.WriteLine($"[ContentArea] WebView 数量已达限制 ({WebViewManager.ActiveCount}/{WebViewManager.MaxCount})，触发 LRU 移除");
                    
                    // 获取按 LRU 顺序排列的缓存键（从最新到最旧）
                    var cachedKeysInOrder = _pageCacheManager.GetCachedPageKeysInLRUOrder().ToList();
                    
                    // 从后往前找到最久未使用的 WebBrowser 页面（不是当前要打开的）
                    string? oldestWebBrowserKey = null;
                    for (int i = cachedKeysInOrder.Count - 1; i >= 0; i--)
                    {
                        var key = cachedKeysInOrder[i];
                        if (key.StartsWith("WebBrowser_") && key != cacheKey)
                        {
                            oldestWebBrowserKey = key;
                            break;
                        }
                    }
                    
                    if (oldestWebBrowserKey != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ContentArea] 移除最久未使用的页面: {oldestWebBrowserKey}");
                        string oldShortcutId = oldestWebBrowserKey.Substring("WebBrowser_".Length);
                        
                        // 先注销 WebView
                        WebViewManager.UnregisterWebView(oldShortcutId);
                        
                        // 再移除页面缓存
                        _pageCacheManager.RemovePage(oldestWebBrowserKey);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ContentArea] 警告：未找到可移除的 WebBrowser 页面");
                    }
                }
            }
            
            // 检查是否已缓存
            if (!string.IsNullOrEmpty(cacheKey) && _pageCacheManager.IsPageCached(cacheKey))
            {
                System.Diagnostics.Debug.WriteLine($"[ContentArea] 页面已缓存，直接使用");
                
                // 从缓存获取页面
                Page cachedPage = _pageCacheManager.GetOrCreatePage(pageType, parameter, cacheKey);
                
                // 直接设置内容（跳过 Frame 导航）
                ContentFrame.Content = cachedPage;
                _currentPage = cachedPage;
                
                System.Diagnostics.Debug.WriteLine($"[ContentArea] 已设置缓存页面到 Frame.Content");
                
                // 手动调用 OnNavigatedTo
                if (cachedPage is INavigationAware navigationAware)
                {
                    System.Diagnostics.Debug.WriteLine($"[ContentArea] 调用 INavigationAware.OnNavigatedTo");
                    navigationAware.OnNavigatedTo(parameter);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ContentArea] 首次导航，使用 Frame.Navigate");
                
                // 首次导航，使用 Frame.Navigate 触发正常流程
                // Frame 的 ContentTransitions 已经设置好，不需要传递 transition 参数
                if (parameter != null)
                {
                    ContentFrame.Navigate(pageType, parameter);
                }
                else
                {
                    ContentFrame.Navigate(pageType);
                }
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // Frame 导航完成后，将页面加入缓存
            if (ContentFrame.Content is Page page)
            {
                string? cacheKey = GenerateCacheKey(e.SourcePageType, e.Parameter);
                
                if (!string.IsNullOrEmpty(cacheKey))
                {
                    // 将新创建的页面加入缓存
                    _pageCacheManager.AddPageToCache(cacheKey, page);
                    System.Diagnostics.Debug.WriteLine($"[ContentArea] 页面已缓存: {cacheKey}");
                }
                
                _currentPage = page;
                
                // 如果是 WebBrowserPage，订阅关闭事件
                if (page is WebBrowserPage webBrowserPage)
                {
                    webBrowserPage.PageCloseRequested += OnPageCloseRequested;
                }
            }
            
            Navigated?.Invoke(this, e);
        }

        private void OnPageCloseRequested(object? sender, string shortcutId)
        {
            System.Diagnostics.Debug.WriteLine($"[ContentArea] 收到页面关闭请求: {shortcutId}");
            
            // 触发关闭事件，通知 Linker
            PageCloseRequested?.Invoke(this, shortcutId);
        }

        // 页面关闭请求事件
        public event EventHandler<string>? PageCloseRequested;

        private string? GenerateCacheKey(Type pageType, object? parameter)
        {
            // WebBrowserPage 使用 shortcut.Id 作为缓存键
            if (pageType == typeof(WebBrowserPage) && parameter is WebAppShortcut shortcut)
            {
                return $"WebBrowser_{shortcut.Id}";
            }
            
            // 其他页面不缓存（每次都创建新实例）
            return null;
        }

        /// <summary>
        /// 移除指定的缓存页面
        /// </summary>
        public void RemoveCachedPage(string shortcutId)
        {
            string cacheKey = $"WebBrowser_{shortcutId}";
            
            // 获取页面实例以便清理（不更新访问顺序）
            var page = _pageCacheManager.GetCachedPage(cacheKey);
            if (page is WebBrowserPage webBrowserPage)
            {
                // 注销 WebView
                WebViewManager.UnregisterWebView(shortcutId);
                System.Diagnostics.Debug.WriteLine($"[ContentArea] 清理缓存页面，注销 WebView: {shortcutId}");
            }
            
            _pageCacheManager.RemovePage(cacheKey);
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public int GetCachedPageCount() => _pageCacheManager.CachedPageCount;

        /// <summary>
        /// 重启当前标签页（销毁并重建 WebView）
        /// </summary>
        public async System.Threading.Tasks.Task RestartCurrentTabAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ContentArea] RestartCurrentTabAsync 被调用");
            
            // 检查当前页面是否是 WebBrowserPage
            if (_currentPage is not WebBrowserPage currentWebBrowserPage)
            {
                System.Diagnostics.Debug.WriteLine("[ContentArea] 当前页面不是 WebBrowserPage，无法重启");
                return;
            }

            // 获取当前页面的参数（通过反射或缓存键）
            string? currentCacheKey = null;
            WebAppShortcut? currentShortcut = null;
            
            // 从缓存管理器中找到当前页面的缓存键
            foreach (var cacheKey in _pageCacheManager.GetCachedPageKeys())
            {
                var cachedPage = _pageCacheManager.GetCachedPage(cacheKey);
                if (ReferenceEquals(cachedPage, currentWebBrowserPage))
                {
                    currentCacheKey = cacheKey;
                    
                    // 从缓存键提取 shortcutId
                    if (cacheKey.StartsWith("WebBrowser_"))
                    {
                        string shortcutId = cacheKey.Substring("WebBrowser_".Length);
                        
                        // 从存储中加载 shortcut
                        var shortcuts = await WebAppShortcutStore.LoadAsync();
                        currentShortcut = shortcuts.FirstOrDefault(s => s.Id == shortcutId);
                    }
                    break;
                }
            }

            if (currentShortcut == null || currentCacheKey == null)
            {
                System.Diagnostics.Debug.WriteLine("[ContentArea] 无法找到当前标签的信息");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ContentArea] 准备重启标签: {currentShortcut.Name} ({currentShortcut.Id})");

            // Step 1: 移除旧的缓存页面
            _pageCacheManager.RemovePage(currentCacheKey);
            
            // Step 2: 注销 WebView
            WebViewManager.UnregisterWebView(currentShortcut.Id);
            
            System.Diagnostics.Debug.WriteLine("[ContentArea] 已清理旧实例");

            // Step 3: 显示加载状态（可选）
            // 这里可以添加一个 Loading UI
            
            // 给一点时间让旧实例完全释放
            await System.Threading.Tasks.Task.Delay(100);

            // Step 4: 重新导航到同一个页面（会创建新实例）
            System.Diagnostics.Debug.WriteLine("[ContentArea] 创建新实例");
            Navigate(typeof(WebBrowserPage), currentShortcut);
            
            System.Diagnostics.Debug.WriteLine("[ContentArea] 标签重启完成");
        }

        private void ContentFrame_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
            {
                return;
            }

            // 更新 Grid 的裁切大小
            if (_gridClipGeometry != null)
            {
                _gridClipGeometry.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
            }

            // 更新 Frame 的裁切
            var visual = ElementCompositionPreview.GetElementVisual(ContentFrame);
            if (_clipGeometry == null)
            {
                var compositor = visual.Compositor;
                _clipGeometry = compositor.CreateRoundedRectangleGeometry();
                _clipGeometry.CornerRadius = new Vector2(_currentCornerRadius, _currentCornerRadius);
                _clipGeometry.Offset = Vector2.Zero;
                visual.Clip = compositor.CreateGeometricClip(_clipGeometry);
            }

            _clipGeometry.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        }
    }
}
