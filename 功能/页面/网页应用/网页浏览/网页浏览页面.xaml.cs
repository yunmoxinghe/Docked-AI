using Docked_AI.Features.Pages.WebApp.Shared;
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
        private const string PreferredWebViewLanguage = "zh-CN";
        private const byte BarBackgroundAlpha = 236;

        private Uri? _pendingNavigationUri;
        private bool _isWebViewReady;
        private WebAppShortcut? _currentShortcut;

        private readonly SolidColorBrush _topBarBackgroundBrush = new(Colors.Transparent);
        private readonly SolidColorBrush _bottomBarBackgroundBrush = new(Colors.Transparent);
        private readonly SolidColorBrush _topBarForegroundBrush = new(Colors.Black);
        private readonly SolidColorBrush _bottomBarForegroundBrush = new(Colors.Black);
        private bool _isDisposed;

        public WebBrowserPage()
        {
            InitializeComponent();

            TopBarHost.Background = _topBarBackgroundBrush;
            BottomBarHost.Background = _bottomBarBackgroundBrush;
            TitleText.Foreground = _topBarForegroundBrush;

            BackButton.Foreground = _bottomBarForegroundBrush;
            ForwardButton.Foreground = _bottomBarForegroundBrush;
            RefreshButton.Foreground = _bottomBarForegroundBrush;
            CopyUrlButton.Foreground = _bottomBarForegroundBrush;
            OpenExternalButton.Foreground = _bottomBarForegroundBrush;
            UrlText.Foreground = _bottomBarForegroundBrush;

            Loaded += WebBrowserPage_Loaded;
            Unloaded += WebBrowserPage_Unloaded;
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
        }

        private void WebBrowserPage_Unloaded(object sender, RoutedEventArgs e)
        {
            DisposeWebView();
        }

        private async Task EnsureWebViewInitializedAsync()
        {
            if (_isWebViewReady)
            {
                return;
            }

            try
            {
                CoreWebView2EnvironmentOptions options = new()
                {
                    Language = GetWebViewLanguage()
                };
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    browserExecutableFolder: null,
                    userDataFolder: null,
                    options: options);
                await WebView.EnsureCoreWebView2Async(environment);

                if (WebView.CoreWebView2 is not null)
                {
                    WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                    WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                    WebView.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
                    WebView.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
                    WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
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
            if (!_isWebViewReady || _pendingNavigationUri is null)
            {
                return;
            }

            WebView.Source = _pendingNavigationUri;
            UrlText.Text = _pendingNavigationUri.AbsoluteUri;
            _pendingNavigationUri = null;
        }

        private async Task EnsureTintScriptInstalledAsync()
        {
            if (WebView.CoreWebView2 is null)
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
    while (cur && cur !== document) {
      const bg = cssToRgbaArray(getComputedStyle(cur).backgroundColor);
      if (bg && bg[3] > 0.01) return bg;
      cur = cur.parentElement;
    }
    const bodyBg = cssToRgbaArray(getComputedStyle(document.body).backgroundColor);
    if (bodyBg && bodyBg[3] > 0.01) return bodyBg;
    const htmlBg = cssToRgbaArray(getComputedStyle(document.documentElement).backgroundColor);
    if (htmlBg && htmlBg[3] > 0.01) return htmlBg;
    return [255, 255, 255, 1];
  }
  function sampleAtY(y) {
    const x = Math.max(1, Math.floor(window.innerWidth / 2));
    const el = document.elementFromPoint(x, y);
    return effectiveBg(el);
  }
  function rgbaToCss(rgba) {
    const a = Math.max(0, Math.min(1, rgba[3]));
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
    const top = rgbaToCss(sampleAtY(1));
    const bottom = rgbaToCss(sampleAtY(Math.max(1, window.innerHeight - 2)));
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

            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            UpdateNavigationButtons();
            UpdateUrlText();
        }

        private void CoreWebView2_HistoryChanged(object? sender, object e)
        {
            UpdateNavigationButtons();
        }

        private static string GetWebViewLanguage()
        {
            string uiLanguage = CultureInfo.CurrentUICulture.Name;
            if (uiLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return uiLanguage;
            }

            return PreferredWebViewLanguage;
        }

        private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
        {
            if (WebView.CoreWebView2 is null)
            {
                return;
            }

            string title = WebView.CoreWebView2.DocumentTitle ?? string.Empty;
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
            var tinted = Windows.UI.Color.FromArgb(BarBackgroundAlpha, sampledColor.R, sampledColor.G, sampledColor.B);
            SolidColorBrush background = isTop ? _topBarBackgroundBrush : _bottomBarBackgroundBrush;
            SolidColorBrush foreground = isTop ? _topBarForegroundBrush : _bottomBarForegroundBrush;

            background.Color = tinted;
            foreground.Color = GetContrastingForeground(sampledColor);
        }

        private static Windows.UI.Color GetContrastingForeground(Windows.UI.Color background)
        {
            // Relative luminance (sRGB) approximation; good enough for choosing black/white.
            double luminance = 0.2126 * background.R + 0.7152 * background.G + 0.0722 * background.B;
            return luminance < 140 ? Colors.White : Colors.Black;
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

                color = Windows.UI.Color.FromArgb(255, r, g, b);
                return true;
            }

            if (s.StartsWith('#'))
            {
                string hex = s.Substring(1);
                if (hex.Length == 6 &&
                    byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                    byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                    byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    color = Windows.UI.Color.FromArgb(255, r, g, b);
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

                percent = Math.Max(0, Math.Min(100, percent));
                value = (byte)Math.Round(percent / 100 * 255);
                return true;
            }

            if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double raw))
            {
                return false;
            }

            raw = Math.Max(0, Math.Min(255, raw));
            value = (byte)Math.Round(raw);
            return true;
        }

        private void UpdateNavigationButtons()
        {
            BackButton.IsEnabled = WebView.CanGoBack;
            ForwardButton.IsEnabled = WebView.CanGoForward;
        }

        private void UpdateUrlText()
        {
            Uri? uri = WebView.Source;
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
            if (WebView.CanGoBack)
            {
                WebView.GoBack();
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebView.CanGoForward)
            {
                WebView.GoForward();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            WebView.Reload();
        }

        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            Uri? uri = WebView.Source;
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
            Uri? uri = WebView.Source;
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

            if (WebView.CoreWebView2 is not null)
            {
                WebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                WebView.CoreWebView2.DocumentTitleChanged -= CoreWebView2_DocumentTitleChanged;
                WebView.CoreWebView2.HistoryChanged -= CoreWebView2_HistoryChanged;
                WebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;

                try
                {
                    WebView.CoreWebView2.Stop();
                }
                catch
                {
                    // Ignore cleanup errors during page teardown.
                }
            }

            WebView.Source = null;
            WebView.Close();
            _pendingNavigationUri = null;
            _currentShortcut = null;
            _isWebViewReady = false;
        }
    }
}
