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
using Docked_AI.Features.Pages.Settings;
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
        private Type? _currentPageType;
        private object? _currentPageParameter;

        public event EventHandler<NavigationEventArgs>? Navigated;

        /// <summary>
        /// 缓存页面导航完成事件（缓存命中时 Frame 不触发 Navigated，由此事件补充通知）
        /// </summary>
        public event EventHandler<(Type PageType, object? Parameter)>? CachedPageNavigated;

        /// <summary>
        /// 当前显示的页面类型
        /// </summary>
        public Type? CurrentPageType => _currentPageType;

        /// <summary>
        /// 当前显示的页面参数
        /// </summary>
        public object? CurrentPageParameter => _currentPageParameter;

        /// <summary>
        /// 是否可以返回（Frame 内置 BackStack）
        /// </summary>
        public bool CanGoBack => ContentFrame.CanGoBack;

        /// <summary>
        /// 返回上一页（Frame 自动使用反向动画）
        /// </summary>
        public void GoBack()
        {
            if (ContentFrame.CanGoBack)
                ContentFrame.GoBack();
        }

        private const double TopBarHeight = 48.0;

        /// <summary>
        /// 获取覆盖层容器，用于添加通用控件和装饰
        /// </summary>
        public Grid OverlayContainer => OverlayLayer;

        /// <summary>
        /// 顶部应用栏容器
        /// </summary>
        public Grid TopAppBar => TopAppBarContainer;

        /// <summary>
        /// 顶部应用栏左侧面板
        /// </summary>
        public StackPanel TopBarLeft => TopBarLeftPanel;

        /// <summary>
        /// 顶部应用栏右侧面板
        /// </summary>
        public StackPanel TopBarRight => TopBarRightPanel;

        /// <summary>
        /// 顶部应用栏中间内容
        /// </summary>
        public ContentPresenter TopBarCenter => TopBarCenterContent;

        /// <summary>
        /// 显示或隐藏顶部应用栏（带淡入淡出动画）
        /// </summary>
        public bool IsTopBarVisible
        {
            get => TopAppBarContainer.Visibility == Visibility.Visible;
            set => SetTopBarVisibleAnimated(value);
        }

        private UIElement? _pageTitle;

        /// <summary>
        /// 注册页面大标题元素，滚动时由服务统一控制其淡入淡出
        /// </summary>
        public void SetPageTitle(UIElement? element)
        {
            _pageTitle = element;
        }

        /// <summary>
        /// 设置页面大标题的显示状态（带动画）
        /// </summary>
        public void SetPageTitleVisible(bool visible)
        {
            if (_pageTitle is null) return;

            var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = visible ? 0.0 : 1.0,
                To = visible ? 1.0 : 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(visible ? 200 : 150)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase
                {
                    EasingMode = visible
                        ? Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut
                        : Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn
                }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, _pageTitle);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "Opacity");
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            sb.Children.Add(anim);
            sb.Begin();
        }

        private void SetTopBarVisibleAnimated(bool visible)
        {
            var visual = ElementCompositionPreview.GetElementVisual(TopAppBarContainer);
            var compositor = visual.Compositor;

            if (visible)
            {
                TopAppBarContainer.Visibility = Visibility.Visible;
                var fadeIn = compositor.CreateScalarKeyFrameAnimation();
                fadeIn.InsertKeyFrame(0f, 0f);
                fadeIn.InsertKeyFrame(1f, 1f);
                fadeIn.Duration = TimeSpan.FromMilliseconds(200);
                visual.StartAnimation("Opacity", fadeIn);
            }
            else
            {
                var fadeOut = compositor.CreateScalarKeyFrameAnimation();
                fadeOut.InsertKeyFrame(0f, 1f);
                fadeOut.InsertKeyFrame(1f, 0f);
                fadeOut.Duration = TimeSpan.FromMilliseconds(150);

                var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                visual.StartAnimation("Opacity", fadeOut);
                batch.End();
                batch.Completed += (_, _) =>
                {
                    TopAppBarContainer.Visibility = Visibility.Collapsed;
                    visual.Opacity = 1f; // 重置，下次显示时从正确状态开始
                };
            }
        }

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

        #region 顶栏按钮事件处理

        /// <summary>
        /// 返回按钮点击事件
        /// </summary>
        public event EventHandler? BackButtonClicked;

        /// <summary>
        /// 菜单按钮点击事件
        /// </summary>
        public event EventHandler? MenuButtonClicked;

        /// <summary>
        /// 设置返回按钮的可见性
        /// </summary>
        public void SetBackButtonVisible(bool visible)
        {
            BackButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 设置菜单按钮的可见性
        /// </summary>
        public void SetMenuButtonVisible(bool visible)
        {
            MenuButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            MenuButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void BackButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(BackAnimatedIcon, "PointerOver");
        }

        private void BackButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(BackAnimatedIcon, "Normal");
        }

        private void MenuButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(MenuAnimatedIcon, "PointerOver");
        }

        private void MenuButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(MenuAnimatedIcon, "Normal");
        }

        private void MoreButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(MoreAnimatedIcon, "PointerOver");
        }

        private void MoreButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(MoreAnimatedIcon, "Normal");
        }

        #endregion

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

        public void Navigate(Type pageType, object? parameter = null, Microsoft.UI.Xaml.Media.Animation.NavigationTransitionInfo? transitionInfo = null)
        {
            System.Diagnostics.Debug.WriteLine($"[ContentArea] Navigate 被调用: {pageType.Name}");
            
            // 生成缓存键
            string? cacheKey = GenerateCacheKey(pageType, parameter);
            System.Diagnostics.Debug.WriteLine($"[ContentArea] 缓存键: {cacheKey ?? "null"}");
            
            // 为 AI 页面设置特殊的反向钻取动画
            NavigationTransitionInfo? customTransition = transitionInfo; // 外部传入优先
            if (customTransition == null && pageType.Name == "AIPage")
            {
                customTransition = new DrillInNavigationTransitionInfo();
            }
            
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
                
                // 把当前页手动加入 BackStack，模拟 Frame.Navigate 的行为
                if (ContentFrame.Content is Page currentPage && _currentPageType != null)
                {
                    ContentFrame.BackStack.Add(new PageStackEntry(_currentPageType, _currentPageParameter, null));
                }

                // 从缓存获取页面
                Page cachedPage = _pageCacheManager.GetOrCreatePage(pageType, parameter, cacheKey);
                
                // 直接设置内容（跳过 Frame 导航）
                ContentFrame.Content = cachedPage;
                _currentPage = cachedPage;
                _currentPageType = pageType;
                _currentPageParameter = parameter;
                
                System.Diagnostics.Debug.WriteLine($"[ContentArea] 已设置缓存页面到 Frame.Content，BackStack 深度: {ContentFrame.BackStackDepth}");
                
                // 手动调用 OnNavigatedTo
                if (cachedPage is INavigationAware navigationAware)
                {
                    System.Diagnostics.Debug.WriteLine($"[ContentArea] 调用 INavigationAware.OnNavigatedTo");
                    navigationAware.OnNavigatedTo(parameter);
                }

                // 手动触发 Navigated 事件，通知 Linker
                // NavigationEventArgs 不可直接构造，通过 ContentFrame_Navigated 的包装事件通知
                OnCachedPageNavigated(pageType, parameter);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ContentArea] 首次导航，使用 Frame.Navigate");
                
                // 首次导航，使用 Frame.Navigate 触发正常流程
                // 如果有自定义动画，使用自定义动画；否则使用 Frame 的默认 ContentTransitions
                if (customTransition != null)
                {
                    if (parameter != null)
                    {
                        ContentFrame.Navigate(pageType, parameter, customTransition);
                    }
                    else
                    {
                        ContentFrame.Navigate(pageType, null, customTransition);
                    }
                }
                else
                {
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
        }

        private void OnCachedPageNavigated(Type pageType, object? parameter)
        {
            CachedPageNavigated?.Invoke(this, (pageType, parameter));
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // Frame 导航完成后，将页面加入缓存
            if (ContentFrame.Content is Page page)
            {
                string? cacheKey = GenerateCacheKey(e.SourcePageType, e.Parameter);
                
                if (!string.IsNullOrEmpty(cacheKey))
                {
                    _pageCacheManager.AddPageToCache(cacheKey, page);
                    System.Diagnostics.Debug.WriteLine($"[ContentArea] 页面已缓存: {cacheKey}");
                }
                
                _currentPage = page;
                _currentPageType = e.SourcePageType;
                _currentPageParameter = e.Parameter;
                
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
