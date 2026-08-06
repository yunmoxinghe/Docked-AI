using DockedTools.Features.Pages.WebApp.Shared;
using DockedTools.Features.Pages.Settings;
using DockedTools.Features.MainWindowContent.ContentArea;
using DockedTools.Features.UnifiedCalls.InAppDialog;
using DockedTools.Features.UnifiedCalls.TopAppBar;
using DockedTools.Features.UnifiedCalls.AsyncSafety;
using DockedTools.Features.Localization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.System;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    public sealed partial class WebBrowserPage : Page, INavigationAware
    {
        private const string TintMessageType = "DockedTools_tint";
        private const string ThemeColorMessageType = "DockedTools_theme_color";
        private const double LuminanceThreshold = 0.179; // WCAG 标准阈值（归一化后）
        private const double MinOpacity = 0.01;
        private const double PercentageMax = 100.0;
        private const double ColorChannelMax = 255.0;
        private const int ColorTransitionDurationMs = 300; // 颜色过渡动画时长
        
        // 响应式布局间距比例（视觉平衡最佳实践）
        private const double ContainerPaddingMultiplier = 1.0; // 容器边距 = 按钮间距（所有间距一致）
        
        // 按钮状态叠加层强度（Material Design 最佳实践）
        private const double ButtonHoverOverlayStrength = 0.08;   // Hover 叠加 8%
        private const double ButtonPressedOverlayStrength = 0.12; // Pressed 叠加 12%
        private const double ButtonDisabledOpacity = 0.38;        // Disabled 透明度 38%
        private const double ButtonHoverBackgroundOpacity = 0.08; // Hover 背景叠加 8%
        private const double ButtonPressedBackgroundOpacity = 0.12; // Pressed 背景叠加 12%
        
        // 加载进度条相关
        private const int IndeterminateAnimationCycleMs = 500; // 不确定模式动画周期时长（估算）
        
        // 双击检测相关
        private DateTime _lastClickTime = DateTime.MinValue;
        private const int DoubleClickMaxDelayMs = 500; // 双击最大间隔时间（毫秒）
        
        // 重载防抖相关
        private DateTime _lastReloadTime = DateTime.MinValue;
        private bool _isReloading = false;
        private const int ReloadDebounceMs = 500; // 重载防抖时间（毫秒）

        private Uri? _pendingNavigationUri;
        private bool _isWebViewReady;
        private WebAppShortcut? _currentShortcut;
        private string? _contextMenuSelectedText;
        private string? _contextMenuLinkUrl;
        private bool _needsWebViewRecreation; // ⭐ 标记是否需要重新创建 WebView
        private CoreWebView2Environment? _webViewEnvironment; // ⭐ 保存 WebView2 environment 引用（用于订阅 BrowserProcessExited）
        private int _unresponsiveCount; // ⭐ 任务 3.4：记录 RenderProcessUnresponsive 连续次数
        private const int MaxUnresponsiveCountBeforeReload = 3; // ⭐ 连续无响应多少次后触发 Reload
        private bool _isRecoveringWebView; // ⭐ 任务 3.5：防重入 guard，多个进程事件同时触发时只执行一次恢复

        // 键盘映射按钮
        private Button? _leftMappingButton;
        private Button? _rightMappingButton;

        // ✅ 修复：初始背景色完全透明，避免黑色闪现
        // 首次采样后会立即设置为正确的颜色
        private readonly SolidColorBrush _topBarBackgroundBrush = new(Colors.Transparent);
        private readonly SolidColorBrush _bottomBarBackgroundBrush = new(Colors.Transparent);
        private readonly SolidColorBrush _topBarForegroundBrush = new();
        private readonly SolidColorBrush _bottomBarForegroundBrush = new();
        private readonly SolidColorBrush _topBarSecondaryForegroundBrush = new();
        private readonly SolidColorBrush _bottomBarDisabledForegroundBrush = new();
        private readonly SolidColorBrush _bottomBarHoverForegroundBrush = new();
        private bool _isDisposed;
        private bool _hasReceivedFirstTint;
        private bool _hasAppliedThemeColor;
        private string? _instanceId;
        
        // Reactor 底部按钮栏
        private Microsoft.UI.Reactor.Hosting.ReactorHostControl? _reactorHostControl;
        private Components.BottomButtonBar? _bottomButtonBarComponent;
        
        // 顶部栏UI元素
        private StackPanel? _topBarContent;
        private Image? _topBarIcon;
        private FontIcon? _topBarIconFallback;
        private TextBlock? _topBarTitle;
        private Button? _unpinButton;

        public WebBrowserPage()
        {
            InitializeComponent();

            _instanceId = Guid.NewGuid().ToString();

            bool useWinUIContextMenu = ExperimentalSettings.EnableWinUIContextMenu;
            if (!useWinUIContextMenu)
            {
                WebView.ContextFlyout = null;
            }

            InitializeForegroundColors();
            InitializeTopBar();
            InitializeBottomBarReactor(); // ✅ 初始化 Reactor 底部按钮栏

            TopBarTintHost.Background = _topBarBackgroundBrush;
            BottomBarHost.Background = _bottomBarBackgroundBrush;

            BottomBarHost.SizeChanged += (s, e) => UpdateBottomBarLayout();

            Loaded += WebBrowserPage_Loaded;
            Unloaded += WebBrowserPage_Unloaded;
            
            // ✅ 监听系统主题变化
            ActualThemeChanged += OnSystemThemeChanged;
            
            // ✅ 订阅网页应用更新事件
            Shared.WebAppUpdateService.UpdateCompleted += OnWebAppUpdated;
            
            Pages.Settings.SettingsPage.WinUIContextMenuSettingsChanged += OnWinUIContextMenuSettingsChanged;
            Pages.Settings.SettingsPage.WebViewPerformanceSettingsChanged += OnWebViewPerformanceSettingsChanged;
        }

        private void InitializeTopBar()
        {
            // 创建居中的标签页内容
            _topBarContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 8
            };

            // 创建图标容器
            var iconViewbox = new Viewbox
            {
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconGrid = new Grid
            {
                Width = 16,
                Height = 16
            };

            _topBarIcon = new Image
            {
                Stretch = Stretch.UniformToFill,
                Visibility = Visibility.Collapsed
            };

            _topBarIconFallback = new FontIcon
            {
                Glyph = "\uE774",
                Width = 16,
                Height = 16,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            iconGrid.Children.Add(_topBarIcon);
            iconGrid.Children.Add(_topBarIconFallback);
            iconViewbox.Child = iconGrid;

            // 创建标题文本
            _topBarTitle = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 300
            };

            _topBarContent.Children.Add(iconViewbox);
            _topBarContent.Children.Add(_topBarTitle);

            // 创建取消固定按钮
            // 右侧按钮由独立顶部栏统一创建，避免页面自管导致尺寸/裁切不一致。
        }

        private void SetupTopBar()
        {
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] SetupTopBar 被调用");
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] _topBarContent.Children.Count = {_topBarContent?.Children.Count}");
            
            // 在页面加载后设置顶部栏
            TopAppBarService.SetCenterContent(_topBarContent);
            
            // 设置左侧键盘映射按钮
            SetupLeftMappingButton();
            
            // 设置右侧内容（映射按钮 + 关闭按钮）
            SetupRightContent();
            
            TopAppBarService.SetForeground(_topBarForegroundBrush);
            TopAppBarService.IsVisible = true;
            
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 顶部栏内容已设置，IsVisible = true");
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] TopAppBarService.IsVisible = {TopAppBarService.IsVisible}");
            
            // 网页主题色只绘制在本页的 TopBarTintHost，统一顶栏隐藏背景/模糊/分隔线以透出本页色块。
            TopAppBarService.SetChromeVisible(false);
            
            // 恢复标题和图标（如果已有数据）
            UpdateTopBarContent();
        }
        
        private void UpdateTopBarContent()
        {
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] UpdateTopBarContent 被调用");
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] _topBarTitle = {(_topBarTitle != null ? "not null" : "null")}");
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] _currentShortcut = {(_currentShortcut != null ? "not null" : "null")}");
            
            // 更新标题
            if (_topBarTitle != null && _currentShortcut != null)
            {
                if (WebView?.CoreWebView2 != null && !string.IsNullOrWhiteSpace(WebView.CoreWebView2.DocumentTitle))
                {
                    // 如果有网页标题，使用网页标题
                    _topBarTitle.Text = WebView.CoreWebView2.DocumentTitle;
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 标题设置为网页标题: {_topBarTitle.Text}");
                }
                else
                {
                    // 否则使用快捷方式名称或 URL
                    _topBarTitle.Text = string.IsNullOrWhiteSpace(_currentShortcut.Name) 
                        ? (_pendingNavigationUri?.Host ?? _currentShortcut.Url) 
                        : _currentShortcut.Name;
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 标题设置为快捷方式名称: {_topBarTitle.Text}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 无法更新标题：_topBarTitle 或 _currentShortcut 为 null");
            }
            
            // 更新图标（如果已有数据）
            if (_currentShortcut != null && _currentShortcut.IconBytes != null && _currentShortcut.IconBytes.Length > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 显示快捷方式图标");
                _ = ShowShortcutIconAsync(_currentShortcut.IconBytes);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 无图标数据");
            }
        }

        private void InitializeBottomBarReactor()
        {
            // 创建 ReactorHostControl（WinUI ContentControl）
            _reactorHostControl = new Microsoft.UI.Reactor.Hosting.ReactorHostControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,  // 拉伸填充
                VerticalAlignment = VerticalAlignment.Stretch       // 拉伸填充
            };

            // 创建 Reactor 组件实例
            _bottomButtonBarComponent = new Components.BottomButtonBar
            {
                ButtonWidth = 48.0,  // 初始按钮宽度（会根据窗口自适应）
                CanGoBack = false,
                CanGoForward = false,
                OnBackClick = () => BackButton_Click(null!, null!),
                OnForwardClick = () => ForwardButton_Click(null!, null!),
                OnRefreshClick = () => RefreshButton_Click(null!, null!),
                OnCopyUrlClick = () => CopyUrlButton_Click(null!, null!),
                OnOpenExternalClick = () => OpenExternalButton_Click(null!, null!)
            };

            // 挂载组件到 ReactorHostControl
            _reactorHostControl.Mount(_bottomButtonBarComponent);

            // 将 ReactorHostControl 添加到容器
            BottomButtonsContainer.Children.Add(_reactorHostControl);
        }

        private void UpdateBottomBarLayout()
        {
            if (BottomBarHost.ActualWidth <= 0 || _bottomButtonBarComponent == null)
            {
                return;
            }

            const int buttonCount = 5;
            const double minButtonWidth = 40.0;
            const double maxButtonWidth = 68.0;
            const double fixedHorizontalSpacing = 4.0;  // 固定左右和按钮间距

            double availableWidth = BottomBarHost.ActualWidth;
            
            // 计算可用于按钮的宽度（减去固定间距）
            // 总间距 = 左边距 + (按钮数-1)*按钮间距 + 右边距 = fixedHorizontalSpacing * (buttonCount + 1)
            double totalSpacing = fixedHorizontalSpacing * (buttonCount + 1);
            double widthForButtons = availableWidth - totalSpacing;
            double buttonWidth = widthForButtons / buttonCount;
            
            // 限制按钮宽度在最小和最大值之间
            buttonWidth = Math.Max(minButtonWidth, Math.Min(maxButtonWidth, buttonWidth));

            // 更新按钮宽度（间距已经在组件内部固定）
            _bottomButtonBarComponent.ButtonWidth = buttonWidth;
            
            // 触发重新渲染
            _reactorHostControl?.Mount(_bottomButtonBarComponent);

            System.Diagnostics.Debug.WriteLine($"[UpdateBottomBarLayout] buttonWidth={buttonWidth:F2} (间距固定4px)");
        }

        /// <summary>
        /// 更新底部导航按钮的启用/禁用状态
        /// </summary>
        private void UpdateNavigationButtonStates()
        {
            if (_bottomButtonBarComponent == null || _reactorHostControl == null)
            {
                return;
            }

            bool canGoBack = WebView?.CanGoBack ?? false;
            bool canGoForward = WebView?.CanGoForward ?? false;

            // 更新组件 Props
            _bottomButtonBarComponent.CanGoBack = canGoBack;
            _bottomButtonBarComponent.CanGoForward = canGoForward;

            // 触发重新渲染
            _reactorHostControl.Mount(_bottomButtonBarComponent);

            System.Diagnostics.Debug.WriteLine($"[UpdateNavigationButtonStates] CanGoBack={canGoBack}, CanGoForward={canGoForward}");
        }

        private void InitializeForegroundColors()
        {
            UpdateForegroundColorsFromTheme();
        }

        /// <summary>
        /// 从当前主题资源更新前景色（支持主题切换）
        /// </summary>
        private void UpdateForegroundColorsFromTheme()
        {
            if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object? resource) 
                && resource is SolidColorBrush themeBrush)
            {
                _topBarForegroundBrush.Color = themeBrush.Color;
                _bottomBarForegroundBrush.Color = themeBrush.Color;
            }
            else
            {
                var theme = Application.Current.RequestedTheme;
                var defaultColor = theme == ApplicationTheme.Dark ? Colors.White : Colors.Black;
                _topBarForegroundBrush.Color = defaultColor;
                _bottomBarForegroundBrush.Color = defaultColor;
            }

            if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out object? secondaryResource) 
                && secondaryResource is SolidColorBrush secondaryBrush)
            {
                _topBarSecondaryForegroundBrush.Color = secondaryBrush.Color;
            }
            else
            {
                var baseColor = _topBarForegroundBrush.Color;
                _topBarSecondaryForegroundBrush.Color = Windows.UI.Color.FromArgb(
                    (byte)(baseColor.A * 0.7),
                    baseColor.R,
                    baseColor.G,
                    baseColor.B
                );
            }

            if (Application.Current.Resources.TryGetValue("TextFillColorDisabledBrush", out object? disabledResource) 
                && disabledResource is SolidColorBrush disabledBrush)
            {
                _bottomBarDisabledForegroundBrush.Color = disabledBrush.Color;
            }
            else
            {
                var baseColor = _bottomBarForegroundBrush.Color;
                _bottomBarDisabledForegroundBrush.Color = Windows.UI.Color.FromArgb(
                    (byte)(baseColor.A * 0.6),
                    baseColor.R,
                    baseColor.G,
                    baseColor.B
                );
            }
            
            _bottomBarHoverForegroundBrush.Color = AdjustColorBrightness(_bottomBarForegroundBrush.Color, 0.15);
        }

        /// <summary>
        /// 系统主题切换时的回调
        /// </summary>
        private void OnSystemThemeChanged(FrameworkElement sender, object args)
        {
            System.Diagnostics.Debug.WriteLine("═══════════════════════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("[WebBrowserPage] ✅✅✅ ActualThemeChanged 事件触发！");
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 当前 ActualTheme: {ActualTheme}");
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] WebView 状态: CoreWebView2={(WebView?.CoreWebView2 != null ? "✓" : "✗")}, IsReady={_isWebViewReady}");
            System.Diagnostics.Debug.WriteLine("═══════════════════════════════════════════════════════");
            
            // 重新从主题资源获取颜色
            UpdateForegroundColorsFromTheme();
            
            // ✅ 立即更新 TopAppBar 的前景色（包括关闭按钮等）
            TopAppBarService.SetForeground(_topBarForegroundBrush);
            System.Diagnostics.Debug.WriteLine("[WebBrowserPage] TopAppBar 前景色已更新");
            
            // ✅ 核心修复：系统主题切换后，WebView2 内部的网页会自动响应（CSS prefers-color-scheme），
            // 但不会触发 NavigationCompleted 事件，所以我们需要手动触发完整的取色逻辑
            
            if (WebView?.CoreWebView2 != null && _isWebViewReady)
            {
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] WebView 已就绪，强制重新提取网页主题色");
                
                // ✅ 重置取色状态，让取色逻辑重新执行
                _hasReceivedFirstTint = false;
                _hasAppliedThemeColor = false;
                
                // ⭐ 任务 6.4：使用 AsyncSafety 包装 DispatcherQueue.TryEnqueue 中的 async lambda
                AsyncSafety.TryEnqueue(
                    DispatcherQueue,
                    async () =>
                    {
                        System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 等待 500ms 让 WebView2 完成主题切换...");
                        
                        // 等待网页重新渲染（prefers-color-scheme CSS 生效）
                        await Task.Delay(500);
                        
                        System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 开始执行主题切换后的取色");
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 取色前背景色: Top={_topBarBackgroundBrush.Color}, Bottom={_bottomBarBackgroundBrush.Color}");
                        
                        // ✅ 步骤1：尝试 meta theme-color
                        await TryApplyThemeColorAsync();
                        
                        // ✅ 步骤2：如果没有 theme-color，使用脚本采样
                        if (!_hasAppliedThemeColor)
                        {
                            System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 没有 theme-color，触发脚本采样取色");
                            await Task.Delay(100);
                            await TriggerTintSamplingAsync();
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 取色完成后背景色: Top={_topBarBackgroundBrush.Color}, Bottom={_bottomBarBackgroundBrush.Color}");
                    },
                    "WebBrowserPage",
                    "ThemeChanged");
            }
            else
            {
                // WebView 还没准备好，只更新前景色
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] ⚠️ WebView 未就绪，仅更新前景色");
            }
        }

        /// <summary>
        /// 应用系统主题的默认颜色（当没有网页主题色时使用）
        /// </summary>
        private void ApplySystemThemeColors()
        {
            // 从系统资源获取强调色或卡片背景色
            if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out object? bgResource) 
                && bgResource is SolidColorBrush bgBrush)
            {
                _topBarBackgroundBrush.Color = bgBrush.Color;
                _bottomBarBackgroundBrush.Color = bgBrush.Color;
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 应用系统卡片背景色");
            }
            else if (Application.Current.Resources.TryGetValue("SystemAccentColor", out object? accentResource) 
                && accentResource is Windows.UI.Color accentColor)
            {
                _topBarBackgroundBrush.Color = accentColor;
                _bottomBarBackgroundBrush.Color = accentColor;
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 应用系统强调色");
            }
            else
            {
                // 回退：根据当前主题选择浅灰或深灰
                var theme = Application.Current.RequestedTheme;
                var defaultBgColor = theme == ApplicationTheme.Dark 
                    ? Windows.UI.Color.FromArgb(255, 32, 32, 32)   // 深色主题：深灰
                    : Windows.UI.Color.FromArgb(255, 243, 243, 243); // 浅色主题：浅灰
                
                _topBarBackgroundBrush.Color = defaultBgColor;
                _bottomBarBackgroundBrush.Color = defaultBgColor;
                System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 应用回退背景色 (主题: {theme})");
            }
        }

        // ⚠️ 旧方法已废弃：SetButtonStateColors, ApplyBottomBarResponsiveLayout, UpdateButtonResources
        // 现在使用 Reactor 组件管理按钮

        private void OnWinUIContextMenuSettingsChanged(object? sender, EventArgs e)
        {
            // 设置改变时，更新右键菜单配置
            bool useWinUIContextMenu = ExperimentalSettings.EnableWinUIContextMenu;
            
            // 更新 WebView 的配置
            if (WebView?.CoreWebView2 != null)
            {
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = !useWinUIContextMenu;
                UpdateContextMenuForWebView(WebView, useWinUIContextMenu);
            }
        }

        private void OnWebViewPerformanceSettingsChanged(object? sender, EventArgs e)
        {
            // 性能设置改变时，应用新设置
            // 注意：某些设置需要重启 WebView 才能生效（如浏览器参数）
            ApplyMemoryModeSettings();
            
            System.Diagnostics.Debug.WriteLine("[OnWebViewPerformanceSettingsChanged] 性能设置已更新，某些设置需要重新加载页面才能生效");
        }

        private void UpdateContextMenuConfiguration(bool useWinUIContextMenu)
        {
            // 配置 WebView
            UpdateContextMenuForWebView(WebView, useWinUIContextMenu);
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
                if (webView.ContextFlyout == null && webView == WebView)
                {
                    webView.ContextFlyout = WebViewContextMenu;
                }
            }
            else
            {
                // 禁用 WinUI 右键菜单：移除 ContextFlyout
                webView.ContextFlyout = null;
            }
        }

        // ⚠️ 旧方法已删除：ApplyBottomBarResponsiveLayout
        // 现在使用 UpdateBottomBarLayout (Reactor)

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
            if (_topBarTitle != null)
            {
                _topBarTitle.Text = string.IsNullOrWhiteSpace(shortcut.Name) ? uri.Host : shortcut.Name;
            }
            _ = ShowShortcutIconAsync(shortcut.IconBytes);

            _pendingNavigationUri = uri;
            TryNavigatePendingUri();
            
            // ⭐ 链接 WebView 到 LRU 管理器（在 _currentShortcut 设置之后）
            if (_currentShortcut != null)
            {
                if (!WebViewManager.IsLinked(_currentShortcut.Id))
                {
                    var result = WebViewManager.RequestLink(_currentShortcut.Id, this);
                    if (result.Success)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] WebView 已链接到 LRU: {_currentShortcut.Id}");
                        if (result.EvictedOldest)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] LRU 淘汰了旧的 WebView");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] WebView 链接失败: {result.ErrorMessage}");
                    }
                }
                else
                {
                    // 已经链接，更新访问顺序
                    WebViewManager.RequestLink(_currentShortcut.Id, this);
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] WebView 已链接，更新访问顺序: {_currentShortcut.Id}");
                }
                
                WebViewManager.DiagnoseState();
            }
            
            // 首次导航时设置顶部栏
            SetupTopBar();
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            RestoreSharedTopAppBarBackground();
        }

        // INavigationAware 实现
        void INavigationAware.OnNavigatedTo(object? parameter)
        {
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] INavigationAware.OnNavigatedTo called");
            
            // ⭐ 如果页面被 LRU 清理过，需要重置 _isDisposed 标志以允许重新初始化
            if (_isDisposed)
            {
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 页面之前被清理过，重置状态以允许恢复");
                _isDisposed = false;
                _isWebViewReady = false;
                _hasReceivedFirstTint = false;
                _hasAppliedThemeColor = false;
                
                // 重新订阅事件
                Loaded += WebBrowserPage_Loaded;
                Unloaded += WebBrowserPage_Unloaded;
                Pages.Settings.SettingsPage.WinUIContextMenuSettingsChanged += OnWinUIContextMenuSettingsChanged;
                Pages.Settings.SettingsPage.WebViewPerformanceSettingsChanged += OnWebViewPerformanceSettingsChanged;
                
                // ⭐ 恢复待导航的 URI（如果有 _currentShortcut）
                if (_currentShortcut != null && Uri.TryCreate(_currentShortcut.Url, UriKind.Absolute, out Uri? uri))
                {
                    _pendingNavigationUri = uri;
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 恢复待导航 URI: {uri}");
                }
            }
            else
            {
                // ✅ 优化：切换到已开启的标签时，刷新上下颜色
                // 确保颜色与当前网页状态一致（处理网页动态改变主题的情况）
                if (WebView?.CoreWebView2 != null && _isWebViewReady)
                {
                    System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 标签切换：刷新网页主题色");
                    
                    AsyncSafety.TryEnqueue(
                        DispatcherQueue,
                        async () =>
                        {
                            // 重置状态标志，允许重新提取颜色
                            _hasAppliedThemeColor = false;
                            
                            // 重新提取主题色
                            await TryApplyThemeColorAsync();
                            
                            // 如果没有 theme-color，触发采样取色
                            if (!_hasAppliedThemeColor)
                            {
                                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 标签切换：没有 theme-color，触发采样取色");
                                await Task.Delay(50); // 短暂延迟，确保 UI 已切换
                                await TriggerTintSamplingAsync();
                            }
                        },
                        "WebBrowserPage",
                        "RefreshThemeColorOnTabSwitch");
                }
            }
            
            // ⭐ 如果需要重新创建 WebView，先重新创建
            if (_needsWebViewRecreation)
            {
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 需要重新创建 WebView");
                
                // ⭐ 任务 3.5：检查是否正在恢复中
                if (!_isRecoveringWebView)
                {
                    RecreateWebView();
                    _needsWebViewRecreation = false;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[WebBrowserPage] ⚠️ 正在恢复中，跳过 RecreateWebView");
                }
            }
            
            // ⭐ 重新链接到 LRU（页面恢复时必须重新加入 LRU 管理）
            if (_currentShortcut != null)
            {
                var result = WebViewManager.RequestLink(_currentShortcut.Id, this);
                if (result.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 页面恢复，重新链接到 LRU: {_currentShortcut.Id}");
                    if (result.EvictedOldest)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] LRU 淘汰了旧的 WebView");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 重新链接失败: {result.ErrorMessage}");
                }
                WebViewManager.DiagnoseState();
            }
            
            // 重新设置顶部栏（因为可能被其他页面清除了）
            SetupTopBar();
            
            // 如果 WebView 被暂停，恢复它
            if (ExperimentalSettings.SuspendInactiveWebView && WebView?.CoreWebView2 != null)
            {
                try
                {
                    WebView.CoreWebView2.Resume();
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] WebView 已恢复");
                    
                    // ✅ 修复：WebView 恢复后重新注入取色脚本
                    _ = ReInjectTintScriptAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 恢复 WebView 失败: {ex.Message}");
                }
            }
            
            // 检查 WebView 状态并初始化
            if (!_isWebViewReady || WebView?.CoreWebView2 == null)
            {
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] WebView 需要初始化");
                _ = EnsureWebViewInitializedAsync().ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        System.Diagnostics.Debug.WriteLine("[WebBrowserPage] WebView 初始化完成");
                        TryNavigatePendingUri();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] WebView 初始化失败: {t.Exception?.Message}");
                    }
                });
            }
            else if (WebView.Source == null && _currentShortcut != null)
            {
                // WebView 已初始化但为空白页，恢复导航
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] WebView 为空白页，恢复导航");
                if (Uri.TryCreate(_currentShortcut.Url, UriKind.Absolute, out Uri? uri))
                {
                    _pendingNavigationUri = uri;
                    TryNavigatePendingUri();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] WebView 状态正常，当前 URL: {WebView.Source}");
                
                // ✅ 修复：页面恢复时重新取色
                _ = RefreshPageTintAsync();
            }
        }

        void INavigationAware.OnNavigatedFrom()
        {
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] INavigationAware.OnNavigatedFrom called");
            
            RestoreSharedTopAppBarBackground();
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 已恢复统一顶部栏背景");
            
            // 如果启用了暂停不活跃 WebView 的功能
            if (ExperimentalSettings.SuspendInactiveWebView && WebView?.CoreWebView2 != null)
            {
                try
                {
                    _ = WebView.CoreWebView2.TrySuspendAsync();
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] WebView 已暂停");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 暂停 WebView 失败: {ex.Message}");
                }
            }
            
            // 如果启用了自动清理缓存
            if (ExperimentalSettings.AutoClearCache && WebView?.CoreWebView2 != null)
            {
                _ = ClearBrowsingDataAsync();
            }
            
            // 注意：不在这里 Unlink，因为页面可能被缓存并稍后恢复
            // Unlink 由 DisposeWebView 负责（当页面真正被销毁时）
        }

        /// <summary>
        /// ⭐ 任务 6.3：WebBrowserPage_Loaded 事件入口（委托到异步实现）
        /// </summary>
        private async void WebBrowserPage_Loaded(object sender, RoutedEventArgs e)
        {
            AsyncSafety.Run(
                async () => await WebBrowserPageLoadedAsync(sender, e),
                "WebBrowserPage",
                "Loaded");
        }

        /// <summary>
        /// ⭐ 任务 6.3：WebBrowserPage_Loaded 异步实现
        /// </summary>
        private async Task WebBrowserPageLoadedAsync(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] Loaded 事件触发");
            
            // Loaded 事件只负责初始化 WebView，不干预导航和链接管理
            // 链接管理由 INavigationAware.OnNavigatedTo 负责
            
            await EnsureWebViewInitializedAsync();
            TryNavigatePendingUri();
        }

        /// <summary>
        /// 处理网页应用配置更新事件
        /// </summary>
        private async void OnWebAppUpdated(object? sender, Shared.WebAppUpdateEventArgs e)
        {
            // 只处理当前快捷方式的更新
            if (_currentShortcut == null || e.AppId != _currentShortcut.Id)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 收到更新通知: {e.AppId}, 类型: {e.UpdateType}");

            // 如果按钮配置有变化，重新加载快捷方式并刷新按钮
            if (e.UpdateType.HasFlag(Shared.WebAppUpdateType.ButtonConfig))
            {
                await DispatcherQueue.EnqueueAsync(async () =>
                {
                    try
                    {
                        // 重新加载快捷方式数据
                        var shortcuts = await Shared.WebAppShortcutStore.LoadAsync();
                        var updatedShortcut = shortcuts.FirstOrDefault(s => s.Id == e.AppId);
                        
                        if (updatedShortcut != null)
                        {
                            _currentShortcut = updatedShortcut;
                            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 快捷方式已重新加载: {updatedShortcut.Name}");
                            
                            // 重新设置顶部栏（刷新按钮）
                            SetupTopBar();
                            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 按钮配置已刷新");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 更新按钮配置失败: {ex.Message}");
                    }
                });
            }

            // 如果名称或图标有变化，更新标题栏
            if (e.UpdateType.HasFlag(Shared.WebAppUpdateType.Name) || 
                e.UpdateType.HasFlag(Shared.WebAppUpdateType.Icon))
            {
                await DispatcherQueue.EnqueueAsync(async () =>
                {
                    try
                    {
                        var shortcuts = await Shared.WebAppShortcutStore.LoadAsync();
                        var updatedShortcut = shortcuts.FirstOrDefault(s => s.Id == e.AppId);
                        
                        if (updatedShortcut != null)
                        {
                            _currentShortcut = updatedShortcut;
                            UpdateTopBarContent();
                            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 标题栏已更新");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 更新标题栏失败: {ex.Message}");
                    }
                });
            }
        }

        private void WebBrowserPage_Unloaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] Unloaded 事件触发");
            
            // 取消订阅更新事件
            Shared.WebAppUpdateService.UpdateCompleted -= OnWebAppUpdated;
            
            // 延迟检查：如果页面真的被移除了（不在可视树中），才清理
            // 使用 DispatcherQueue 延迟执行，让 Loaded 事件有机会先触发
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                // 检查页面是否还在可视树中
                if (XamlRoot == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 页面已从可视树移除，但由于缓存机制不清理顶部栏");
                    // 注意：即使页面从可视树移除，也不清理顶部栏
                    // 因为页面可能被缓存，稍后会恢复
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 页面仍在可视树中，Unloaded 是误触发");
                }
            });
        }

        private async Task EnsureWebViewInitializedAsync()
        {
            if (WebView == null)
            {
                System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] WebView 为 null，无法初始化");
                return;
            }

            // ⭐ 如果 WebView 已经 ready 且 CoreWebView2 存在，直接返回
            if (_isWebViewReady && WebView.CoreWebView2 != null)
            {
                System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] WebView 已就绪，跳过初始化");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] 开始初始化 WebView");
            
            // ⭐ 检查 CoreWebView2 是否已经初始化（可能是首次加载，CoreWebView2 还未初始化）
            if (WebView.CoreWebView2 != null)
            {
                System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] CoreWebView2 已存在，重新配置");
                
                // 重新配置设置
                bool useWinUIContextMenu = ExperimentalSettings.EnableWinUIContextMenu;
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = !useWinUIContextMenu;
                WebView.CoreWebView2.Settings.IsSwipeNavigationEnabled = true;
                WebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                
                // 应用内存模式设置
                ApplyMemoryModeSettings();
                
                // 重新订阅事件
                WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                WebView.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
                WebView.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
                WebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                
                // ⭐ 任务 3.2：订阅 ProcessFailed 事件（防止重复订阅）
                WebView.CoreWebView2.ProcessFailed -= CoreWebView2_ProcessFailed;
                WebView.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed;
                
                // ⭐ 任务 3.2：订阅 BrowserProcessExited 事件（如果 environment 已存在）
                if (_webViewEnvironment != null)
                {
                    _webViewEnvironment.BrowserProcessExited -= CoreWebView2Environment_BrowserProcessExited;
                    _webViewEnvironment.BrowserProcessExited += CoreWebView2Environment_BrowserProcessExited;
                }
                
                // 根据设置配置右键菜单
                UpdateContextMenuConfiguration(useWinUIContextMenu);
                
                // 重新注入脚本
                _ = Task.Run(async () => 
                {
                    await Task.Delay(100);
                    await DispatcherQueue.EnqueueAsync(async () => 
                    {
                        await EnsureTintScriptInstalledAsync();
                    });
                });
                
                _isWebViewReady = true;
                System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] ✅ WebView 重新配置完成");
                return;
            }

            try
            {
                // 检查 WebView2 Runtime 是否可用
                string? runtimeVersion = null;
                try
                {
                    runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
                    System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] WebView2 Runtime 版本: {runtimeVersion}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] ❌ WebView2 Runtime 未安装或不可用: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] 请从以下地址下载并安装 WebView2 Runtime:");
                    System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] https://developer.microsoft.com/microsoft-edge/webview2/");
                    
                    // 显示用户友好的错误消息
                    await ShowWebView2RuntimeMissingDialogAsync();
                    return;
                }

                CoreWebView2EnvironmentOptions options = new()
                {
                    Language = GetWebViewLanguage(),
                    // 优化触摸板滚动体验的浏览器参数
                    AdditionalBrowserArguments = BuildBrowserArguments()
                };
                
                System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] 创建 CoreWebView2Environment...");
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    browserExecutableFolder: null,
                    userDataFolder: null,
                    options: options);
                
                // ⭐ 保存 environment 引用（用于后续订阅 BrowserProcessExited）
                _webViewEnvironment = environment;
                
                // ⭐ 任务 3.2：订阅 BrowserProcessExited 事件（防止重复订阅）
                _webViewEnvironment.BrowserProcessExited -= CoreWebView2Environment_BrowserProcessExited;
                _webViewEnvironment.BrowserProcessExited += CoreWebView2Environment_BrowserProcessExited;
                
                System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] 初始化 CoreWebView2...");
                await WebView.EnsureCoreWebView2Async(environment);
                
                // 设置 WebView2 背景透明
                WebView.DefaultBackgroundColor = Microsoft.UI.Colors.Transparent;

                if (WebView.CoreWebView2 is not null)
                {
                    System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] ✅ CoreWebView2 初始化成功");
                    
                    WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                    
                    // 优化触摸板和滚动体验
                    WebView.CoreWebView2.Settings.IsSwipeNavigationEnabled = true;
                    
                    // 禁用触摸板缩放
                    WebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                    
                    // 禁用状态栏（悬停链接时左下角不显示 URL）
                    WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                    
                    // 根据设置决定是否禁用默认右键菜单
                    bool useWinUIContextMenu = ExperimentalSettings.EnableWinUIContextMenu;
                    WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = !useWinUIContextMenu;
                    
                    // 应用内存模式设置
                    ApplyMemoryModeSettings();
                    
                    WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                    WebView.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
                    WebView.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
                    WebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                    WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                    
                    // ⭐ 任务 3.2：订阅 ProcessFailed 事件
                    WebView.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed;
                    
                    // 根据设置配置右键菜单
                    UpdateContextMenuConfiguration(useWinUIContextMenu);
                    
                    // ✅ 延迟注入脚本，不阻塞首次导航
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(100); // 让首次导航先开始
                        await DispatcherQueue.EnqueueAsync(async () => 
                        {
                            await EnsureTintScriptInstalledAsync();
                        });
                    });
                    
                    // 只有在 CoreWebView2 成功初始化后才设置为 ready
                    _isWebViewReady = true;
                    System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] ✅ WebView 初始化完成，准备导航");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] ❌ CoreWebView2 为 null，初始化失败");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] ❌ WebView 初始化失败: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] 错误消息: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[EnsureWebViewInitializedAsync] 堆栈跟踪: {ex.StackTrace}");
                _isWebViewReady = false;
                
                // 显示用户友好的错误消息
                await ShowWebViewInitializationErrorDialogAsync(ex);
            }
        }

        private async Task ShowWebView2RuntimeMissingDialogAsync()
        {
            try
            {
                if (DispatcherQueue == null)
                {
                    return;
                }

                await DispatcherQueue.EnqueueAsync(async () =>
                {
                    var dialog = new DockedTools.Features.UnifiedCalls.InAppDialog.UnifiedInAppDialog();
                    dialog.Configure(
                        LocalizationHelper.GetString("WebView2_NotInstalled_Title"),
                        LocalizationHelper.GetString("WebView2_NotInstalled_Content"),
                        closeButtonText: LocalizationHelper.GetString("WebView2_NotInstalled_CloseButton")
                    );

                    await DockedTools.Features.UnifiedCalls.InAppDialog.InAppDialogService.ShowAsync(dialog, this);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShowWebView2RuntimeMissingDialogAsync] 显示对话框失败: {ex.Message}");
            }
        }

        private async Task ShowWebViewInitializationErrorDialogAsync(Exception ex)
        {
            try
            {
                if (DispatcherQueue == null)
                {
                    return;
                }

                await DispatcherQueue.EnqueueAsync(async () =>
                {
                    var dialog = new DockedTools.Features.UnifiedCalls.InAppDialog.UnifiedInAppDialog();
                    dialog.Configure(
                        LocalizationHelper.GetString("WebView2_InitFailed_Title"),
                        string.Format(LocalizationHelper.GetString("WebView2_InitFailed_Content"), ex.GetType().Name, ex.Message),
                        closeButtonText: LocalizationHelper.GetString("WebView2_InitFailed_CloseButton")
                    );

                    await DockedTools.Features.UnifiedCalls.InAppDialog.InAppDialogService.ShowAsync(dialog, this);
                });
            }
            catch (Exception dialogEx)
            {
                System.Diagnostics.Debug.WriteLine($"[ShowWebViewInitializationErrorDialogAsync] 显示对话框失败: {dialogEx.Message}");
            }
        }

        private void TryNavigatePendingUri()
        {
            System.Diagnostics.Debug.WriteLine($"[TryNavigatePendingUri] 被调用");
            System.Diagnostics.Debug.WriteLine($"[TryNavigatePendingUri] _isWebViewReady={_isWebViewReady}");
            System.Diagnostics.Debug.WriteLine($"[TryNavigatePendingUri] _pendingNavigationUri={_pendingNavigationUri}");
            System.Diagnostics.Debug.WriteLine($"[TryNavigatePendingUri] WebView={WebView != null}");
            System.Diagnostics.Debug.WriteLine($"[TryNavigatePendingUri] WebView.CoreWebView2={WebView?.CoreWebView2 != null}");
            
            if (!_isWebViewReady || _pendingNavigationUri is null || WebView == null)
            {
                System.Diagnostics.Debug.WriteLine($"[TryNavigatePendingUri] 跳过导航：条件不满足");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[TryNavigatePendingUri] ✅ 开始导航到: {_pendingNavigationUri}");
                WebView.Source = _pendingNavigationUri;
                _pendingNavigationUri = null;
                System.Diagnostics.Debug.WriteLine($"[TryNavigatePendingUri] ✅ 导航请求已发送");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TryNavigatePendingUri] ❌ 导航失败: {ex.Message}");
            }
        }

        private async Task EnsureTintScriptInstalledAsync()
        {
            if (WebView?.CoreWebView2 is null)
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
      type: 'DockedTools_tint', 
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
    
    // ✅ 修复：首次采样时即使是 null 也要发送（告诉宿主页面是透明的）
    // 之后的采样才需要去重
    const isFirstSample = (state.lastTop === null && state.lastBottom === null);
    if (!isFirstSample && top === state.lastTop && bottom === state.lastBottom) return;
    
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
  
  // ✅ 修复：不在脚本加载时自动触发，完全由 C# 控制首次采样时机
  // 只监听 DOMContentLoaded 和 load 事件，但不立即执行
  // document.addEventListener('DOMContentLoaded', schedule);
  // window.addEventListener('load', schedule);
  
  // 注释掉自动触发，避免过早采样导致黑屏闪现
  // if (document.readyState === 'complete') {
  //   schedule();
  // } else {
  //   window.addEventListener('load', () => {
  //     setTimeout(schedule, 100);
  //   }, { once: true });
  // }
})();";

            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // ✅ 修复：不在导航开始时重置取色状态
            // 改为在导航完成后再重置，避免脚本注入前状态被清空
            // _hasReceivedFirstTint = false;
            // _hasAppliedThemeColor = false;
            
            // 显示加载条
            DispatcherQueue.TryEnqueue(() =>
            {
                if (LoadingProgressBar != null)
                {
                    LoadingProgressBar.IsIndeterminate = true;
                    LoadingProgressBar.Visibility = Visibility.Visible;
                }
            });
        }

        /// <summary>
        /// ⭐ 任务 6.3：CoreWebView2_NavigationCompleted 事件入口（委托到异步实现）
        /// </summary>
        private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            AsyncSafety.Run(
                async () => await CoreWebView2NavigationCompletedAsync(sender, e),
                "WebBrowserPage",
                "NavigationCompleted");
        }

        /// <summary>
        /// ⭐ 任务 6.3：CoreWebView2_NavigationCompleted 异步实现
        /// </summary>
        private async Task CoreWebView2NavigationCompletedAsync(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            UpdateNavigationButtonStates();
            
            // ⭐ 任务 3.4：导航成功后重置无响应计数器
            _unresponsiveCount = 0;
            
            // ✅ 修复：在导航完成时重置取色状态
            // 确保新页面能重新取色
            _hasReceivedFirstTint = false;
            _hasAppliedThemeColor = false;
            System.Diagnostics.Debug.WriteLine("[CoreWebView2_NavigationCompleted] 取色状态已重置");
            
            // 平滑隐藏加载条：先停止动画，等待当前周期完成，再隐藏
            await HideLoadingProgressBarSmoothlyAsync();
            
            // ✅ 修复：等待页面渲染完成后再取色
            // 延迟 300ms 确保 DOM 完全加载和渲染（原来 200ms 不够）
            await Task.Delay(300);
            
            // 分层取色策略：优先使用 theme-color
            await TryApplyThemeColorAsync();
            
            // ✅ 修复：如果没有 theme-color，主动触发一次采样取色
            if (!_hasAppliedThemeColor)
            {
                System.Diagnostics.Debug.WriteLine("[CoreWebView2_NavigationCompleted] 没有 theme-color，触发采样取色");
                
                // 再等待 100ms，确保脚本的 load 事件已触发
                await Task.Delay(100);
                await TriggerTintSamplingAsync();
            }
        }

        private void CoreWebView2_HistoryChanged(object? sender, object e)
        {
            UpdateNavigationButtonStates();
        }
        
        /// <summary>
        /// WebView2 浏览器进程退出事件处理器（占位，任务 3.2-3.4 将实现完整功能）
        /// </summary>
        private void CoreWebView2Environment_BrowserProcessExited(object? sender, CoreWebView2BrowserProcessExitedEventArgs e)
        {
            // TODO: 任务 3.3 将实现完整的日志记录逻辑
            // TODO: 任务 3.4 将实现恢复策略
            System.Diagnostics.Debug.WriteLine($"[CoreWebView2Environment_BrowserProcessExited] 浏览器进程退出 (占位方法)");
        }
        
        /// <summary>
        /// ⭐ 任务 3.3：WebView2 进程失败事件处理器（已完成）
        /// 记录 ProcessFailedKind、Reason、当前 URL、Shortcut ID、是否正在恢复等诊断信息
        /// 捕获 handler 内部异常，避免二次崩溃
        /// </summary>
        private void CoreWebView2_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
        {
            try
            {
                // ✅ 记录 ProcessFailedKind
                var processFailedKind = e.ProcessFailedKind;
                
                // ✅ 记录 Reason
                var reason = e.Reason;
                
                // ✅ 记录当前 URL
                string? currentUrl = null;
                try
                {
                    currentUrl = WebView?.CoreWebView2?.Source ?? _pendingNavigationUri?.ToString() ?? "未知";
                }
                catch
                {
                    currentUrl = "无法获取";
                }
                
                // ✅ 记录 Shortcut ID
                var shortcutId = _currentShortcut?.Id ?? "null";
                var shortcutName = _currentShortcut?.Name ?? "未知";
                
                // ✅ 记录是否正在恢复（通过检查相关标志）
                var isRecovering = _needsWebViewRecreation ? "是" : "否";
                
                // 记录进程描述信息（如果可用）
                var processDescription = !string.IsNullOrEmpty(e.ProcessDescription) 
                    ? e.ProcessDescription 
                    : "无描述";
                
                // 记录 ExitCode（如果可用）
                int? exitCode = null;
                try
                {
                    exitCode = e.ExitCode;
                }
                catch
                {
                    // ExitCode 可能不可用（某些失败类型）
                }
                
                // ✅ 构建详细的日志消息（包含所有需求字段）
                var logMessage = $"WebView2 进程失败\n" +
                                $"  ProcessFailedKind: {processFailedKind}\n" +
                                $"  Reason: {reason}\n" +
                                $"  ProcessDescription: {processDescription}\n" +
                                $"  ExitCode: {(exitCode.HasValue ? exitCode.Value.ToString() : "N/A")}\n" +
                                $"  当前 URL: {currentUrl}\n" +
                                $"  Shortcut ID: {shortcutId}\n" +
                                $"  Shortcut 名称: {shortcutName}\n" +
                                $"  是否正在恢复: {isRecovering}\n" +
                                $"  IsDisposed: {_isDisposed}\n" +
                                $"  IsWebViewReady: {_isWebViewReady}\n" +
                                $"  实例 ID: {_instanceId}";
                
                // ✅ 输出到调试控制台
                System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] {logMessage}");
                
                // ✅ 使用 LogService 记录到文件（需求：2.3.2）
                Features.UnifiedCalls.Logging.LogService.Error(
                    "WebView2.ProcessFailed",
                    logMessage);
                
                // ⭐ 任务 3.4：实现恢复策略（需求：2.3.3、3.2.1、3.2.2、3.2.3）
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    try
                    {
                        // ⭐ 任务 3.5：防重入检查 - 如果正在恢复中，直接返回（需求：2.3.3）
                        if (_isRecoveringWebView)
                        {
                            System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ⚠️ 正在恢复中，忽略本次事件以防止重入");
                            Features.UnifiedCalls.Logging.LogService.Warning(
                                "WebView2.ProcessFailed",
                                $"防重入保护：忽略 {processFailedKind} 事件（恢复正在进行中）");
                            return;
                        }
                        
                        switch (processFailedKind)
                        {
                            case CoreWebView2ProcessFailedKind.RenderProcessExited:
                                // ⭐ 渲染进程退出：优先调用 Reload（需求：3.2.1）
                                System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] 检测到 RenderProcessExited，尝试 Reload");
                                
                                // ⭐ 任务 3.5：设置恢复标志
                                _isRecoveringWebView = true;
                                
                                if (WebView?.CoreWebView2 != null)
                                {
                                    try
                                    {
                                        WebView.CoreWebView2.Reload();
                                        System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ Reload 已调用");
                                        
                                        // 重置无响应计数器（Reload 后重置）
                                        _unresponsiveCount = 0;
                                    }
                                    catch (Exception reloadEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] ❌ Reload 失败: {reloadEx.Message}");
                                        Features.UnifiedCalls.Logging.LogService.Error(
                                            "WebView2.ProcessFailed.Reload",
                                            "渲染进程退出后尝试 Reload 失败",
                                            reloadEx);
                                    }
                                    finally
                                    {
                                        // ⭐ 任务 3.5：恢复结束后重置 guard
                                        _isRecoveringWebView = false;
                                        System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ 恢复标志已重置（RenderProcessExited）");
                                    }
                                }
                                else
                                {
                                    // WebView 不可用，重置标志
                                    _isRecoveringWebView = false;
                                }
                                break;
                            
                            case CoreWebView2ProcessFailedKind.BrowserProcessExited:
                                // ⭐ 主浏览器进程退出：标记需要重建，关闭旧 WebView（需求：3.2.2）
                                System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] 检测到 BrowserProcessExited，标记需要重建 WebView");
                                
                                // ⭐ 任务 3.5：设置恢复标志
                                _isRecoveringWebView = true;
                                
                                _needsWebViewRecreation = true;
                                
                                // 关闭旧 WebView（清理资源）
                                try
                                {
                                    if (WebView?.CoreWebView2 != null)
                                    {
                                        WebView.CoreWebView2.ProcessFailed -= CoreWebView2_ProcessFailed;
                                        WebView.Close();
                                        System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ 旧 WebView 已关闭");
                                    }
                                    
                                    // 重新创建 WebView
                                    RecreateWebView();
                                    _needsWebViewRecreation = false;
                                    
                                    System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ WebView 已重建");
                                }
                                catch (Exception recreateEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] ❌ 重建 WebView 失败: {recreateEx.Message}");
                                    Features.UnifiedCalls.Logging.LogService.Error(
                                        "WebView2.ProcessFailed.Recreate",
                                        "主浏览器进程退出后尝试重建 WebView 失败",
                                        recreateEx);
                                }
                                finally
                                {
                                    // ⭐ 任务 3.5：恢复结束后重置 guard
                                    _isRecoveringWebView = false;
                                    System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ 恢复标志已重置（BrowserProcessExited）");
                                }
                                break;
                            
                            case CoreWebView2ProcessFailedKind.RenderProcessUnresponsive:
                                // ⭐ 渲染进程无响应：记录次数，连续多次后 reload（需求：3.2.3）
                                _unresponsiveCount++;
                                System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] 检测到 RenderProcessUnresponsive，计数: {_unresponsiveCount}/{MaxUnresponsiveCountBeforeReload}");
                                
                                if (_unresponsiveCount >= MaxUnresponsiveCountBeforeReload)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] 连续无响应达到 {MaxUnresponsiveCountBeforeReload} 次，触发 Reload");
                                    
                                    // ⭐ 任务 3.5：设置恢复标志
                                    _isRecoveringWebView = true;
                                    
                                    if (WebView?.CoreWebView2 != null)
                                    {
                                        try
                                        {
                                            WebView.CoreWebView2.Reload();
                                            System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ Reload 已调用（无响应恢复）");
                                            
                                            // 重置计数器
                                            _unresponsiveCount = 0;
                                        }
                                        catch (Exception reloadEx)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] ❌ Reload 失败（无响应恢复）: {reloadEx.Message}");
                                            Features.UnifiedCalls.Logging.LogService.Error(
                                                "WebView2.ProcessFailed.UnresponsiveReload",
                                                $"连续无响应 {MaxUnresponsiveCountBeforeReload} 次后尝试 Reload 失败",
                                                reloadEx);
                                        }
                                        finally
                                        {
                                            // ⭐ 任务 3.5：恢复结束后重置 guard
                                            _isRecoveringWebView = false;
                                            System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ 恢复标志已重置（RenderProcessUnresponsive）");
                                        }
                                    }
                                    else
                                    {
                                        // WebView 不可用，重置标志
                                        _isRecoveringWebView = false;
                                    }
                                }
                                break;
                            
                            case CoreWebView2ProcessFailedKind.FrameRenderProcessExited:
                                // Frame 渲染进程退出：仅记录日志，通常不需要恢复
                                System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] 检测到 FrameRenderProcessExited，仅记录日志");
                                break;
                            
                            case CoreWebView2ProcessFailedKind.UtilityProcessExited:
                            case CoreWebView2ProcessFailedKind.SandboxHelperProcessExited:
                            case CoreWebView2ProcessFailedKind.GpuProcessExited:
                                // 辅助进程退出：通常无需恢复，仅记录诊断信息
                                System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] 检测到 {processFailedKind}，仅记录诊断信息");
                                break;
                            
                            default:
                                // 未知类型：记录日志
                                System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] 检测到未知的 ProcessFailedKind: {processFailedKind}");
                                break;
                        }
                    }
                    catch (Exception recoveryEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] ⚠️ 恢复策略执行失败: {recoveryEx.Message}");
                        Features.UnifiedCalls.Logging.LogService.Error(
                            "WebView2.ProcessFailed.Recovery",
                            "恢复策略执行过程中发生异常",
                            recoveryEx);
                        
                        // ⭐ 任务 3.5：异常情况下也要重置 guard
                        _isRecoveringWebView = false;
                        System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ 恢复标志已重置（异常恢复）");
                    }
                });
            }
            catch (Exception ex)
            {
                // ✅ 捕获处理器内部异常，避免二次崩溃（需求：2.3.2）
                System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] ⚠️ 处理器内部异常: {ex.Message}");
                
                // 记录处理器自身的异常
                try
                {
                    Features.UnifiedCalls.Logging.LogService.Error(
                        "WebView2.ProcessFailed",
                        "ProcessFailed 处理器内部发生异常（已捕获，避免二次崩溃）",
                        ex);
                }
                catch
                {
                    // 如果日志服务本身失败，也不抛出异常
                }
            }
        }

        private async Task HideLoadingProgressBarSmoothlyAsync()
        {
            if (LoadingProgressBar == null) return;

            await DispatcherQueue.EnqueueAsync(() =>
            {
                if (LoadingProgressBar == null) return;

                // 不停止 IsIndeterminate，让动画继续运行
                // 使用淡出动画隐藏，这样条纹会在淡出过程中继续滚动
                var fadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var storyboard = new Storyboard();
                storyboard.Children.Add(fadeOut);
                Storyboard.SetTarget(fadeOut, LoadingProgressBar);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");

                storyboard.Completed += (s, e) =>
                {
                    if (LoadingProgressBar != null)
                    {
                        LoadingProgressBar.Visibility = Visibility.Collapsed;
                        LoadingProgressBar.Opacity = 1.0; // 重置透明度
                        LoadingProgressBar.IsIndeterminate = false; // 停止动画以节省资源
                    }
                };

                storyboard.Begin();
            });
        }

        private static string GetWebViewLanguage()
        {
            return CultureInfo.CurrentUICulture.Name;
        }

        private string BuildBrowserArguments()
        {
            var args = new List<string>
            {
                "--enable-smooth-scrolling",
                "--enable-zero-copy",
                "--disable-features=msExperimentalScrolling"
            };

            // 🚀 启动速度优化（零内存成本）
            args.Add("--dns-prefetch-disable=false");  // 启用 DNS 预解析
            args.Add("--enable-tcp-fast-open");        // 启用 TCP Fast Open

            // 🎨 消除白闪（无论是否快速启动模式都启用）
            args.Add("--disable-backgrounding-occluded-windows");  // 禁用窗口遮挡时的背景化
            args.Add("--disable-renderer-backgrounding");          // 禁用渲染器后台化
            
            // 🎯 进程模型优化
            if (ExperimentalSettings.SingleProcessMode)
            {
                // 单进程模式：将所有服务合并到主进程
                args.Add("--single-process");  // 完全单进程（最激进）
            }
            else
            {
                // 多进程模式：优化辅助进程
                args.Add("--in-process-gpu");              // GPU 进程合并到主进程
                args.Add("--disable-gpu-process-crash-limit");  // 禁用 GPU 进程崩溃限制
                
                // 将 Network Service 和 Storage Service 合并到主进程
                args.Add("--enable-features=NetworkServiceInProcess");
            }

            // 构建 enable-features 列表
            var enableFeatures = new List<string>
            {
                "msEdgeFluentOverlayScrollbar"  // 细滚动条
            };

            // GPU 优化设置
            if (ExperimentalSettings.EnableHardwareAcceleration)
            {
                args.Add("--enable-accelerated-2d-canvas");
                args.Add("--enable-gpu-rasterization");
            }
            else
            {
                // 完全禁用 GPU 进程
                args.Add("--disable-gpu");
                args.Add("--disable-gpu-compositing");
                args.Add("--disable-accelerated-2d-canvas");
            }

            if (ExperimentalSettings.EnableHardwareOverlays)
            {
                args.Add("--enable-hardware-overlays");
            }

            if (ExperimentalSettings.EnableHardwareVideoDecoder)
            {
                enableFeatures.Add("VaapiVideoDecoder");
                args.Add("--enable-accelerated-video-decode");
            }

            if (ExperimentalSettings.DisableSoftwareRasterizer)
            {
                args.Add("--disable-software-rasterizer");
            }

            // 应用性能优化设置
            if (ExperimentalSettings.DisableBackgroundNetwork)
            {
                args.Add("--disable-background-networking");
                args.Add("--disable-sync");
                // ❌ 移除 --disable-preconnect，它严重影响首次加载速度
                args.Add("--no-pings");
            }
            else
            {
                // ✅ 显式启用预连接优化
                args.Add("--enable-preconnect");
            }

            if (ExperimentalSettings.DisableExtensions)
            {
                args.Add("--disable-extensions");
            }

            if (ExperimentalSettings.DisablePlugins)
            {
                args.Add("--disable-plugins");
            }

            // 磁盘缓存大小限制
            int cacheSizeMB = ExperimentalSettings.DiskCacheSize;
            int cacheSizeBytes = cacheSizeMB * 1024 * 1024;
            args.Add($"--disk-cache-size={cacheSizeBytes}");
            args.Add($"--media-cache-size={cacheSizeBytes}");

            // 快速启动模式：减少启动时的检查和初始化
            if (ExperimentalSettings.FastStartupMode)
            {
                args.Add("--disable-breakpad");              // 禁用崩溃报告
                args.Add("--disable-component-update");      // 禁用组件更新检查
                args.Add("--disable-domain-reliability");    // 禁用域名可靠性监控
                args.Add("--disable-background-timer-throttling");  // 减少后台定时器
                args.Add("--disable-features=CalculateNativeWinOcclusion");  // 禁用窗口遮挡计算
            }

            // 合并所有 enable-features
            if (enableFeatures.Count > 0)
            {
                args.Add($"--enable-features={string.Join(",", enableFeatures)}");
            }

            return string.Join(" ", args);
        }

        private void ApplyMemoryModeSettings()
        {
            if (WebView?.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                var memoryMode = ExperimentalSettings.MemoryMode;
                WebView.CoreWebView2.MemoryUsageTargetLevel = memoryMode == WebViewMemoryMode.Low
                    ? CoreWebView2MemoryUsageTargetLevel.Low
                    : CoreWebView2MemoryUsageTargetLevel.Normal;

                System.Diagnostics.Debug.WriteLine($"[ApplyMemoryModeSettings] 内存模式设置为: {memoryMode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApplyMemoryModeSettings] 设置内存模式失败: {ex.Message}");
            }
        }

        private async Task ClearBrowsingDataAsync()
        {
            if (WebView?.CoreWebView2?.Profile == null)
            {
                return;
            }

            try
            {
                await WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(
                    CoreWebView2BrowsingDataKinds.DiskCache |
                    CoreWebView2BrowsingDataKinds.DownloadHistory
                );
                System.Diagnostics.Debug.WriteLine($"[ClearBrowsingDataAsync] 缓存已清理");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ClearBrowsingDataAsync] 清理缓存失败: {ex.Message}");
            }
        }

        private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
        {
            if (WebView?.CoreWebView2 is null)
            {
                return;
            }

            string title = WebView.CoreWebView2.DocumentTitle ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                if (_currentShortcut is not null && !string.IsNullOrWhiteSpace(_currentShortcut.Name))
                {
                    if (_topBarTitle != null)
                    {
                        _topBarTitle.Text = _currentShortcut.Name;
                    }
                }

                return;
            }

            if (_topBarTitle != null)
            {
                _topBarTitle.Text = title;
            }
        }

        /// <summary>
        /// ⭐ 任务 6.3：CoreWebView2_WebMessageReceived 事件入口（委托到异步实现）
        /// </summary>
        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            AsyncSafety.Run(
                async () => await CoreWebView2WebMessageReceivedAsync(sender, e),
                "WebBrowserPage",
                "WebMessageReceived");
        }

        /// <summary>
        /// ⭐ 任务 6.3：CoreWebView2_WebMessageReceived 异步实现
        /// </summary>
        private async Task CoreWebView2WebMessageReceivedAsync(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
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
            var tinted = Windows.UI.Color.FromArgb(byte.MaxValue, sampledColor.R, sampledColor.G, sampledColor.B);
            SolidColorBrush background = isTop ? _topBarBackgroundBrush : _bottomBarBackgroundBrush;
            SolidColorBrush foreground = isTop ? _topBarForegroundBrush : _bottomBarForegroundBrush;

            // 防闪烁逻辑
            if (!_hasReceivedFirstTint)
            {
                bool isCurrentlyInitial = background.Color.A <= 1 && 
                    background.Color.R == 0 && background.Color.G == 0 && background.Color.B == 0;
                
                bool isPureWhite = sampledColor.R == 255 && sampledColor.G == 255 && sampledColor.B == 255;
                
                if (isCurrentlyInitial && isPureWhite)
                {
                    return;
                }
                
                _hasReceivedFirstTint = true;
            }

            AnimateColorChange(background, tinted);
            
            var contrastColor = GetContrastingForeground(sampledColor);
            AnimateColorChange(foreground, contrastColor);

            if (isTop)
            {
                // 更新次要前景色
                var secondaryColor = Windows.UI.Color.FromArgb(
                    (byte)(contrastColor.A * 0.7),
                    contrastColor.R,
                    contrastColor.G,
                    contrastColor.B
                );
                AnimateColorChange(_topBarSecondaryForegroundBrush, secondaryColor);
                
                // 更新顶部栏UI元素的颜色
                if (_topBarTitle != null)
                {
                    _topBarTitle.Foreground = _topBarForegroundBrush;
                }
                if (_topBarIconFallback != null)
                {
                    _topBarIconFallback.Foreground = _topBarSecondaryForegroundBrush;
                }
                if (_unpinButton?.Content is FontIcon unpinIcon)
                {
                    unpinIcon.Foreground = _topBarForegroundBrush;
                }
                TopAppBarService.SetForeground(_topBarForegroundBrush);
            }
            else
            {
                // ✅ 底部栏按钮颜色 - 使用 Material Design 最佳实践
                double luminance = CalculateLuminance(sampledColor);
                bool isDarkBackground = luminance < LuminanceThreshold;
                
                // Hover: 在前景色上叠加 8% 的白色/黑色（Material Design 规范）
                var hoverColor = CreateStateOverlayColor(
                    contrastColor, 
                    isDarkBackground ? ButtonHoverOverlayStrength : -ButtonHoverOverlayStrength
                );
                AnimateColorChange(_bottomBarHoverForegroundBrush, hoverColor);
                
                // Disabled: 38% 透明度（WCAG 豁免，禁用组件无对比度要求）
                var disabledColor = Windows.UI.Color.FromArgb(
                    (byte)(contrastColor.A * ButtonDisabledOpacity),
                    contrastColor.R,
                    contrastColor.G,
                    contrastColor.B
                );
                AnimateColorChange(_bottomBarDisabledForegroundBrush, disabledColor);
                
                // ⚠️ Reactor 组件会自动使用更新后的 Brush，无需手动更新资源
            }
        }

        private static void RestoreSharedTopAppBarBackground()
        {
            TopAppBarService.ResetBackground();
            TopAppBarService.ResetForeground();
            TopAppBarService.ResetChromeVisibility();
        }

        /// <summary>
        /// 创建状态叠加层颜色（Material Design 最佳实践）
        /// </summary>
        /// <param name="baseColor">基础颜色</param>
        /// <param name="overlayStrength">叠加强度（正数=变亮，负数=变暗）</param>
        private static Windows.UI.Color CreateStateOverlayColor(Windows.UI.Color baseColor, double overlayStrength)
        {
            if (overlayStrength > 0)
            {
                // 叠加白色（变亮）
                return Windows.UI.Color.FromArgb(
                    baseColor.A,
                    (byte)Math.Min(255, baseColor.R + (255 - baseColor.R) * overlayStrength),
                    (byte)Math.Min(255, baseColor.G + (255 - baseColor.G) * overlayStrength),
                    (byte)Math.Min(255, baseColor.B + (255 - baseColor.B) * overlayStrength)
                );
            }
            else
            {
                // 叠加黑色（变暗）
                overlayStrength = -overlayStrength;
                return Windows.UI.Color.FromArgb(
                    baseColor.A,
                    (byte)Math.Max(0, baseColor.R * (1 - overlayStrength)),
                    (byte)Math.Max(0, baseColor.G * (1 - overlayStrength)),
                    (byte)Math.Max(0, baseColor.B * (1 - overlayStrength))
                );
            }
        }

        // ⚠️ 旧方法已删除：UpdateButtonResources
        // 现在使用 Reactor 组件管理按钮状态

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

            // ✅ 修复：首次设置颜色时，先设置目标色，再从透明淡入（避免黑色闪现）
            if (brush.Color == Colors.Transparent)
            {
                // 先直接设置为目标颜色（但保持透明）
                brush.Color = Windows.UI.Color.FromArgb(0, targetColor.R, targetColor.G, targetColor.B);
                
                // 然后用透明度动画淡入
                var fadeInAnimation = new ColorAnimation
                {
                    From = Windows.UI.Color.FromArgb(0, targetColor.R, targetColor.G, targetColor.B),
                    To = targetColor,
                    Duration = new Duration(TimeSpan.FromMilliseconds(ColorTransitionDurationMs)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var storyboard = new Storyboard();
                storyboard.Children.Add(fadeInAnimation);
                Storyboard.SetTarget(fadeInAnimation, brush);
                Storyboard.SetTargetProperty(fadeInAnimation, "Color");
                
                storyboard.Begin();
                return;
            }

            // 后续颜色变化：正常的颜色过渡动画
            var animation = new ColorAnimation
            {
                To = targetColor,
                Duration = new Duration(TimeSpan.FromMilliseconds(ColorTransitionDurationMs)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var storyboard2 = new Storyboard();
            storyboard2.Children.Add(animation);
            Storyboard.SetTarget(animation, brush);
            Storyboard.SetTargetProperty(animation, "Color");
            
            storyboard2.Begin();
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

                if (_topBarIcon != null)
                {
                    _topBarIcon.Source = bitmap;
                    _topBarIcon.Visibility = Visibility.Visible;
                }
                if (_topBarIconFallback != null)
                {
                    _topBarIconFallback.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                ShowFallbackIcon();
            }
        }

        private void ShowFallbackIcon()
        {
            if (_topBarIcon != null)
            {
                _topBarIcon.Source = null;
                _topBarIcon.Visibility = Visibility.Collapsed;
            }
            if (_topBarIconFallback != null)
            {
                _topBarIconFallback.Visibility = Visibility.Visible;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebView != null && WebView.CanGoBack)
            {
                WebView.GoBack();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 关闭当前页面
            if (_currentShortcut != null)
            {
                System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 用户点击关闭按钮: {_currentShortcut.Id}");
                
                // 触发关闭事件，通知导航栏和内容区域
                PageCloseRequested?.Invoke(this, _currentShortcut.Id);
            }
        }

        // 页面关闭请求事件
        public event EventHandler<string>? PageCloseRequested;

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebView != null && WebView.CanGoForward)
            {
                WebView.GoForward();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            TryReloadWebView();
        }

        private async void TryReloadWebView()
        {
            try
            {
                // 防抖检查：如果正在重载或距离上次重载时间太短，则忽略
                var now = DateTime.Now;
                var timeSinceLastReload = (now - _lastReloadTime).TotalMilliseconds;
                
                if (_isReloading)
                {
                    System.Diagnostics.Debug.WriteLine("[TryReloadWebView] 正在重载中，忽略本次请求");
                    return;
                }
                
                if (timeSinceLastReload < ReloadDebounceMs)
                {
                    System.Diagnostics.Debug.WriteLine($"[TryReloadWebView] 距离上次重载时间太短 ({timeSinceLastReload:F0}ms < {ReloadDebounceMs}ms)，忽略本次请求");
                    return;
                }
                
                _isReloading = true;
                _lastReloadTime = now;
                
                System.Diagnostics.Debug.WriteLine("[TryReloadWebView] 开始重载流程");
                System.Diagnostics.Debug.WriteLine($"[TryReloadWebView] WebView 是否为 null: {WebView == null}");
                System.Diagnostics.Debug.WriteLine($"[TryReloadWebView] _isWebViewReady: {_isWebViewReady}");
                
                // 检查 WebView 是否存在
                if (WebView == null)
                {
                    System.Diagnostics.Debug.WriteLine("[TryReloadWebView] WebView 为 null");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[TryReloadWebView] CoreWebView2 是否为 null: {WebView.CoreWebView2 == null}");
                
                // 检查 CoreWebView2 是否已初始化
                if (WebView.CoreWebView2 == null)
                {
                    System.Diagnostics.Debug.WriteLine("[TryReloadWebView] CoreWebView2 未初始化，尝试重新初始化");
                    
                    // 尝试重新初始化 WebView
                    _isWebViewReady = false;
                    await EnsureWebViewInitializedAsync();
                    
                    System.Diagnostics.Debug.WriteLine($"[TryReloadWebView] 初始化完成，_isWebViewReady: {_isWebViewReady}");
                    System.Diagnostics.Debug.WriteLine($"[TryReloadWebView] CoreWebView2 是否为 null: {WebView?.CoreWebView2 == null}");
                    
                    // 如果初始化成功且有待导航的 URI，则导航
                    if (_isWebViewReady && WebView?.Source != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TryReloadWebView] 重新导航到: {WebView.Source}");
                        var currentSource = WebView.Source;
                        WebView.Source = null;
                        await Task.Delay(50);
                        WebView.Source = currentSource;
                    }
                    else if (_pendingNavigationUri != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TryReloadWebView] 导航到待处理的 URI: {_pendingNavigationUri}");
                        TryNavigatePendingUri();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[TryReloadWebView] 初始化后无可用的 URI");
                    }
                    return;
                }

                // 正常重载
                System.Diagnostics.Debug.WriteLine("[TryReloadWebView] 执行正常重载");
                WebView.Reload();
                System.Diagnostics.Debug.WriteLine("[TryReloadWebView] 重载命令已发送");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TryReloadWebView] 重载失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[TryReloadWebView] 堆栈: {ex.StackTrace}");
                
                // 如果重载失败，尝试重新导航到当前 URL
                try
                {
                    if (WebView?.Source != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TryReloadWebView] 尝试重新导航到: {WebView.Source}");
                        var currentSource = WebView.Source;
                        
                        // 短暂延迟后重新初始化
                        await Task.Delay(100);
                        
                        // 重新初始化 WebView
                        _isWebViewReady = false;
                        await EnsureWebViewInitializedAsync();
                        
                        // 导航到之前的 URL
                        if (_isWebViewReady && WebView != null)
                        {
                            WebView.Source = currentSource;
                        }
                    }
                }
                catch (Exception innerEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[TryReloadWebView] 重新导航也失败: {innerEx.Message}");
                }
            }
            finally
            {
                // 重载完成，重置标志
                _isReloading = false;
                System.Diagnostics.Debug.WriteLine("[TryReloadWebView] 重载流程结束");
            }
        }
        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            Uri? uri = WebView?.Source;
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
            Uri? uri = WebView?.Source;
            if (uri is null)
            {
                return;
            }

            var dialog = CreateExternalOpenDialog(uri);
            var result = await InAppDialogService.ShowAsync(dialog, this);
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
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
            CopyMenuItem.IsEnabled = true;
            CopyLinkMenuItem.IsEnabled = !string.IsNullOrEmpty(_contextMenuLinkUrl);
            {
                CopyMenuItem.IsEnabled = true;
                CopyLinkMenuItem.IsEnabled = !string.IsNullOrEmpty(_contextMenuLinkUrl);
            }
            
            // 显示自定义菜单
            var flyout = WebView?.ContextFlyout as MenuFlyout;
            if (flyout != null)
            {
                flyout.ShowAt(WebView, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
                {
                    Position = new Windows.Foundation.Point(e.Location.X, e.Location.Y)
                });
            }
        }

        private void BackMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (WebView != null && WebView.CanGoBack)
            {
                WebView.GoBack();
            }
        }

        private void ForwardMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (WebView != null && WebView.CanGoForward)
            {
                WebView.GoForward();
            }
        }

        private void RefreshMenuItem_Click(object sender, RoutedEventArgs e)
        {
            TryReloadWebView();
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
                if (WebView?.CoreWebView2 != null)
                {
                    string script = "window.getSelection().toString()";
                    string result = await WebView.CoreWebView2.ExecuteScriptAsync(script);
                    
                    // ExecuteScriptAsync 返回 JSON 字符串字面量，用 JsonDocument 避免 AOT 下的反射序列化路径。
                    string? selectedText = null;
                    if (result.Length >= 2 && result.StartsWith("\"") && result.EndsWith("\""))
                    {
                        using var document = JsonDocument.Parse(result);
                        selectedText = document.RootElement.GetString();
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
            Uri? uri = WebView?.Source;
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
            Uri? uri = WebView?.Source;
            if (uri is null)
            {
                return;
            }

            var dialog = CreateExternalOpenDialog(uri);
            var result = await InAppDialogService.ShowAsync(dialog, this);
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private static UnifiedInAppDialog CreateExternalOpenDialog(Uri uri)
        {
            var dialog = new UnifiedInAppDialog();
            dialog.Configure(
                DockedTools.Features.Localization.LocalizationHelper.GetString("InAppDialog_OpenExternal_Title"),
                new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = DockedTools.Features.Localization.LocalizationHelper.GetString("InAppDialog_OpenExternal_Content"),
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 14
                        },
                        new TextBlock
                        {
                            Text = uri.AbsoluteUri,
                            TextWrapping = TextWrapping.WrapWholeWords,
                            IsTextSelectionEnabled = true,
                            Opacity = 0.72,
                            FontSize = 12
                        }
                    }
                },
                DockedTools.Features.Localization.LocalizationHelper.GetString("InAppDialog_OpenExternal_OpenButton"),
                DockedTools.Features.Localization.LocalizationHelper.GetString("InAppDialog_OpenExternal_CancelButton"),
                defaultButton: ContentDialogButton.Primary);
            return dialog;
        }

        // ==================== 右键菜单相关方法结束 ====================

        private void HandleDoubleClick()
        {
            try
            {
                var window = GetMainWindowInstance();
                if (window is DockedTools.MainWindow mainWindow)
                {
                    mainWindow.ToggleWindowState();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HandleDoubleClick] 异常: {ex.Message}");
            }
        }

        private Window? GetMainWindowInstance()
        {
            try
            {
                if (Application.Current is App app)
                {
                    var window = app.MainWindow;
                    System.Diagnostics.Debug.WriteLine($"[GetMainWindowInstance] 从 App.MainWindow 获取: {window?.GetType().Name ?? "null"}");
                    if (window != null)
                    {
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

        #region 键盘映射按钮

        private void SetupRightContent()
        {
            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };

            // 1. 添加右侧映射按钮（如果启用）
            if (_currentShortcut?.RightButton.IsEnabled == true)
            {
                SetupRightMappingButton();
                if (_rightMappingButton != null)
                {
                    container.Children.Add(_rightMappingButton);
                }
            }

            // 2. 添加关闭按钮（如果未隐藏）
            if (!ExperimentalSettings.HideWebViewCloseButton)
            {
                _unpinButton = new Button
                {
                    Width = 40,
                    Height = 40,
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(4),
                    Content = new FontIcon
                    {
                        Glyph = "\uE733",
                        FontSize = 16,
                        Foreground = _topBarForegroundBrush
                    }
                };

                // 设置按钮背景样式（与返回按钮一致）
                var transparentColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTransparent"];
                _unpinButton.Background = new SolidColorBrush(transparentColor);
                _unpinButton.BackgroundSizing = BackgroundSizing.InnerBorderEdge;

                // 设置悬停和按下状态的背景色
                var resources = new ResourceDictionary();
                var secondaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorSecondary"];
                var tertiaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTertiary"];
                resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(secondaryColor);
                resources["ButtonBackgroundPressed"] = new SolidColorBrush(tertiaryColor);
                _unpinButton.Resources = resources;

                ToolTipService.SetToolTip(_unpinButton, "关闭");
                _unpinButton.Click += CloseButton_Click;
                container.Children.Add(_unpinButton);
            }

            // 设置到右侧面板
            TopAppBarService.SetRightContent(container.Children.Count > 0 ? container : null);
        }

        private void SetupLeftMappingButton()
        {
            if (_currentShortcut == null)
            {
                return;
            }

            var config = _currentShortcut.LeftButton;
            
            // 如果未启用，不显示按钮
            if (!config.IsEnabled)
            {
                TopAppBarService.SetLeftContent(null);
                _leftMappingButton = null;
                return;
            }

            // 根据图标类型创建图标
            UIElement icon;
            if (config.IconType == "Animated")
            {
                icon = CreateAnimatedIcon(config.AnimatedIconType);
            }
            else
            {
                var fontIcon = new FontIcon
                {
                    Glyph = config.StaticIconGlyph,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    FontSize = 16,
                    Foreground = _topBarForegroundBrush
                };
                
                // 调试日志
                System.Diagnostics.Debug.WriteLine($"[CreateLeftButton] Glyph='{config.StaticIconGlyph}' (长度={config.StaticIconGlyph?.Length}), FontFamily={fontIcon.FontFamily.Source}");
                
                icon = fontIcon;
            }
            
            _leftMappingButton = new Button
            {
                Width = 40,
                Height = 40,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Content = icon
            };

            // 设置按钮背景样式（与返回按钮一致）
            var transparentColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTransparent"];
            _leftMappingButton.Background = new SolidColorBrush(transparentColor);
            _leftMappingButton.BackgroundSizing = BackgroundSizing.InnerBorderEdge;

            // 设置悬停和按下状态的背景色
            var resources = new ResourceDictionary();
            var secondaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorSecondary"];
            var tertiaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTertiary"];
            resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(secondaryColor);
            resources["ButtonBackgroundPressed"] = new SolidColorBrush(tertiaryColor);
            _leftMappingButton.Resources = resources;

            ToolTipService.SetToolTip(_leftMappingButton, config.Tooltip);
            _leftMappingButton.Click += OnLeftMappingButtonClick;

            // 创建容器
            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };

            container.Children.Add(_leftMappingButton);

            TopAppBarService.SetLeftContent(container);
        }

        private void SetupRightMappingButton()
        {
            _rightMappingButton = null;

            if (_currentShortcut == null)
            {
                return;
            }

            var config = _currentShortcut.RightButton;
            
            // 如果未启用，不创建按钮
            if (!config.IsEnabled)
            {
                return;
            }

            // 根据图标类型创建图标
            UIElement icon;
            if (config.IconType == "Animated")
            {
                icon = CreateAnimatedIcon(config.AnimatedIconType);
            }
            else
            {
                var fontIcon = new FontIcon
                {
                    Glyph = config.StaticIconGlyph,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    FontSize = 16,
                    Foreground = _topBarForegroundBrush
                };
                
                icon = fontIcon;
            }
            
            _rightMappingButton = new Button
            {
                Width = 40,
                Height = 40,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Content = icon,
                Margin = new Thickness(0, 0, 4, 0)
            };

            // 设置按钮背景样式（与返回按钮一致）
            var transparentColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTransparent"];
            _rightMappingButton.Background = new SolidColorBrush(transparentColor);
            _rightMappingButton.BackgroundSizing = BackgroundSizing.InnerBorderEdge;

            // 设置悬停和按下状态的背景色
            var resources = new ResourceDictionary();
            var secondaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorSecondary"];
            var tertiaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTertiary"];
            resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(secondaryColor);
            resources["ButtonBackgroundPressed"] = new SolidColorBrush(tertiaryColor);
            _rightMappingButton.Resources = resources;

            ToolTipService.SetToolTip(_rightMappingButton, config.Tooltip);
            _rightMappingButton.Click += OnRightMappingButtonClick;
        }

        private Microsoft.UI.Xaml.Controls.AnimatedIcon CreateAnimatedIcon(string animatedIconType)
        {
            var animatedIcon = new Microsoft.UI.Xaml.Controls.AnimatedIcon
            {
                Width = 16,
                Height = 16,
                Foreground = _topBarForegroundBrush
            };

            // 根据类型名创建对应的 Source
            animatedIcon.Source = animatedIconType switch
            {
                "AnimatedAcceptVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedAcceptVisualSource(),
                "AnimatedBackVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedBackVisualSource(),
                "AnimatedChevronDownSmallVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedChevronDownSmallVisualSource(),
                "AnimatedChevronRightDownSmallVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedChevronRightDownSmallVisualSource(),
                "AnimatedChevronUpDownSmallVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedChevronUpDownSmallVisualSource(),
                "AnimatedFindVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedFindVisualSource(),
                "AnimatedGlobalNavigationButtonVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedGlobalNavigationButtonVisualSource(),
                "AnimatedSettingsVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedSettingsVisualSource(),
                _ => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedChevronDownSmallVisualSource() // 默认
            };

            // 设置状态为 Normal
            Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Normal");

            return animatedIcon;
        }

        private async void OnLeftMappingButtonClick(object sender, RoutedEventArgs e)
        {
            if (_currentShortcut == null || WebView?.CoreWebView2 == null)
            {
                return;
            }

            var config = _currentShortcut.LeftButton;
            
            // 播放点击动画
            if (_leftMappingButton?.Content is Microsoft.UI.Xaml.Controls.AnimatedIcon animatedIcon)
            {
                Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Pressed");
                await Task.Delay(200);
                Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Normal");
            }
            
            // 发送快捷键到 WebView2
            await SendHotkeyToWebViewAsync(config.Key, config.Ctrl, config.Shift, config.Alt);

            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 左侧按钮发送快捷键: {config.GetHotkeyDisplayText()}");
        }

        private async void OnRightMappingButtonClick(object sender, RoutedEventArgs e)
        {
            if (_currentShortcut == null || WebView?.CoreWebView2 == null)
            {
                return;
            }

            var config = _currentShortcut.RightButton;
            
            // 播放点击动画
            if (_rightMappingButton?.Content is Microsoft.UI.Xaml.Controls.AnimatedIcon animatedIcon)
            {
                Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Pressed");
                await Task.Delay(200);
                Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Normal");
            }
            
            // 发送快捷键到 WebView2
            await SendHotkeyToWebViewAsync(config.Key, config.Ctrl, config.Shift, config.Alt);

            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 右侧按钮发送快捷键: {config.GetHotkeyDisplayText()}");
        }

        private async Task SendHotkeyToWebViewAsync(VirtualKey key, bool ctrl, bool shift, bool alt)
        {
            if (WebView?.CoreWebView2 == null || key == VirtualKey.None)
            {
                return;
            }

            try
            {
                // 构建修饰键字符串
                var modifiers = new System.Collections.Generic.List<string>();
                if (ctrl) modifiers.Add("ctrlKey: true");
                if (shift) modifiers.Add("shiftKey: true");
                if (alt) modifiers.Add("altKey: true");
                
                string modifiersStr = modifiers.Count > 0 ? ", " + string.Join(", ", modifiers) : "";
                
                // 使用 JavaScript 模拟键盘事件
                string script = $@"
                    (function() {{
                        const event = new KeyboardEvent('keydown', {{
                            key: '{GetKeyString(key)}',
                            code: '{GetKeyCode(key)}',
                            keyCode: {(int)key},
                            which: {(int)key},
                            bubbles: true,
                            cancelable: true{modifiersStr}
                        }});
                        document.dispatchEvent(event);
                        
                        const eventUp = new KeyboardEvent('keyup', {{
                            key: '{GetKeyString(key)}',
                            code: '{GetKeyCode(key)}',
                            keyCode: {(int)key},
                            which: {(int)key},
                            bubbles: true,
                            cancelable: true{modifiersStr}
                        }});
                        document.dispatchEvent(eventUp);
                        
                        return 'OK';
                    }})();
                ";

                await WebView.CoreWebView2.ExecuteScriptAsync(script);
                System.Diagnostics.Debug.WriteLine($"[SendHotkeyToWebViewAsync] 已发送快捷键");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SendHotkeyToWebViewAsync] 发送快捷键失败: {ex.Message}");
            }
        }

        private static string GetKeyString(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.Enter => "Enter",
                VirtualKey.Tab => "Tab",
                VirtualKey.Escape => "Escape",
                VirtualKey.Space => " ",
                VirtualKey.Back => "Backspace",
                VirtualKey.Delete => "Delete",
                VirtualKey.F5 => "F5",
                _ => key.ToString()
            };
        }

        private static string GetKeyCode(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.Enter => "Enter",
                VirtualKey.Tab => "Tab",
                VirtualKey.Escape => "Escape",
                VirtualKey.Space => "Space",
                VirtualKey.Back => "Backspace",
                VirtualKey.Delete => "Delete",
                VirtualKey.F5 => "F5",
                _ => $"Key{key}"
            };
        }

        #endregion

        /// <summary>
        /// 清理并释放 WebView 资源（公开方法，供 PageCacheManager 和 WebViewManager 调用）
        /// </summary>
        /// <param name="skipUnlink">是否跳过 Unlink 操作（LRU 淘汰时已经移除，不需要再次 Unlink）</param>
        public void DisposeWebView(bool skipUnlink = false)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            
            System.Diagnostics.Debug.WriteLine($"[DisposeWebView] 开始清理 WebView: {_currentShortcut?.Id ?? "null"}, skipUnlink: {skipUnlink}");
            
            // ⭐ 取消链接 WebView（防护：只在有 shortcut 且不跳过时调用）
            if (_currentShortcut != null && !skipUnlink)
            {
                WebViewManager.Unlink(_currentShortcut.Id);
                System.Diagnostics.Debug.WriteLine($"[DisposeWebView] 已取消链接 WebView: {_currentShortcut.Id}");
                WebViewManager.DiagnoseState();
            }
            
            Loaded -= WebBrowserPage_Loaded;
            Unloaded -= WebBrowserPage_Unloaded;
            Pages.Settings.SettingsPage.WinUIContextMenuSettingsChanged -= OnWinUIContextMenuSettingsChanged;
            Pages.Settings.SettingsPage.WebViewPerformanceSettingsChanged -= OnWebViewPerformanceSettingsChanged;
            
            // 清理 WebView 实例
            CleanupAndCloseWebView(WebView);
            
            // ⭐ 标记需要重新创建 WebView
            _needsWebViewRecreation = true;
            
            _pendingNavigationUri = null;
            // ⭐ 不清空 _currentShortcut，因为恢复时需要它来重新导航
            // _currentShortcut = null;
            _isWebViewReady = false;
            
            System.Diagnostics.Debug.WriteLine($"[DisposeWebView] 清理完成，标记需要重新创建 WebView");
        }
        
        /// <summary>
        /// 清理 WebView 实例（完全释放资源以节省内存）
        /// </summary>
        private void CleanupAndCloseWebView(Microsoft.UI.Xaml.Controls.WebView2? webView)
        {
            if (webView?.CoreWebView2 != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[CleanupAndCloseWebView] 清理并关闭 WebView 实例");
                    
                    // 移除事件订阅
                    webView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                    webView.CoreWebView2.DocumentTitleChanged -= CoreWebView2_DocumentTitleChanged;
                    webView.CoreWebView2.HistoryChanged -= CoreWebView2_HistoryChanged;
                    webView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                    webView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                    webView.CoreWebView2.ContextMenuRequested -= CoreWebView2_ContextMenuRequested;
                    
                    // ⭐ 任务 3.2：取消订阅 ProcessFailed 事件
                    webView.CoreWebView2.ProcessFailed -= CoreWebView2_ProcessFailed;

                    // 停止当前导航
                    webView.CoreWebView2.Stop();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CleanupAndCloseWebView] 清理事件失败: {ex.Message}");
                }
            }
            
            // ⭐ 取消订阅 BrowserProcessExited 事件（避免重复订阅）
            if (_webViewEnvironment != null)
            {
                try
                {
                    _webViewEnvironment.BrowserProcessExited -= CoreWebView2Environment_BrowserProcessExited;
                    System.Diagnostics.Debug.WriteLine($"[CleanupAndCloseWebView] 已取消订阅 BrowserProcessExited");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CleanupAndCloseWebView] 取消订阅 BrowserProcessExited 失败: {ex.Message}");
                }
                _webViewEnvironment = null;
            }

            if (webView != null)
            {
                try
                {
                    // ⭐ 完全关闭 WebView 以释放内存
                    webView.Close();
                    System.Diagnostics.Debug.WriteLine($"[CleanupAndCloseWebView] WebView 已关闭并释放资源");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CleanupAndCloseWebView] 关闭 WebView 失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 重新创建 WebView 控件（在 LRU 清理后恢复页面时使用）
        /// </summary>
        private void RecreateWebView()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[RecreateWebView] 开始重新创建 WebView");
                
                // 找到 WebView 的父容器（Grid，Row=1）
                if (Content is Grid rootGrid && rootGrid.Children.Count > 0)
                {
                    // 查找旧的 WebView 并移除
                    Microsoft.UI.Xaml.Controls.WebView2? oldWebView = null;
                    foreach (var child in rootGrid.Children)
                    {
                        if (child is Microsoft.UI.Xaml.Controls.WebView2 wv)
                        {
                            oldWebView = wv;
                            break;
                        }
                    }
                    
                    if (oldWebView != null)
                    {
                        rootGrid.Children.Remove(oldWebView);
                        System.Diagnostics.Debug.WriteLine("[RecreateWebView] 已移除旧的 WebView");
                    }
                    
                    // 创建新的 WebView
                    var newWebView = new Microsoft.UI.Xaml.Controls.WebView2
                    {
                        Name = "WebView",
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        DefaultBackgroundColor = Microsoft.UI.Colors.Transparent
                    };
                    
                    // 设置 Grid.Row
                    Grid.SetRow(newWebView, 1);
                    
                    // 配置右键菜单
                    bool useWinUIContextMenu = ExperimentalSettings.EnableWinUIContextMenu;
                    if (useWinUIContextMenu)
                    {
                        newWebView.ContextFlyout = WebViewContextMenu;
                    }
                    
                    // 添加到 Grid
                    rootGrid.Children.Add(newWebView);
                    
                    // 更新字段引用
                    WebView = newWebView;
                    
                    System.Diagnostics.Debug.WriteLine("[RecreateWebView] ✅ WebView 重新创建成功");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[RecreateWebView] ❌ 无法找到根 Grid");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecreateWebView] ❌ 重新创建 WebView 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 分层策略第一步：尝试从 meta[name="theme-color"] 获取主题色
        /// </summary>
        private async Task TryApplyThemeColorAsync()
        {
            if (WebView?.CoreWebView2 is null)
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

                string result = await WebView.CoreWebView2.ExecuteScriptAsync(script);
                
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
        /// 主动触发一次采样取色（用于首次加载页面）
        /// </summary>
        private async Task TriggerTintSamplingAsync()
        {
            if (WebView?.CoreWebView2 is null)
            {
                return;
            }

            try
            {
                // ✅ 方案1：直接在 C# 这边执行完整的采样逻辑（不依赖脚本状态）
                string script = @"
(function() {
    // 完整复制采样逻辑，确保立即执行
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
    
    function effectiveBg(el) {
        if (!el) return null;
        let cur = el;
        const minAlpha = 0.01;
        const maxDepth = 20;
        let depth = 0;
        
        while (cur && cur !== document && depth < maxDepth) {
            const style = getComputedStyle(cur);
            const bg = cssToRgbaArray(style.backgroundColor);
            
            if (bg && bg[3] > minAlpha) {
                return bg;
            }
            
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
        
        if (document.body) {
            const bodyBg = cssToRgbaArray(getComputedStyle(document.body).backgroundColor);
            if (bodyBg && bodyBg[3] > minAlpha) return bodyBg;
        }
        
        if (document.documentElement) {
            const htmlBg = cssToRgbaArray(getComputedStyle(document.documentElement).backgroundColor);
            if (htmlBg && htmlBg[3] > minAlpha) return htmlBg;
        }
        
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
        const a = Math.max(0, Math.min(1, rgba[3]));
        return 'rgba(' + Math.round(rgba[0]) + ',' + Math.round(rgba[1]) + ',' + Math.round(rgba[2]) + ',' + a + ')';
    }
    
    // 立即采样
    const topColor = sampleAtY(1);
    const bottomColor = sampleAtY(Math.max(1, window.innerHeight - 2));
    const top = rgbaToCss(topColor);
    const bottom = rgbaToCss(bottomColor);
    
    // 发送消息
    const msg = { 
        type: 'DockedTools_tint', 
        top: top, 
        bottom: bottom, 
        title: (document.title || ''),
        isTransparent: !top || !bottom
    };
    
    try {
        window.chrome?.webview?.postMessage(JSON.stringify(msg));
        return 'sent: top=' + top + ', bottom=' + bottom;
    } catch (error) {
        return 'error: ' + error.message;
    }
})();";

                string result = await WebView.CoreWebView2.ExecuteScriptAsync(script);
                System.Diagnostics.Debug.WriteLine($"[TriggerTintSamplingAsync] 立即采样结果: {result}");
                
                // ✅ 方案2（备选）：如果脚本已经准备好，也调用一次
                await Task.Delay(50);
                await WebView.CoreWebView2.ExecuteScriptAsync(@"
                    if (window.__dockedAiTint && typeof window.__dockedAiTint.updateNow === 'function') {
                        window.__dockedAiTint.updateNow();
                    }
                ");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TriggerTintSamplingAsync] 触发失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 分层策略终极方案：截图采样（仅在页面完全透明时使用）
        /// </summary>
        private async Task TryScreenshotSamplingAsync()
        {
            if (WebView?.CoreWebView2 is null)
            {
                return;
            }

            try
            {
                using var stream = new InMemoryRandomAccessStream();
                await WebView.CoreWebView2.CapturePreviewAsync(
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
        /// ✅ Bug修复：WebView恢复后重新注入取色脚本
        /// </summary>
        private async Task ReInjectTintScriptAsync()
        {
            if (WebView?.CoreWebView2 == null)
            {
                System.Diagnostics.Debug.WriteLine("[ReInjectTintScriptAsync] WebView 未初始化");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[ReInjectTintScriptAsync] 开始重新注入取色脚本");
                
                // 延迟一小段时间确保 WebView 完全恢复
                await Task.Delay(100);
                
                // 重新注入取色脚本
                await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(Services.WebViewTintScript.GetTintScript());
                
                System.Diagnostics.Debug.WriteLine("[ReInjectTintScriptAsync] 取色脚本重新注入成功");
                
                // 重新触发一次取色
                await RefreshPageTintAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReInjectTintScriptAsync] 重新注入脚本失败: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ Bug修复：页面恢复时刷新取色
        /// </summary>
        private async Task RefreshPageTintAsync()
        {
            if (WebView?.CoreWebView2 == null)
            {
                System.Diagnostics.Debug.WriteLine("[RefreshPageTintAsync] WebView 未初始化");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[RefreshPageTintAsync] 开始刷新页面取色");
                
                // 重置取色状态以允许重新取色
                _hasReceivedFirstTint = false;
                _hasAppliedThemeColor = false;
                
                // 延迟确保页面已完全加载
                await Task.Delay(200);
                
                // 重新执行取色策略
                await TryApplyThemeColorAsync();
                
                System.Diagnostics.Debug.WriteLine("[RefreshPageTintAsync] 页面取色刷新完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RefreshPageTintAsync] 刷新取色失败: {ex.Message}");
                // 失败时使用系统强调色作为后备
                ApplySystemAccentColor();
            }
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
