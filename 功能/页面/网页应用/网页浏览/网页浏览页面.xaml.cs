using Docked_AI.Features.Pages.WebApp.Shared;
using Docked_AI.Features.Settings;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        private const double LuminanceThreshold = 140.0;
        private const double MinOpacity = 0.01;
        private const double PercentageMax = 100.0;
        private const double ColorChannelMax = 255.0;

        private Uri? _pendingNavigationUri;
        private bool _isWebViewReady;
        private WebAppShortcut? _currentShortcut;

        private readonly SolidColorBrush _topBarBackgroundBrush = new(Colors.Transparent);
        private readonly SolidColorBrush _bottomBarBackgroundBrush = new(Colors.Transparent);
        private readonly SolidColorBrush _topBarForegroundBrush = new();
        private readonly SolidColorBrush _bottomBarForegroundBrush = new();
        private readonly SolidColorBrush _topBarSecondaryForegroundBrush = new();
        private readonly SolidColorBrush _bottomBarDisabledForegroundBrush = new();
        private bool _isDisposed;
        private bool _useRoundedWebView;
        private Microsoft.UI.Xaml.Controls.WebView2? _activeWebView;
        private bool _hasReceivedFirstTint;

        public WebBrowserPage()
        {
            InitializeComponent();

            // 根据设置决定使用哪个 WebView
            _useRoundedWebView = ExperimentalSettings.EnableRoundedWebView;
            UpdateWebViewVisibility();

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
            
            // 监听 Frame 的 SizeChanged 以同步圆角
            if (_useRoundedWebView)
            {
                this.SizeChanged += OnPageSizeChanged;
            }
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
            }
            else
            {
                // 回退：根据当前主题选择黑色或白色
                var theme = Application.Current.RequestedTheme;
                var defaultColor = theme == ApplicationTheme.Dark ? Colors.White : Colors.Black;
                _topBarForegroundBrush.Color = defaultColor;
                _bottomBarForegroundBrush.Color = defaultColor;
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
            }
            else
            {
                // 回退：使用主色的40%透明度
                var baseColor = _bottomBarForegroundBrush.Color;
                _bottomBarDisabledForegroundBrush.Color = Windows.UI.Color.FromArgb(
                    (byte)(baseColor.A * 0.4),
                    baseColor.R,
                    baseColor.G,
                    baseColor.B
                );
            }
        }

        private void SetButtonStateColors(AppBarButton button)
        {
            // 设置按钮的悬停、按下和禁用状态颜色
            button.Resources["AppBarButtonForegroundPointerOver"] = _bottomBarForegroundBrush;
            button.Resources["AppBarButtonForegroundPressed"] = _bottomBarForegroundBrush;
            button.Resources["AppBarButtonForegroundDisabled"] = _bottomBarDisabledForegroundBrush;
        }

        private void OnRoundedWebViewSettingsChanged(object? sender, EventArgs e)
        {
            // 设置改变时，需要重新加载页面才能生效
            // 这里只是更新标志，实际切换需要重新导航
            _useRoundedWebView = ExperimentalSettings.EnableRoundedWebView;
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
                    _activeWebView.CoreWebView2.Settings.IsZoomControlEnabled = true;
                    
                    _activeWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                    _activeWebView.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
                    _activeWebView.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
                    _activeWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                    _activeWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
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

            string script = @"
(() => {
  if (window.__dockedAiTint) return;
  const state = { lastTop: null, lastBottom: null, scheduled: false };
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
    let cur = el;
    const minAlpha = 0.01;
    while (cur && cur !== document) {
      const bg = cssToRgbaArray(getComputedStyle(cur).backgroundColor);
      if (bg && bg[3] > minAlpha) return bg;
      cur = cur.parentElement;
    }
    const bodyBg = cssToRgbaArray(getComputedStyle(document.body).backgroundColor);
    if (bodyBg && bodyBg[3] > minAlpha) return bodyBg;
    const htmlBg = cssToRgbaArray(getComputedStyle(document.documentElement).backgroundColor);
    if (htmlBg && htmlBg[3] > minAlpha) return htmlBg;
    return [255, 255, 255, 1];
  }
  function sampleAtY(y) {
    const minX = 1;
    const x = Math.max(minX, Math.floor(window.innerWidth / 2));
    const el = document.elementFromPoint(x, y);
    return effectiveBg(el);
  }
  function rgbaToCss(rgba) {
    const minAlpha = 0;
    const maxAlpha = 1;
    const a = Math.max(minAlpha, Math.min(maxAlpha, rgba[3]));
    return `rgba(${Math.round(rgba[0])},${Math.round(rgba[1])},${Math.round(rgba[2])},${a})`;
  }
  function post(topCss, bottomCss) {
    const msg = { type: 'docked_ai_tint', top: topCss, bottom: bottomCss, title: (document.title || '') };
    try {
      window.chrome?.webview?.postMessage(JSON.stringify(msg));
    } catch (error) {
      console.warn('Failed to post tint message to host.', error);
    }
  }
  function sendNow() {
    state.scheduled = false;
    const minY = 1;
    const top = rgbaToCss(sampleAtY(minY));
    const bottom = rgbaToCss(sampleAtY(Math.max(minY, window.innerHeight - 2)));
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
  window.__dockedAiTint = { updateNow: schedule };
  window.addEventListener('scroll', schedule, { passive: true });
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
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingProgressBar.Visibility = Visibility.Collapsed;
            UpdateNavigationButtons();
            UpdateUrlText();
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

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
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
                if (!root.TryGetProperty("type", out JsonElement typeEl) ||
                    !string.Equals(typeEl.GetString(), TintMessageType, StringComparison.Ordinal))
                {
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

            // 只在首次接收到颜色时过滤纯白色，避免加载初期的闪烁
            // 之后允许白色，因为网页本身可能就是白色背景
            if (!_hasReceivedFirstTint && sampledColor.R == 255 && sampledColor.G == 255 && sampledColor.B == 255)
            {
                // 首次加载时忽略纯白色，保持透明
                return;
            }

            // 标记已接收到第一次颜色
            _hasReceivedFirstTint = true;

            background.Color = tinted;
            var contrastColor = GetContrastingForeground(sampledColor);
            foreground.Color = contrastColor;

            // 更新次要前景色（用于URL和图标）
            if (isTop)
            {
                _topBarSecondaryForegroundBrush.Color = Windows.UI.Color.FromArgb(
                    (byte)(contrastColor.A * 0.7),
                    contrastColor.R,
                    contrastColor.G,
                    contrastColor.B
                );
            }
            else
            {
                // 更新底部栏的禁用状态颜色
                _bottomBarDisabledForegroundBrush.Color = Windows.UI.Color.FromArgb(
                    (byte)(contrastColor.A * 0.4),
                    contrastColor.R,
                    contrastColor.G,
                    contrastColor.B
                );
            }
        }

        private static Windows.UI.Color GetContrastingForeground(Windows.UI.Color background)
        {
            // Relative luminance (sRGB) approximation; good enough for choosing black/white.
            double luminance = 0.2126 * background.R + 0.7152 * background.G + 0.0722 * background.B;
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
            
            if (_useRoundedWebView)
            {
                this.SizeChanged -= OnPageSizeChanged;
            }

            if (_activeWebView?.CoreWebView2 is not null)
            {
                _activeWebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                _activeWebView.CoreWebView2.DocumentTitleChanged -= CoreWebView2_DocumentTitleChanged;
                _activeWebView.CoreWebView2.HistoryChanged -= CoreWebView2_HistoryChanged;
                _activeWebView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                _activeWebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;

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
    }
}