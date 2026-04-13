using Docked_AI.Features.Pages.WebApp.Shared;
using Docked_AI.Features.Settings;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using System;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.System;

namespace Docked_AI.Features.Pages.WebApp.Browser
{
    public sealed partial class WebBrowserPage : Page
    {
        private const string TintMessageType = "docked_ai_tint";
        private const string ThemeColorMessageType = "docked_ai_theme_color";
        private const double LuminanceThreshold = 0.179; // WCAG 标准阈值（归一化后）
        private const double MinOpacity = 0.01;
        private const double PercentageMax = 100.0;
        private const double ColorChannelMax = 255.0;
        private const int ColorTransitionDurationMs = 300; // 颜色过渡动画时长
        
        // 双击检测相关
        private DateTime _lastClickTime = DateTime.MinValue;
        private const int DoubleClickMaxDelayMs = 500; // 双击最大间隔时间（毫秒）

        private Uri? _pendingNavigationUri;
        private bool _isWebViewReady;
        private WebAppShortcut? _currentShortcut;
        private string? _contextMenuSelectedText;
        private string? _contextMenuLinkUrl;

        private readonly SolidColorBrush _topBarBackgroundBrush = new(Windows.UI.Color.FromArgb(1, 0, 0, 0)); // 几乎透明但能接收事件
        private readonly SolidColorBrush _bottomBarBackgroundBrush = new(Windows.UI.Color.FromArgb(1, 0, 0, 0)); // 几乎透明但能接收事件
        private readonly SolidColorBrush _topBarForegroundBrush = new();
        private readonly SolidColorBrush _bottomBarForegroundBrush = new();
        private readonly SolidColorBrush _topBarSecondaryForegroundBrush = new();
        private readonly SolidColorBrush _bottomBarDisabledForegroundBrush = new();
        private readonly SolidColorBrush _bottomBarHoverForegroundBrush = new();
        private bool _isDisposed;
        private bool _useRoundedWebView;
        private Microsoft.UI.Xaml.Controls.WebView2? _activeWebView;
        private bool _hasReceivedFirstTint;
        private bool _hasAppliedThemeColor;

        public WebBrowserPage()
        {
            InitializeComponent();

            // 根据设置决定使用哪个 WebView
            _useRoundedWebView = ExperimentalSettings.EnableRoundedWebView;
            UpdateWebViewVisibility();

            // 根据设置配置右键菜单（在 WebView 初始化之前）
            bool useWinUIContextMenu = ExperimentalSettings.EnableWinUIContextMenu;
            if (!useWinUIContextMenu)
            {
                // 如果不使用 WinUI 右键菜单，移除 ContextFlyout
                WebView.ContextFlyout = null;
                RoundedWebView.ContextFlyout = null;
            }

            // 初始化前景色为主题默认文本颜色
            InitializeForegroundColors();

            TopBarHost.Background = _topBarBackgroundBrush;
            TopBarExtension.Background = _topBarBackgroundBrush;
            BottomBarHost.Background = _bottomBarBackgroundBrush;
            BottomBarExtension.Background = _bottomBarBackgroundBrush;
            TitleText.Foreground = _topBarForegroundBrush;
            UrlText.Foreground = _topBarSecondaryForegroundBrush;
            SiteIconFallback.Foreground = _topBarSecondaryForegroundBrush;

            BackButton.Foreground = _bottomBarForegroundBrush;
            ForwardButton.Foreground = _bottomBarForegroundBrush;
            RefreshButton.Foreground = _bottomBarForegroundBrush;
            CopyUrlButton.Foreground = _bottomBarForegroundBrush;
            OpenExternalButton.Foreground = _bottomBarForegroundBrush;

            // 设置按钮的悬停和禁用状态颜色
            SetButtonStateColors(BackButton);
            SetButtonStateColors(ForwardButton);
            SetButtonStateColors(RefreshButton);
            SetButtonStateColors(CopyUrlButton);
            SetButtonStateColors(OpenExternalButton);

            // 设置自适应间距
            ApplyResponsiveSpacing();
            SizeChanged += (s, e) => ApplyResponsiveSpacing();
            BottomBarHost.SizeChanged += (s, e) => ApplyBottomBarResponsiveLayout();

            Loaded += WebBrowserPage_Loaded;
            Unloaded += WebBrowserPage_Unloaded;
            
            // 监听设置变化
            Pages.Settings.SettingsPage.RoundedWebViewSettingsChanged += OnRoundedWebViewSettingsChanged;
            Pages.Settings.SettingsPage.WinUIContextMenuSettingsChanged += OnWinUIContextMenuSettingsChanged;
            
            // 监听 Frame 的 SizeChanged 以同步圆角
            if (_useRoundedWebView)
            {
                this.SizeChanged += OnPageSizeChanged;
            }
            
            // 监听父容器变化以同步动态圆角
            this.SizeChanged += OnPageSizeChangedForCorners;
        }

        private void UpdateWebViewVisibility()
        {
            if (_useRoundedWebView)
            {
                WebView.Visibility = Visibility.Collapsed;
                RoundedWebViewContainer.Visibility = Visibility.Visible;
                _activeWebView = RoundedWebView;
            }
            else
            {
                WebView.Visibility = Visibility.Visible;
                RoundedWebViewContainer.Visibility = Visibility.Collapsed;
                _activeWebView = WebView;
            }
        }

        private void InitializeForegroundColors()
        {
            // 从主题资源获取默认文本颜色
            if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object? resource) 
                && resource is SolidColorBrush themeBrush)
            {
                _topBarForegroundBrush.Color = themeBrush.Color;
                _bottomBarForegroundBrush.Color = themeBrush.Color;
                System.Diagnostics.Debug.WriteLine($"[InitializeForegroundColors] 从主题获取: {themeBrush.Color}");
            }
            else
            {
                // 回退：根据当前主题选择黑色或白色
                var theme = Application.Current.RequestedTheme;
                var defaultColor = theme == ApplicationTheme.Dark ? Colors.White : Colors.Black;
                _topBarForegroundBrush.Color = defaultColor;
                _bottomBarForegroundBrush.Color = defaultColor;
                System.Diagnostics.Debug.WriteLine($"[InitializeForegroundColors] 使用默认: {defaultColor}, 主题: {theme}");
            }

            // 初始化次要前景色（用于URL和图标）
            if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out object? secondaryResource) 
                && secondaryResource is SolidColorBrush secondaryBrush)
            {
                _topBarSecondaryForegroundBrush.Color = secondaryBrush.Color;
            }
            else
            {
                // 回退：使用主色的70%透明度
                var baseColor = _topBarForegroundBrush.Color;
                _topBarSecondaryForegroundBrush.Color = Windows.UI.Color.FromArgb(
                    (byte)(baseColor.A * 0.7),
                    baseColor.R,
                    baseColor.G,
                    baseColor.B
                );
            }

            // 初始化禁用状态颜色
            if (Application.Current.Resources.TryGetValue("TextFillColorDisabledBrush", out object? disabledResource) 
                && disabledResource is SolidColorBrush disabledBrush)
            {
                _bottomBarDisabledForegroundBrush.Color = disabledBrush.Color;
                System.Diagnostics.Debug.WriteLine($"[InitializeForegroundColors] 禁用颜色从主题: {disabledBrush.Color}");
            }
            else
            {
                // 回退：使用主色的60%透明度（提高可见度）
                var baseColor = _bottomBarForegroundBrush.Color;
                _bottomBarDisabledForegroundBrush.Color = Windows.UI.Color.FromArgb(
                    (byte)(baseColor.A * 0.6),
                    baseColor.R,
                    baseColor.G,
                    baseColor.B
                );
                System.Diagnostics.Debug.WriteLine($"[InitializeForegroundColors] 禁用颜色计算: {_bottomBarDisabledForegroundBrush.Color}");
            }
            
            // 初始化悬停状态颜色（比正常状态稍亮）
            _bottomBarHoverForegroundBrush.Color = AdjustColorBrightness(_bottomBarForegroundBrush.Color, 0.15);
            System.Diagnostics.Debug.WriteLine($"[InitializeForegroundColors] 悬停颜色: {_bottomBarHoverForegroundBrush.Color}");
        }

        private void SetButtonStateColors(AppBarButton button)
        {
            // 设置按钮的悬停、按下和禁用状态颜色
            button.Resources["AppBarButtonForegroundPointerOver"] = _bottomBarHoverForegroundBrush;
            button.Resources["AppBarButtonForegroundPressed"] = _bottomBarForegroundBrush;
            button.Resources["AppBarButtonForegroundDisabled"] = _bottomBarDisabledForegroundBrush;
        }

        private void OnRoundedWebViewSettingsChanged(object? sender, EventArgs e)
        {
            // 设置改变时，需要重新加载页面才能生效
            // 这里只是更新标志，实际切换需要重新导航
            _useRoundedWebView = ExperimentalSettings.EnableRoundedWebView;
        }

        private void OnWinUIContextMenuSettingsChanged(object? sender, EventArgs e)
        {
            // 设置改变时，更新右键菜单配置
            bool useWinUIContextMenu = ExperimentalSettings.EnableWinUIContextMenu;
            
            // 更新两个 WebView 的配置
            if (WebView?.CoreWebView2 != null)
            {
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = !useWinUIContextMenu;
                UpdateContextMenuForWebView(WebView, useWinUIContextMenu);
            }
            
            if (RoundedWebView?.CoreWebView2 != null)
            {
                RoundedWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = !useWinUIContextMenu;
                UpdateContextMenuForWebView(RoundedWebView, useWinUIContextMenu);
            }
        }

        private void UpdateContextMenuConfiguration(bool useWinUIContextMenu)
        {
            // 配置当前激活的 WebView
            if (_activeWebView != null)
            {
                UpdateContextMenuForWebView(_activeWebView, useWinUIContextMenu);
            }
        }

        private void UpdateContextMenuForWebView(Microsoft.UI.Xaml.Controls.WebView2 webView, bool useWinUIContextMenu)
        {
            if (webView == null)
            {
                return;
            }

            // 如果 CoreWebView2 已初始化，配置事件订阅
            if (webView.CoreWebView2 != null)
            {
                // 先移除事件订阅（避免重复订阅）
                webView.CoreWebView2.ContextMenuRequested -= CoreWebView2_ContextMenuRequested;
                
                if (useWinUIContextMenu)
                {
                    // 启用 WinUI 右键菜单：订阅事件
                    webView.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
                }
            }
            
            // 配置 ContextFlyout
            if (useWinUIContextMenu)
            {
                // 恢复 ContextFlyout（如果之前被移除）
                if (webView.ContextFlyout == null)
                {
                    if (webView == WebView)
                    {
                        webView.ContextFlyout = WebViewContextMenu;
                    }
                    else if (webView == RoundedWebView)
                    {
                        webView.ContextFlyout = RoundedWebViewContextMenu;
                    }
                }
            }
            else
            {
                // 禁用 WinUI 右键菜单：移除 ContextFlyout
                webView.ContextFlyout = null;
            }
        }

        private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_useRoundedWebView)
            {
                return;
            }

            // 同步上层 Frame 的圆角到 Border
            SyncCornerRadiusFromParent();
        }

        private void SyncCornerRadiusFromParent()
        {
            // 尝试从父级 Frame 获取 CornerRadius
            DependencyObject? parent = this.Parent;
            CornerRadius cornerRadius = new CornerRadius(0);
            
            while (parent != null)
            {
                if (parent is Frame frame && frame.CornerRadius != new CornerRadius(0))
                {
                    cornerRadius = frame.CornerRadius;
                    break;
                }
                if (parent is Border border && border.CornerRadius != new CornerRadius(0))
                {
                    cornerRadius = border.CornerRadius;
                    break;
                }
                if (parent is Grid grid && grid.CornerRadius != new CornerRadius(0))
                {
                    cornerRadius = grid.CornerRadius;
                    break;
                }
                
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            // 如果没有找到圆角，使用默认值 12
            if (cornerRadius == new CornerRadius(0))
            {
                cornerRadius = new CornerRadius(12);
            }
            
            RoundedWebViewContainer.CornerRadius = cornerRadius;
        }

        private void OnPageSizeChangedForCorners(object sender, SizeChangedEventArgs e)
        {
            SyncDynamicCorners();
        }

        private void SyncDynamicCorners()
        {
            // 尝试从父级容器获取 CornerRadius
            DependencyObject? parent = this.Parent;
            CornerRadius cornerRadius = new CornerRadius(0);
            string foundIn = "default";
            
            while (parent != null)
            {
                if (parent is Frame frame && frame.CornerRadius != new CornerRadius(0))
                {
                    cornerRadius = frame.CornerRadius;
                    foundIn = $"Frame (CornerRadius={cornerRadius})";
                    break;
                }
                if (parent is Border border && border.CornerRadius != new CornerRadius(0))
                {
                    cornerRadius = border.CornerRadius;
                    foundIn = $"Border (CornerRadius={cornerRadius})";
                    break;
                }
                if (parent is Grid grid && grid.CornerRadius != new CornerRadius(0))
                {
                    cornerRadius = grid.CornerRadius;
                    foundIn = $"Grid (CornerRadius={cornerRadius})";
                    break;
                }
                
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            // 如果没有找到圆角，使用默认值 12
            if (cornerRadius == new CornerRadius(0))
            {
                cornerRadius = new CornerRadius(12);
            }
            
            // 确保最小圆角为 4
            const double minCornerRadius = 4.0;
            double topLeft = Math.Max(minCornerRadius, cornerRadius.TopLeft);
            double topRight = Math.Max(minCornerRadius, cornerRadius.TopRight);
            double bottomLeft = Math.Max(minCornerRadius, cornerRadius.BottomLeft);
            double bottomRight = Math.Max(minCornerRadius, cornerRadius.BottomRight);
            
            // 直接给顶部栏和底部栏设置圆角
            // 顶部栏：只有顶部圆角
            TopBarHost.CornerRadius = new CornerRadius(
                topLeft,
                topRight,
                0,
                0
            );
            
            // 底部栏：只有底部圆角
            BottomBarHost.CornerRadius = new CornerRadius(
                0,
                0,
                bottomRight,
                bottomLeft
            );
            
            // 调试输出
            System.Diagnostics.Debug.WriteLine($"[SyncDynamicCorners] Found in: {foundIn}");
            System.Diagnostics.Debug.WriteLine($"[SyncDynamicCorners] Original CornerRadius: {cornerRadius}");
            System.Diagnostics.Debug.WriteLine($"[SyncDynamicCorners] Applied (with min=4): TopLeft={topLeft}, TopRight={topRight}, BottomLeft={bottomLeft}, BottomRight={bottomRight}");
            System.Diagnostics.Debug.WriteLine($"[SyncDynamicCorners] TopBarHost.CornerRadius: {TopBarHost.CornerRadius}");
            System.Diagnostics.Debug.WriteLine($"[SyncDynamicCorners] BottomBarHost.CornerRadius: {BottomBarHost.CornerRadius}");
        }

        private void ApplyResponsiveSpacing()
        {
            // 根据窗口宽度计算自适应间距
            double width = ActualWidth;
            double horizontalPadding = Math.Max(8, Math.Min(16, width * 0.02));
            double verticalPadding = 8;
            double stackPanelMargin = Math.Max(8, Math.Min(16, width * 0.015));

            TopBarGrid.Padding = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding);
            TitleStackPanel.Margin = new Thickness(stackPanelMargin, 0, stackPanelMargin, 0);
        }

        private void ApplyBottomBarResponsiveLayout()
        {
            if (BottomBarHost.ActualWidth <= 0)
            {
                return;
            }

            // 获取所有按钮
            var buttons = new[]
            {
                BackButton,
                ForwardButton,
                RefreshButton,
                CopyUrlButton,
                OpenExternalButton
            };

            const int buttonCount = 5;
            const double minButtonWidth = 40.0;
            const double maxButtonWidth = 68.0;
            const double minSpacing = 2.0;
            const double maxSpacing = 16.0;

            double availableWidth = BottomBarHost.ActualWidth;

            // 计算最优按钮宽度和间距
            // 公式: availableWidth = (sidePadding * 2) + (buttonWidth * buttonCount) + (spacing * (buttonCount - 1))
            // 其中 sidePadding = spacing，保持一致

            double buttonWidth;
            double spacing;

            // 尝试使用最大按钮宽度
            double maxTotalButtonWidth = maxButtonWidth * buttonCount;
            // 两侧边距 + 按钮间距 = spacing * (buttonCount + 1)
            double remainingWidth = availableWidth - maxTotalButtonWidth;
            double calculatedSpacing = remainingWidth / (buttonCount + 1);

            if (calculatedSpacing >= minSpacing)
            {
                // 空间充足
                buttonWidth = maxButtonWidth;
                spacing = Math.Min(maxSpacing, calculatedSpacing);
            }
            else
            {
                // 空间不足，需要缩小按钮
                // 使用最小间距重新计算
                double totalSpacing = minSpacing * (buttonCount + 1);
                double widthForButtons = availableWidth - totalSpacing;
                buttonWidth = widthForButtons / buttonCount;

                if (buttonWidth >= minButtonWidth)
                {
                    // 按钮宽度在合理范围内
                    spacing = minSpacing;
                }
                else
                {
                    // 极端情况：使用最小按钮宽度
                    buttonWidth = minButtonWidth;
                    double totalButtonWidth = buttonWidth * buttonCount;
                    spacing = Math.Max(0, (availableWidth - totalButtonWidth) / (buttonCount + 1));
                }
            }

            // 应用计算结果
            foreach (var button in buttons)
            {
                button.Width = buttonWidth;
                button.MinWidth = minButtonWidth;
            }

            // 设置间距（通过 Margin 实现）
            for (int i = 0; i < buttons.Length; i++)
            {
                if (i == 0)
                {
                    // 第一个按钮：右侧有间距
                    buttons[i].Margin = new Thickness(0, 0, spacing, 0);
                }
                else if (i == buttons.Length - 1)
                {
                    // 最后一个按钮：无间距
                    buttons[i].Margin = new Thickness(0);
                }
                else
                {
                    // 中间按钮：右侧有间距
                    buttons[i].Margin = new Thickness(0, 0, spacing, 0);
                }
            }

            // 设置 StackPanel 的 Padding，两侧边距等于按钮间距
            BottomButtonsPanel.Padding = new Thickness(spacing, 0, spacing, 0);
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is not WebAppShortcut shortcut)
            {
                return;
            }

            if (!Uri.TryCreate(shortcut.Url, UriKind.Absolute, out Uri? uri))
            {
                return;
            }

            _currentShortcut = shortcut;
            TitleText.Text = string.IsNullOrWhiteSpace(shortcut.Name) ? uri.Host : shortcut.Name;
            _ = ShowShortcutIconAsync(shortcut.IconBytes);

            _pendingNavigationUri = uri;
            TryNavigatePendingUri();
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            DisposeWebView();
            base.OnNavigatedFrom(e);
        }

        private async void WebBrowserPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= WebBrowserPage_Loaded;
            await EnsureWebViewInitializedAsync();
            TryNavigatePendingUri();
            
            // 如果使用圆角 WebView，同步圆角
            if (_useRoundedWebView)
            {
                SyncCornerRadiusFromParent();
            }
            
            // 同步动态圆角
            SyncDynamicCorners();
            
            // 确保 TopBarHost 可以接收双击事件
            System.Diagnostics.Debug.WriteLine("========================================");
            System.Diagnostics.Debug.WriteLine("[WebBrowserPage_Loaded] 检查 TopBarHost 状态:");
            System.Diagnostics.Debug.WriteLine($"  Background: {TopBarHost.Background}");
            if (TopBarHost.Background is SolidColorBrush brush)
            {
                System.Diagnostics.Debug.WriteLine($"  Background Color: A={brush.Color.A}, R={brush.Color.R}, G={brush.Color.G}, B={brush.Color.B}");
            }
            System.Diagnostics.Debug.WriteLine($"  IsDoubleTapEnabled: {TopBarHost.IsDoubleTapEnabled}");
            System.Diagnostics.Debug.WriteLine($"  IsTapEnabled: {TopBarHost.IsTapEnabled}");
            System.Diagnostics.Debug.WriteLine("========================================");
            
            // 添加单击测试
            TopBarHost.Tapped += (s, args) =>
            {
                System.Diagnostics.Debug.WriteLine("[TopBarHost] 单击事件触发");
            };
        }

        private void WebBrowserPage_Unloaded(object sender, RoutedEventArgs e)
        {
            DisposeWebView();
        }

        private async Task EnsureWebViewInitializedAsync()
        {
            if (_isWebViewReady || _activeWebView == null)
            {
                return;
            }

            try
            {
                CoreWebView2EnvironmentOptions options = new()
                {
                    Language = GetWebViewLanguage(),
                    // 优化触摸板滚动体验的浏览器参数
                    AdditionalBrowserArguments = string.Join(" ", new[]
                    {
                        "--enable-features=msEdgeFluentOverlayScrollbar",
                        "--enable-smooth-scrolling",
                        "--enable-gpu-rasterization",
                        "--enable-zero-copy",
                        "--disable-features=msExperimentalScrolling"
                    })
                };
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    browserExecutableFolder: null,
                    userDataFolder: null,
                    options: options);
                await _activeWebView.EnsureCoreWebView2Async(environment);

                if (_activeWebView.CoreWebView2 is not null)
                {
                    _activeWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                    
                    // 优化触摸板和滚动体验
                    _activeWebView.CoreWebView2.Settings.IsSwipeNavigationEnabled = true;
                    
                    // 禁用触摸板缩放
                    _activeWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                    
                    // 禁用状态栏（悬停链接时左下角不显示 URL）
                    _activeWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                    
                    // 根据设置决定是否禁用默认右键菜单
                    bool useWinUIContextMenu = ExperimentalSettings.EnableWinUIContextMenu;
                    _activeWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = !useWinUIContextMenu;
                    
                    _activeWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                    _activeWebView.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
                    _activeWebView.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
                    _activeWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                    _activeWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                    
                    // 根据设置配置右键菜单
                    UpdateContextMenuConfiguration(useWinUIContextMenu);
                    
                    await EnsureTintScriptInstalledAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView initialization failed: {ex.Message}");
                // Fall back to default initialization behavior.
            }
            finally
            {
                _isWebViewReady = true;
            }
        }

        private void TryNavigatePendingUri()
        {
            if (!_isWebViewReady || _pendingNavigationUri is null || _activeWebView == null)
            {
                return;
            }

            _activeWebView.Source = _pendingNavigationUri;
            UrlText.Text = _pendingNavigationUri.AbsoluteUri;
            _pendingNavigationUri = null;
        }

        private async Task EnsureTintScriptInstalledAsync()
        {
            if (_activeWebView?.CoreWebView2 is null)
            {
                return;
            }

            // 增强版取色脚本：递归向上查找、支持渐变、图片背景等复杂场景
            string script = @"
(() => {
  if (window.__dockedAiTint) return;
  const state = { 
    lastTop: null, 
    lastBottom: null, 
    scheduled: false,
    scrollDebounceTimer: null 
  };
  
  function cssToRgbaArray(css) {
    if (!css) return null;
    const m = css.match(/rgba?\(([^)]+)\)/i);
    if (!m) return null;
    const parts = m[1].split(',').map(p => p.trim());
    if (parts.length < 3) return null;
    const r = parseFloat(parts[0]);
    const g = parseFloat(parts[1]);
    const b = parseFloat(parts[2]);
    const a = parts.length >= 4 ? parseFloat(parts[3]) : 1;
    if (![r,g,b,a].every(n => Number.isFinite(n))) return null;
    return [r, g, b, a];
  }
  
  // 增强版：递归向上查找有效背景色
  function effectiveBg(el) {
    if (!el) return null;
    let cur = el;
    const minAlpha = 0.01;
    const maxDepth = 20; // 防止无限循环
    let depth = 0;
    
    while (cur && cur !== document && depth < maxDepth) {
      const style = getComputedStyle(cur);
      const bg = cssToRgbaArray(style.backgroundColor);
      
      // 找到不透明的背景色
      if (bg && bg[3] > minAlpha) {
        return bg;
      }
      
      // 检查是否有渐变背景（取渐变起始色）
      const bgImage = style.backgroundImage;
      if (bgImage && bgImage !== 'none') {
        const gradientMatch = bgImage.match(/rgba?\([^)]+\)/i);
        if (gradientMatch) {
          const gradientColor = cssToRgbaArray(gradientMatch[0]);
          if (gradientColor && gradientColor[3] > minAlpha) {
            return gradientColor;
          }
        }
      }
      
      cur = cur.parentElement;
      depth++;
    }
    
    // 回退到 body
    if (document.body) {
      const bodyBg = cssToRgbaArray(getComputedStyle(document.body).backgroundColor);
      if (bodyBg && bodyBg[3] > minAlpha) return bodyBg;
    }
    
    // 回退到 html
    if (document.documentElement) {
      const htmlBg = cssToRgbaArray(getComputedStyle(document.documentElement).backgroundColor);
      if (htmlBg && htmlBg[3] > minAlpha) return htmlBg;
    }
    
    // 最终回退：返回 null 表示透明，让宿主决定
    return null;
  }
  
  function sampleAtY(y) {
    const minX = 1;
    const x = Math.max(minX, Math.floor(window.innerWidth / 2));
    const el = document.elementFromPoint(x, y);
    return effectiveBg(el);
  }
  
  function rgbaToCss(rgba) {
    if (!rgba) return null;
    const minAlpha = 0;
    const maxAlpha = 1;
    const a = Math.max(minAlpha, Math.min(maxAlpha, rgba[3]));
    return `rgba(${Math.round(rgba[0])},${Math.round(rgba[1])},${Math.round(rgba[2])},${a})`;
  }
  
  function post(topCss, bottomCss) {
    const msg = { 
      type: 'docked_ai_tint', 
      top: topCss, 
      bottom: bottomCss, 
      title: (document.title || ''),
      isTransparent: !topCss || !bottomCss
    };
    try {
      window.chrome?.webview?.postMessage(JSON.stringify(msg));
    } catch (error) {
      console.warn('Failed to post tint message to host.', error);
    }
  }
  
  function sendNow() {
    state.scheduled = false;
    const minY = 1;
    const topColor = sampleAtY(minY);
    
    // 滚动时只采样顶部，底部保持不变（大多数页面底部栏固定）
    const bottomColor = sampleAtY(Math.max(minY, window.innerHeight - 2));
    
    const top = rgbaToCss(topColor);
    const bottom = rgbaToCss(bottomColor);
    
    if (top === state.lastTop && bottom === state.lastBottom) return;
    state.lastTop = top;
    state.lastBottom = bottom;
    post(top, bottom);
  }
  
  function schedule() {
    if (state.scheduled) return;
    state.scheduled = true;
    requestAnimationFrame(sendNow);
  }
  
  // 滚动时使用防抖，避免频繁采样
  function scheduleWithDebounce() {
    if (state.scrollDebounceTimer) {
      clearTimeout(state.scrollDebounceTimer);
    }
    state.scrollDebounceTimer = setTimeout(() => {
      schedule();
      state.scrollDebounceTimer = null;
    }, 300); // 300ms 防抖
  }
  
  window.__dockedAiTint = { updateNow: schedule };
  
  // 滚动使用防抖版本
  window.addEventListener('scroll', scheduleWithDebounce, { passive: true });
  
  // 其他事件立即触发
  window.addEventListener('resize', schedule);
  document.addEventListener('readystatechange', schedule);
  document.addEventListener('DOMContentLoaded', schedule);
  window.addEventListener('load', schedule);
  
  schedule();
})();";

            await _activeWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            LoadingProgressBar.Visibility = Visibility.Visible;
            // 重置取色状态，准备接收新页面的颜色
            _hasReceivedFirstTint = false;
            _hasAppliedThemeColor = false;
        }

        private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingProgressBar.Visibility = Visibility.Collapsed;
            UpdateNavigationButtons();
            UpdateUrlText();
            
            // 分层取色策略：优先使用 theme-color
            await TryApplyThemeColorAsync();
        }

        private void CoreWebView2_HistoryChanged(object? sender, object e)
        {
            UpdateNavigationButtons();
        }

        private static string GetWebViewLanguage()
        {
            return CultureInfo.CurrentUICulture.Name;
        }

        private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
        {
            if (_activeWebView?.CoreWebView2 is null)
            {
                return;
            }

            string title = _activeWebView.CoreWebView2.DocumentTitle ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                if (_currentShortcut is not null && !string.IsNullOrWhiteSpace(_currentShortcut.Name))
                {
                    TitleText.Text = _currentShortcut.Name;
                }

                return;
            }

            TitleText.Text = title;
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string json = e.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                if (!root.TryGetProperty("type", out JsonElement typeEl))
                {
                    return;
                }

                string messageType = typeEl.GetString() ?? string.Empty;

                // 处理 theme-color 消息（优先级最高）
                if (string.Equals(messageType, ThemeColorMessageType, StringComparison.Ordinal))
                {
                    if (root.TryGetProperty("color", out JsonElement colorEl) &&
                        TryParseCssColor(colorEl.GetString(), out var themeColor))
                    {
                        _hasAppliedThemeColor = true;
                        ApplyBarTint(isTop: true, themeColor);
                        ApplyBarTint(isTop: false, themeColor);
                    }
                    return;
                }

                // 处理采样颜色消息
                if (string.Equals(messageType, TintMessageType, StringComparison.Ordinal))
                {
                    // 如果已经应用了 theme-color，跳过采样颜色
                    if (_hasAppliedThemeColor)
                    {
                        return;
                    }

                    bool isTransparent = root.TryGetProperty("isTransparent", out JsonElement transparentEl) && 
                                        transparentEl.GetBoolean();

                    // 如果页面完全透明，尝试截图采样
                    if (isTransparent)
                    {
                        await TryScreenshotSamplingAsync();
                        return;
                    }

                    if (root.TryGetProperty("top", out JsonElement topEl) &&
                        TryParseCssColor(topEl.GetString(), out var topColor))
                    {
                        ApplyBarTint(isTop: true, topColor);
                    }

                    if (root.TryGetProperty("bottom", out JsonElement bottomEl) &&
                        TryParseCssColor(bottomEl.GetString(), out var bottomColor))
                    {
                        ApplyBarTint(isTop: false, bottomColor);
                    }
                }
            }
            catch
            {
                // Ignore malformed messages.
            }
        }

        private void ApplyBarTint(bool isTop, Windows.UI.Color sampledColor)
        {
            // 确保Alpha值至少为1，以便接收指针事件
            var tinted = Windows.UI.Color.FromArgb(byte.MaxValue, sampledColor.R, sampledColor.G, sampledColor.B);
            SolidColorBrush background = isTop ? _topBarBackgroundBrush : _bottomBarBackgroundBrush;
            SolidColorBrush foreground = isTop ? _topBarForegroundBrush : _bottomBarForegroundBrush;

            System.Diagnostics.Debug.WriteLine($"[ApplyBarTint] isTop={isTop}, 背景色={tinted}, 采样色={sampledColor}");

            // 改进的防闪烁逻辑：
            // 只在首次接收且颜色与当前背景相同（仍是初始透明状态）时过滤纯白
            // 这样可以避免白色主题网站的颜色跳变
            if (!_hasReceivedFirstTint)
            {
                // 检查是否为初始状态（Alpha=1的黑色）
                bool isCurrentlyInitial = background.Color.A <= 1 && 
                    background.Color.R == 0 && background.Color.G == 0 && background.Color.B == 0;
                
                bool isPureWhite = sampledColor.R == 255 && sampledColor.G == 255 && sampledColor.B == 255;
                
                // 只有在当前是初始状态且采样到纯白时才过滤
                if (isCurrentlyInitial && isPureWhite)
                {
                    // 首次加载时忽略纯白色，保持初始状态，等待真实内容加载
                    System.Diagnostics.Debug.WriteLine("[ApplyBarTint] 首次加载忽略纯白色");
                    return;
                }
                
                // 标记已接收到第一次有效颜色
                _hasReceivedFirstTint = true;
            }

            // 使用动画平滑过渡背景色
            AnimateColorChange(background, tinted);
            
            var contrastColor = GetContrastingForeground(sampledColor);
            
            System.Diagnostics.Debug.WriteLine($"[ApplyBarTint] 对比色={contrastColor}");
            
            // 使用动画平滑过渡前景色
            AnimateColorChange(foreground, contrastColor);

            // 更新次要前景色（用于URL和图标）
            if (isTop)
            {
                var secondaryColor = Windows.UI.Color.FromArgb(
                    (byte)(contrastColor.A * 0.7),
                    contrastColor.R,
                    contrastColor.G,
                    contrastColor.B
                );
                AnimateColorChange(_topBarSecondaryForegroundBrush, secondaryColor);
            }
            else
            {
                // 更新底部栏的悬停状态颜色
                // 根据背景亮度决定是变亮还是变暗
                double luminance = CalculateLuminance(sampledColor);
                double adjustFactor = luminance < LuminanceThreshold ? 0.2 : -0.2; // 暗背景变亮，亮背景变暗
                var hoverColor = AdjustColorBrightness(contrastColor, adjustFactor);
                
                System.Diagnostics.Debug.WriteLine($"[ApplyBarTint] 底部栏 - 背景亮度={luminance:F3}, 对比色={contrastColor}, 悬停色={hoverColor}");
                AnimateColorChange(_bottomBarHoverForegroundBrush, hoverColor);
                
                // 更新底部栏的禁用状态颜色
                // 使用更高的不透明度以确保在白色背景上可见
                var disabledColor = Windows.UI.Color.FromArgb(
                    (byte)(contrastColor.A * 0.6),  // 提高到 60% 不透明度
                    contrastColor.R,
                    contrastColor.G,
                    contrastColor.B
                );
                System.Diagnostics.Debug.WriteLine($"[ApplyBarTint] 底部栏 - 禁用颜色={disabledColor}");
                AnimateColorChange(_bottomBarDisabledForegroundBrush, disabledColor);
                
                // 强制更新按钮的Resources和Foreground（确保立即生效）
                UpdateButtonResources();
            }
        }

        /// <summary>
        /// 强制更新所有按钮的Resources和Foreground
        /// </summary>
        private void UpdateButtonResources()
        {
            var buttons = new[] { BackButton, ForwardButton, RefreshButton, CopyUrlButton, OpenExternalButton };
            var icons = new[] { BackIcon, ForwardIcon, RefreshIcon, CopyIcon, OpenExternalIcon };
            
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                var icon = icons[i];
                
                if (button != null)
                {
                    // 更新Resources
                    button.Resources["AppBarButtonForegroundPointerOver"] = _bottomBarHoverForegroundBrush;
                    button.Resources["AppBarButtonForegroundPressed"] = _bottomBarForegroundBrush;
                    button.Resources["AppBarButtonForegroundDisabled"] = _bottomBarDisabledForegroundBrush;
                    
                    // 直接更新Foreground以确保立即生效
                    button.Foreground = _bottomBarForegroundBrush;
                    
                    // 同时更新Icon的Foreground
                    if (icon != null)
                    {
                        icon.Foreground = _bottomBarForegroundBrush;
                    }
                    
                    // 强制刷新视觉状态
                    VisualStateManager.GoToState(button, "Normal", false);
                }
            }
            System.Diagnostics.Debug.WriteLine($"[UpdateButtonResources] 按钮已更新 - 正常色={_bottomBarForegroundBrush.Color}, 悬停色={_bottomBarHoverForegroundBrush.Color}, 禁用色={_bottomBarDisabledForegroundBrush.Color}");
        }

        /// <summary>
        /// 计算颜色的相对亮度
        /// </summary>
        private static double CalculateLuminance(Windows.UI.Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        /// <summary>
        /// 调整颜色亮度
        /// </summary>
        /// <param name="color">原始颜色</param>
        /// <param name="factor">调整因子，正数变亮，负数变暗</param>
        private static Windows.UI.Color AdjustColorBrightness(Windows.UI.Color color, double factor)
        {
            if (factor > 0)
            {
                // 变亮：向白色混合
                return Windows.UI.Color.FromArgb(
                    color.A,
                    (byte)Math.Min(255, color.R + (255 - color.R) * factor),
                    (byte)Math.Min(255, color.G + (255 - color.G) * factor),
                    (byte)Math.Min(255, color.B + (255 - color.B) * factor)
                );
            }
            else
            {
                // 变暗：向黑色混合
                factor = -factor;
                return Windows.UI.Color.FromArgb(
                    color.A,
                    (byte)Math.Max(0, color.R * (1 - factor)),
                    (byte)Math.Max(0, color.G * (1 - factor)),
                    (byte)Math.Max(0, color.B * (1 - factor))
                );
            }
        }

        /// <summary>
        /// 使用动画平滑过渡颜色
        /// </summary>
        private void AnimateColorChange(SolidColorBrush brush, Windows.UI.Color targetColor)
        {
            if (brush.Color == targetColor)
            {
                return; // 颜色相同，无需动画
            }

            var animation = new ColorAnimation
            {
                To = targetColor,
                Duration = new Duration(TimeSpan.FromMilliseconds(ColorTransitionDurationMs)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, brush);
            Storyboard.SetTargetProperty(animation, "Color");
            
            storyboard.Begin();
        }

        private static Windows.UI.Color GetContrastingForeground(Windows.UI.Color background)
        {
            // WCAG 标准相对亮度公式：先归一化到 [0, 1]
            double r = background.R / 255.0;
            double g = background.G / 255.0;
            double b = background.B / 255.0;
            
            // 相对亮度计算（sRGB）
            double luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            
            // 使用 WCAG 标准阈值 0.179
            return luminance < LuminanceThreshold ? Colors.White : Colors.Black;
        }

        private static bool TryParseCssColor(string? cssColor, out Windows.UI.Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(cssColor))
            {
                return false;
            }

            string s = cssColor.Trim();
            if (s.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
            {
                int start = s.IndexOf('(');
                int end = s.IndexOf(')');
                if (start < 0 || end <= start)
                {
                    return false;
                }

                string inner = s.Substring(start + 1, end - start - 1);
                string[] parts = inner.Split(',');
                if (parts.Length < 3)
                {
                    return false;
                }

                if (!TryParseByte(parts[0], out byte r) ||
                    !TryParseByte(parts[1], out byte g) ||
                    !TryParseByte(parts[2], out byte b))
                {
                    return false;
                }

                color = Windows.UI.Color.FromArgb(byte.MaxValue, r, g, b);
                return true;
            }

            if (s.StartsWith('#'))
            {
                string hex = s.Substring(1);
                const int hexColorLength = 6;
                const int hexByteLength = 2;
                if (hex.Length == hexColorLength &&
                    byte.TryParse(hex.Substring(0, hexByteLength), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                    byte.TryParse(hex.Substring(hexByteLength, hexByteLength), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                    byte.TryParse(hex.Substring(hexByteLength * 2, hexByteLength), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    color = Windows.UI.Color.FromArgb(byte.MaxValue, r, g, b);
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseByte(string part, out byte value)
        {
            value = 0;
            string trimmed = part.Trim();
            if (trimmed.EndsWith("%", StringComparison.Ordinal))
            {
                if (!double.TryParse(trimmed.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
                {
                    return false;
                }

                percent = Math.Max(0, Math.Min(PercentageMax, percent));
                value = (byte)Math.Round(percent / PercentageMax * ColorChannelMax);
                return true;
            }

            if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double raw))
            {
                return false;
            }

            raw = Math.Max(0, Math.Min(ColorChannelMax, raw));
            value = (byte)Math.Round(raw);
            return true;
        }

        private void UpdateNavigationButtons()
        {
            if (_activeWebView == null) return;
            BackButton.IsEnabled = _activeWebView.CanGoBack;
            ForwardButton.IsEnabled = _activeWebView.CanGoForward;
        }

        private void UpdateUrlText()
        {
            Uri? uri = _activeWebView?.Source;
            UrlText.Text = uri?.AbsoluteUri ?? string.Empty;
        }

        private async Task ShowShortcutIconAsync(byte[]? iconBytes)
        {
            if (iconBytes is not { Length: > 0 })
            {
                ShowFallbackIcon();
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(iconBytes.AsBuffer());
                stream.Seek(0);
                await bitmap.SetSourceAsync(stream);

                SiteIconImage.Source = bitmap;
                SiteIconImage.Visibility = Visibility.Visible;
                SiteIconFallback.Visibility = Visibility.Collapsed;
            }
            catch
            {
                ShowFallbackIcon();
            }
        }

        private void ShowFallbackIcon()
        {
            SiteIconImage.Source = null;
            SiteIconImage.Visibility = Visibility.Collapsed;
            SiteIconFallback.Visibility = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_activeWebView != null && _activeWebView.CanGoBack)
            {
                _activeWebView.GoBack();
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_activeWebView != null && _activeWebView.CanGoForward)
            {
                _activeWebView.GoForward();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _activeWebView?.Reload();
        }

        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            Uri? uri = _activeWebView?.Source;
            if (uri is null)
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(uri.AbsoluteUri);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }

        private async void OpenExternalButton_Click(object sender, RoutedEventArgs e)
        {
            Uri? uri = _activeWebView?.Source;
            if (uri is null)
            {
                return;
            }

            await Launcher.LaunchUriAsync(uri);
        }

        // ==================== 右键菜单相关方法 ====================

        private void CoreWebView2_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            // 清除默认菜单项
            e.MenuItems.Clear();
            
            // 获取链接地址
            _contextMenuLinkUrl = e.ContextMenuTarget.LinkUri;
            
            // 获取选中的文本
            _contextMenuSelectedText = e.ContextMenuTarget.SelectionText;
            
            // 根据当前使用的 WebView 更新对应的菜单项
            if (_useRoundedWebView)
            {
                CopyMenuItem2.IsEnabled = true;
                CopyLinkMenuItem2.IsEnabled = !string.IsNullOrEmpty(_contextMenuLinkUrl);
            }
            else
            {
                CopyMenuItem.IsEnabled = true;
                CopyLinkMenuItem.IsEnabled = !string.IsNullOrEmpty(_contextMenuLinkUrl);
            }
            
            // 显示自定义菜单
            var flyout = _activeWebView?.ContextFlyout as MenuFlyout;
            if (flyout != null)
            {
                flyout.ShowAt(_activeWebView, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
                {
                    Position = new Windows.Foundation.Point(e.Location.X, e.Location.Y)
                });
            }
        }

        private void BackMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_activeWebView != null && _activeWebView.CanGoBack)
            {
                _activeWebView.GoBack();
            }
        }

        private void ForwardMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_activeWebView != null && _activeWebView.CanGoForward)
            {
                _activeWebView.GoForward();
            }
        }

        private void RefreshMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _activeWebView?.Reload();
        }

        private async void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 如果已经有缓存的选中文本，直接使用
                if (!string.IsNullOrEmpty(_contextMenuSelectedText))
                {
                    var dataPackage = new DataPackage();
                    dataPackage.SetText(_contextMenuSelectedText);
                    Clipboard.SetContent(dataPackage);
                    Clipboard.Flush();
                    return;
                }

                // 否则，实时从网页获取选中的文本
                if (_activeWebView?.CoreWebView2 != null)
                {
                    string script = "window.getSelection().toString()";
                    string result = await _activeWebView.CoreWebView2.ExecuteScriptAsync(script);
                    
                    // 反序列化 JSON 字符串
                    string? selectedText = null;
                    if (result.Length >= 2 && result.StartsWith("\"") && result.EndsWith("\""))
                    {
                        selectedText = System.Text.Json.JsonSerializer.Deserialize<string>(result);
                    }
                    else if (!string.IsNullOrEmpty(result) && result != "null")
                    {
                        selectedText = result;
                    }

                    if (!string.IsNullOrEmpty(selectedText))
                    {
                        var dataPackage = new DataPackage();
                        dataPackage.SetText(selectedText);
                        Clipboard.SetContent(dataPackage);
                        Clipboard.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to copy text: {ex.Message}");
            }
        }

        private void CopyLinkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_contextMenuLinkUrl))
            {
                return;
            }

            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(_contextMenuLinkUrl);
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to copy link: {ex.Message}");
            }
        }

        private void CopyUrlMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Uri? uri = _activeWebView?.Source;
            if (uri is null)
            {
                return;
            }

            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(uri.AbsoluteUri);
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to copy URL: {ex.Message}");
            }
        }

        private async void OpenExternalMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Uri? uri = _activeWebView?.Source;
            if (uri is null)
            {
                return;
            }

            await Launcher.LaunchUriAsync(uri);
        }

        // ==================== 右键菜单相关方法结束 ====================

        private void TopBarHost_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var now = DateTime.Now;
            var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
            
            System.Diagnostics.Debug.WriteLine($"[TopBarHost_PointerPressed] 点击事件触发，距离上次点击: {timeSinceLastClick}ms");
            
            if (timeSinceLastClick <= DoubleClickMaxDelayMs && timeSinceLastClick > 0)
            {
                // 检测到双击
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("[TopBarHost_PointerPressed] ✓ 检测到双击！");
                System.Diagnostics.Debug.WriteLine("========================================");
                
                HandleDoubleClick();
                _lastClickTime = DateTime.MinValue; // 重置，避免三击被识别为双击
            }
            else
            {
                // 单击
                _lastClickTime = now;
            }
        }
        
        private void HandleDoubleClick()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[HandleDoubleClick] 开始处理双击");
                
                // 方法1: 直接触发导航栏的 WindowStateToggleRequested 事件
                // 从当前页面向上遍历找到 Linker，然后访问 NavBarInstance
                DependencyObject? current = this;
                while (current != null)
                {
                    current = VisualTreeHelper.GetParent(current);
                    
                    // 查找 Linker 类型
                    if (current?.GetType().Name == "Linker")
                    {
                        System.Diagnostics.Debug.WriteLine("[HandleDoubleClick] 找到 Linker");
                        
                        // 通过反射获取 NavBarInstance 属性
                        var navBarProperty = current.GetType().GetProperty("NavBarInstance");
                        if (navBarProperty != null)
                        {
                            var navBar = navBarProperty.GetValue(current);
                            System.Diagnostics.Debug.WriteLine($"[HandleDoubleClick] 获取到 NavBarInstance: {navBar?.GetType().Name ?? "null"}");
                            
                            if (navBar != null)
                            {
                                // 获取 WindowStateToggleRequested 事件并触发
                                var eventField = navBar.GetType().GetField("WindowStateToggleRequested",
                                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                
                                if (eventField != null)
                                {
                                    var eventDelegate = eventField.GetValue(navBar) as MulticastDelegate;
                                    if (eventDelegate != null)
                                    {
                                        System.Diagnostics.Debug.WriteLine("[HandleDoubleClick] ✓ 触发 WindowStateToggleRequested 事件");
                                        eventDelegate.DynamicInvoke(navBar, EventArgs.Empty);
                                        return;
                                    }
                                }
                                
                                // 备用方法：直接调用事件触发方法
                                var raiseMethod = navBar.GetType().GetMethod("OnWindowStateToggleRequested",
                                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                if (raiseMethod != null)
                                {
                                    System.Diagnostics.Debug.WriteLine("[HandleDoubleClick] ✓ 调用 OnWindowStateToggleRequested 方法");
                                    raiseMethod.Invoke(navBar, null);
                                    return;
                                }
                            }
                        }
                        break;
                    }
                }
                
                // 方法2: 直接调用主窗口的 ToggleWindowState
                System.Diagnostics.Debug.WriteLine("[HandleDoubleClick] 尝试直接调用主窗口方法");
                var window = GetMainWindowInstance();
                if (window is Docked_AI.MainWindow mainWindow)
                {
                    System.Diagnostics.Debug.WriteLine("[HandleDoubleClick] ✓ 找到主窗口，调用 ToggleWindowState");
                    mainWindow.ToggleWindowState();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[HandleDoubleClick] ✗ 窗口类型不匹配: {window?.GetType().Name ?? "null"}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HandleDoubleClick] ✗ 异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[HandleDoubleClick] 堆栈: {ex.StackTrace}");
            }
        }

        private void TopBarHost_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("========================================");
            System.Diagnostics.Debug.WriteLine("[TopBarHost_DoubleTapped] 双击事件触发！");
            System.Diagnostics.Debug.WriteLine($"[TopBarHost_DoubleTapped] Sender: {sender?.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"[TopBarHost_DoubleTapped] OriginalSource: {e.OriginalSource?.GetType().Name}");
            System.Diagnostics.Debug.WriteLine("========================================");
            
            HandleDoubleClick();
        }

        private Window? GetMainWindowInstance()
        {
            try
            {
                // 方法1: 使用公共属性
                if (Application.Current is App app)
                {
                    var window = app.MainWindow;
                    System.Diagnostics.Debug.WriteLine($"[GetMainWindowInstance] 从 App.MainWindow 获取: {window?.GetType().Name ?? "null"}");
                    if (window != null)
                    {
                        return window;
                    }
                }
                
                // 方法2: 从 App.Current 获取主窗口（备用）
                System.Diagnostics.Debug.WriteLine("[GetMainWindowInstance] 尝试从 App._window 字段获取");
                if (Application.Current is App app2)
                {
                    var windowField = typeof(App).GetField("_window", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (windowField != null)
                    {
                        var window = windowField.GetValue(app2) as Window;
                        System.Diagnostics.Debug.WriteLine($"[GetMainWindowInstance] 从 App._window 获取: {window?.GetType().Name ?? "null"}");
                        return window;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetMainWindowInstance] 异常: {ex.Message}");
            }
            
            System.Diagnostics.Debug.WriteLine("[GetMainWindowInstance] 所有方法都失败了");
            return null;
        }

        private void DisposeWebView()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Loaded -= WebBrowserPage_Loaded;
            Unloaded -= WebBrowserPage_Unloaded;
            Pages.Settings.SettingsPage.RoundedWebViewSettingsChanged -= OnRoundedWebViewSettingsChanged;
            Pages.Settings.SettingsPage.WinUIContextMenuSettingsChanged -= OnWinUIContextMenuSettingsChanged;
            
            if (_useRoundedWebView)
            {
                this.SizeChanged -= OnPageSizeChanged;
            }
            
            // 移除动态圆角监听
            this.SizeChanged -= OnPageSizeChangedForCorners;

            if (_activeWebView?.CoreWebView2 is not null)
            {
                _activeWebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                _activeWebView.CoreWebView2.DocumentTitleChanged -= CoreWebView2_DocumentTitleChanged;
                _activeWebView.CoreWebView2.HistoryChanged -= CoreWebView2_HistoryChanged;
                _activeWebView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                _activeWebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                _activeWebView.CoreWebView2.ContextMenuRequested -= CoreWebView2_ContextMenuRequested;

                try
                {
                    _activeWebView.CoreWebView2.Stop();
                }
                catch
                {
                    // Ignore cleanup errors during page teardown.
                }
            }

            if (_activeWebView != null)
            {
                _activeWebView.Source = null;
                _activeWebView.Close();
            }
            
            _pendingNavigationUri = null;
            _currentShortcut = null;
            _isWebViewReady = false;
        }

        /// <summary>
        /// 分层策略第一步：尝试从 meta[name="theme-color"] 获取主题色
        /// </summary>
        private async Task TryApplyThemeColorAsync()
        {
            if (_activeWebView?.CoreWebView2 is null)
            {
                return;
            }

            try
            {
                string script = @"
(function() {
    const meta = document.querySelector('meta[name=""theme-color""]');
    if (meta && meta.content) {
        return meta.content;
    }
    return null;
})();";

                string result = await _activeWebView.CoreWebView2.ExecuteScriptAsync(script);
                
                // 移除 JSON 字符串的引号
                if (!string.IsNullOrWhiteSpace(result) && result != "null")
                {
                    string colorString = result.Trim('"');
                    if (TryParseCssColor(colorString, out var themeColor))
                    {
                        _hasAppliedThemeColor = true;
                        ApplyBarTint(isTop: true, themeColor);
                        ApplyBarTint(isTop: false, themeColor);
                        System.Diagnostics.Debug.WriteLine($"Applied theme-color: {colorString}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get theme-color: {ex.Message}");
            }
        }

        /// <summary>
        /// 分层策略终极方案：截图采样（仅在页面完全透明时使用）
        /// </summary>
        private async Task TryScreenshotSamplingAsync()
        {
            if (_activeWebView?.CoreWebView2 is null)
            {
                return;
            }

            try
            {
                using var stream = new InMemoryRandomAccessStream();
                await _activeWebView.CoreWebView2.CapturePreviewAsync(
                    CoreWebView2CapturePreviewImageFormat.Png, 
                    stream);

                stream.Seek(0);
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                var pixelData = await decoder.GetPixelDataAsync();
                byte[] pixels = pixelData.DetachPixelData();

                uint width = decoder.PixelWidth;
                uint height = decoder.PixelHeight;

                if (width == 0 || height == 0)
                {
                    return;
                }

                // 采样顶部 10 行的中心区域
                var topColor = SampleRegion(pixels, width, height, 0, 10);
                if (topColor.HasValue)
                {
                    ApplyBarTint(isTop: true, topColor.Value);
                }

                // 采样底部 10 行的中心区域
                var bottomColor = SampleRegion(pixels, width, height, (int)height - 10, (int)height);
                if (bottomColor.HasValue)
                {
                    ApplyBarTint(isTop: false, bottomColor.Value);
                }

                _hasReceivedFirstTint = true;
                System.Diagnostics.Debug.WriteLine("Applied screenshot sampling colors");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Screenshot sampling failed: {ex.Message}");
                // Fallback 到系统主题色
                ApplySystemAccentColor();
            }
        }

        /// <summary>
        /// 从像素数据中采样指定区域的平均颜色
        /// </summary>
        private Windows.UI.Color? SampleRegion(byte[] pixels, uint width, uint height, int startY, int endY)
        {
            if (pixels.Length == 0 || width == 0 || height == 0)
            {
                return null;
            }

            startY = Math.Max(0, startY);
            endY = Math.Min((int)height, endY);

            // 采样中心 50% 的宽度
            int startX = (int)(width * 0.25);
            int endX = (int)(width * 0.75);

            long sumR = 0, sumG = 0, sumB = 0;
            int count = 0;
            int bytesPerPixel = 4; // BGRA

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    int index = (y * (int)width + x) * bytesPerPixel;
                    if (index + 3 < pixels.Length)
                    {
                        byte b = pixels[index];
                        byte g = pixels[index + 1];
                        byte r = pixels[index + 2];
                        byte a = pixels[index + 3];

                        // 忽略透明像素
                        if (a > 10)
                        {
                            sumR += r;
                            sumG += g;
                            sumB += b;
                            count++;
                        }
                    }
                }
            }

            if (count == 0)
            {
                return null;
            }

            return Windows.UI.Color.FromArgb(
                255,
                (byte)(sumR / count),
                (byte)(sumG / count),
                (byte)(sumB / count)
            );
        }

        /// <summary>
        /// Fallback：应用系统强调色
        /// </summary>
        private void ApplySystemAccentColor()
        {
            try
            {
                // 尝试获取系统强调色
                if (Application.Current.Resources.TryGetValue("SystemAccentColor", out object? accentResource) 
                    && accentResource is Windows.UI.Color accentColor)
                {
                    ApplyBarTint(isTop: true, accentColor);
                    ApplyBarTint(isTop: false, accentColor);
                    System.Diagnostics.Debug.WriteLine("Applied system accent color as fallback");
                }
            }
            catch
            {
                // 最终 fallback：保持透明
            }
        }
    }
}