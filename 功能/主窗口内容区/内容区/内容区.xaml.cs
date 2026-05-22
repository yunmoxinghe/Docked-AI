using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Docked_AI.Features.Pages.WebApp.Shared;
using Docked_AI.Features.Pages.WebApp.Browser;
using Docked_AI.Features.Pages.Settings;
using Docked_AI.Features.UnifiedCalls.TopAppBar;
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
        private readonly NavigationService _navigationService;

        public event EventHandler<NavigationEventArgs>? Navigated;

        /// <summary>
        /// 顶部应用栏中间空白区域双击事件
        /// </summary>
        public event EventHandler? TopBarDoubleTapped;

        /// <summary>
        /// 缓存页面导航完成事件（缓存命中时 Frame 不触发 Navigated，由此事件补充通知）
        /// </summary>
        public event EventHandler<(Type PageType, object? Parameter)>? CachedPageNavigated;

        /// <summary>
        /// 当前显示的页面类型
        /// </summary>
        public Type? CurrentPageType => _navigationService.CurrentPageType;

        /// <summary>
        /// 当前显示的页面参数
        /// </summary>
        public object? CurrentPageParameter => _navigationService.CurrentPageParameter;

        /// <summary>
        /// 是否可以返回（Frame 内置 BackStack）
        /// </summary>
        public bool CanGoBack => _navigationService.CanGoBack;

        /// <summary>
        /// 返回上一页（使用缓存机制）
        /// </summary>
        public void GoBack()
        {
            _navigationService.GoBack();
        }

        private const double TopBarHeight = 48.0;

        /// <summary>
        /// 获取覆盖层容器，用于添加通用控件和装饰
        /// </summary>
        public Grid OverlayContainer => OverlayLayer;

        /// <summary>
        /// 顶部应用栏独立控件
        /// </summary>
        public TopAppBarControl TopAppBar => TopAppBarHost;

        /// <summary>
        /// 顶部应用栏背景容器，保留给需要定制背景的页面使用
        /// </summary>
        public Grid TopAppBarBackground => TopAppBarHost.AppBarBackground;

        /// <summary>
        /// 顶部应用栏左侧面板
        /// </summary>
        public StackPanel TopBarLeft => TopAppBarHost.LeftContentPanel;

        /// <summary>
        /// 顶部应用栏右侧面板
        /// </summary>
        public StackPanel TopBarRight => TopAppBarHost.RightContentPanel;

        /// <summary>
        /// 顶部应用栏中间内容
        /// </summary>
        public ContentPresenter TopBarCenter => TopAppBarHost.CenterContent;

        /// <summary>
        /// 显示或隐藏顶部应用栏（带淡入淡出动画）
        /// </summary>
        public bool IsTopBarVisible
        {
            get => TopAppBarHost.IsAppBarVisible;
            set => TopAppBarHost.IsAppBarVisible = value;
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

        public ContentArea()
        {
            InitializeComponent();
            _pageCacheManager = new PageCacheManager(maxCacheSize: 20);
            _pageCacheManager.PageAutoRemoved += OnPageAutoRemoved;
            _navigationService = new NavigationService(ContentFrame, _pageCacheManager);
            _navigationService.Navigated += OnNavigationServiceNavigated;
            _navigationService.CachedPageNavigated += OnNavigationServiceCachedPageNavigated;
            ContentGrid.Loaded += ContentGrid_Loaded;
            TopAppBarHost.BackButtonClicked += TopAppBarHost_BackButtonClicked;
            TopAppBarHost.MenuButtonClicked += TopAppBarHost_MenuButtonClicked;
            
            // 初始化 Frame 动画
            UpdateFrameAnimation();
            
            // 订阅设置变化事件
            Pages.Settings.SettingsPage.FrameAnimationSettingsChanged += OnFrameAnimationSettingsChanged;
        }

        #region 顶栏按钮控制

        /// <summary>
        /// 菜单按钮点击事件
        /// </summary>
        public event EventHandler? MenuButtonClicked;

        /// <summary>
        /// 智能刷新返回按钮：根据 CanGoBack 自动显示/隐藏（带淡入淡出动画）。
        /// 返回按钮在独立的第四层，不依赖顶栏背景容器，顶栏显隐由页面自行控制。
        /// </summary>
        public void RefreshBackButton()
        {
            TopAppBarHost.SetBackButtonVisible(ContentFrame.CanGoBack);
        }

        /// <summary>
        /// 强制设置返回按钮可见性，用于页面自行管理顶部栏时覆盖默认行为。
        /// </summary>
        public void SetBackButtonVisible(bool visible)
        {
            TopAppBarHost.SetBackButtonVisible(visible);
        }

        /// <summary>
        /// 设置菜单按钮的可见性
        /// </summary>
        public void SetMenuButtonVisible(bool visible)
        {
            TopAppBarHost.SetMenuButtonVisible(visible);
        }

        /// <summary>
        /// 设置更多按钮的可见性
        /// </summary>
        public void SetMoreButtonVisible(bool visible)
        {
            TopAppBarHost.SetMoreButtonVisible(visible);
        }

        /// <summary>
        /// 获取更多按钮的菜单，用于动态添加菜单项
        /// </summary>
        public MenuFlyout? GetMoreMenu()
        {
            return TopAppBarHost.MoreMenu;
        }

        private void TopAppBarHost_BackButtonClicked(object? sender, EventArgs e)
        {
            // 优先让当前页面接管返回逻辑
            if (_navigationService.CurrentPage is IBackHandler handler && handler.OnBackRequested())
            {
                System.Diagnostics.Debug.WriteLine("[ContentArea] 返回被页面接管");
                return;
            }

            // 页面未接管，执行默认返回（使用缓存机制）
            System.Diagnostics.Debug.WriteLine("[ContentArea] 执行默认返回");
            GoBack();
        }

        private DateTime _lastTopBarTapTime = DateTime.MinValue;
        private const double TopBarDoubleTapMaxMs = 400;

        private void ContentGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(ContentGrid).Position;
            // 只响应顶部栏高度范围内（48px）
            if (pt.Y > TopBarHeight) return;
            // 排除左右按钮区域（各约 8px margin + 按钮宽度，粗略排除两端 56px）
            if (pt.X < 56 || pt.X > ContentGrid.ActualWidth - 56) return;

            var now = DateTime.Now;
            if ((now - _lastTopBarTapTime).TotalMilliseconds <= TopBarDoubleTapMaxMs)
            {
                TopBarDoubleTapped?.Invoke(this, EventArgs.Empty);
                _lastTopBarTapTime = DateTime.MinValue; // 重置，避免三击再触发
            }
            else
            {
                _lastTopBarTapTime = now;
            }
        }

        private void TopAppBarHost_MenuButtonClicked(object? sender, EventArgs e)
        {
            MenuButtonClicked?.Invoke(this, EventArgs.Empty);
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
            System.Diagnostics.Debug.WriteLine($"[ContentArea] 页面被 LRU 自动移除: {cacheKey}");
            
            // PageCacheManager 已经调用了 DisposeWebView，这里只需要记录日志
            // WebView 的 Unlink 由 WebBrowserPage.DisposeWebView 自动处理
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

        public void Navigate(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type pageType,
            object? parameter = null,
            Microsoft.UI.Xaml.Media.Animation.NavigationTransitionInfo? transitionInfo = null)
        {
            System.Diagnostics.Debug.WriteLine($"[ContentArea] Navigate 被调用: {pageType.Name}");
            
            // 为 AI 页面设置特殊的反向钻取动画
            NavigationTransitionInfo? customTransition = transitionInfo; // 外部传入优先
            if (customTransition == null && pageType.Name == "AIPage")
            {
                customTransition = new DrillInNavigationTransitionInfo();
            }
            
            // 使用导航服务进行导航
            _navigationService.Navigate(pageType, parameter, customTransition);
        }

        private void OnNavigationServiceNavigated(object? sender, NavigationEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[ContentArea] NavigationService.Navigated 事件触发: {e.SourcePageType.Name}");
            
            // 如果是 WebBrowserPage，订阅关闭事件
            if (ContentFrame.Content is WebBrowserPage webBrowserPage)
            {
                webBrowserPage.PageCloseRequested += OnPageCloseRequested;
            }

            // 智能刷新返回按钮
            RefreshBackButton();
            
            // 转发导航事件
            Navigated?.Invoke(this, e);
        }

        private void OnNavigationServiceCachedPageNavigated(object? sender, (Type PageType, object? Parameter) e)
        {
            System.Diagnostics.Debug.WriteLine($"[ContentArea] NavigationService.CachedPageNavigated 事件触发: {e.PageType.Name}");
            
            // 智能刷新返回按钮
            RefreshBackButton();
            
            // 转发缓存导航事件
            CachedPageNavigated?.Invoke(this, e);
        }

        private void OnPageCloseRequested(object? sender, string shortcutId)
        {
            System.Diagnostics.Debug.WriteLine($"[ContentArea] 收到页面关闭请求: {shortcutId}");
            
            // 触发关闭事件，通知 Linker
            PageCloseRequested?.Invoke(this, shortcutId);
        }

        // 页面关闭请求事件
        public event EventHandler<string>? PageCloseRequested;

        /// <summary>
        /// 移除指定的缓存页面
        /// </summary>
        public void RemoveCachedPage(string shortcutId)
        {
            string cacheKey = $"WebBrowser_{shortcutId}";
            
            // PageCacheManager.RemovePage 会自动调用 DisposeWebView
            // DisposeWebView 会自动调用 WebViewManager.Unlink
            _pageCacheManager.RemovePage(cacheKey);
            System.Diagnostics.Debug.WriteLine($"[ContentArea] 移除缓存页面: {shortcutId}");
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
            if (_navigationService.CurrentPage is not WebBrowserPage currentWebBrowserPage)
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

            // Step 1: 移除旧的缓存页面（会自动调用 DisposeWebView 和 Unlink）
            _pageCacheManager.RemovePage(currentCacheKey);
            System.Diagnostics.Debug.WriteLine("[ContentArea] 已清理旧实例");
            
            // 给一点时间让旧实例完全释放
            await System.Threading.Tasks.Task.Delay(100);

            // Step 2: 重新导航到同一个页面（会创建新实例）
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
