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
        private Uri? _currentUri;
        private WebAppShortcut? _currentShortcut;
        private int _navigationVersion;
        private (double x, double y)? _pendingScrollRestore;
        private string? _pendingScrollRestoreUrl;

        private readonly SolidColorBrush _topBarBackgroundBrush = new(Colors.Transparent);
        private readonly SolidColorBrush _bottomBarBackgroundBrush = new(Colors.Transparent);
        private readonly SolidColorBrush _topBarForegroundBrush = new(Colors.Black);
        private readonly SolidColorBrush _bottomBarForegroundBrush = new(Colors.Black);

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

            if (_isWebViewReady && _currentUri is not null && UriEquals(_currentUri, uri))
            {
                return;
            }

            _currentUri = uri;
            _pendingNavigationUri = null;
            int version = ++_navigationVersion;
            _ = PrepareNavigationAsync(shortcut, uri, version);
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _ = SaveStateAsync();
        }

        private async void WebBrowserPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= WebBrowserPage_Loaded;
            await EnsureWebViewInitializedAsync();
            TryNavigatePendingUri();
        }

        private async Task PrepareNavigationAsync(WebAppShortcut shortcut, Uri defaultUri, int version)
        {
            Uri target = defaultUri;
            (double x, double y)? restoreScroll = null;

            WebBrowserState? state = await WebBrowserStateStore.LoadAsync(shortcut.Id);
            if (!string.IsNullOrWhiteSpace(state?.LastUrl) &&
                Uri.TryCreate(state.LastUrl, UriKind.Absolute, out Uri? savedUri))
            {
                target = savedUri;

                if (state.ScrollX is not null && state.ScrollY is not null)
                {
                    restoreScroll = ((double)state.ScrollX, (double)state.ScrollY);
                }
            }

            if (version != _navigationVersion)
            {
                return;
            }

            _pendingNavigationUri = target;
            _pendingScrollRestore = restoreScroll;
            _pendingScrollRestoreUrl = target.AbsoluteUri;

            TryNavigatePendingUri();
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
            catch
            {
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

            if (WebView.Source is not null && UriEquals(WebView.Source, _pendingNavigationUri))
            {
                _pendingNavigationUri = null;
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
    } catch (_) { }
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
            _ = RestoreScrollIfNeededAsync();
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

        private async Task RestoreScrollIfNeededAsync()
        {
            if (WebView.CoreWebView2 is null || _pendingScrollRestore is null || string.IsNullOrWhiteSpace(_pendingScrollRestoreUrl))
            {
                return;
            }

            string currentUrl = WebView.Source?.AbsoluteUri ?? string.Empty;
            if (!string.Equals(currentUrl, _pendingScrollRestoreUrl, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var (x, y) = _pendingScrollRestore.Value;
            _pendingScrollRestore = null;
            _pendingScrollRestoreUrl = null;

            try
            {
                await WebView.CoreWebView2.ExecuteScriptAsync($"try{{window.scrollTo({x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)});}}catch(e){{}}");
            }
            catch
            {
                // Ignore failures (navigation in progress / blocked).
            }
        }

        private async Task SaveStateAsync()
        {
            if (_currentShortcut is null)
            {
                return;
            }

            string? url = WebView.Source?.AbsoluteUri;
            string? title = TitleText.Text;
            (double x, double y)? scroll = await TryGetScrollAsync();

            var state = new WebBrowserState(
                LastUrl: url,
                LastTitle: title,
                ScrollX: scroll?.x,
                ScrollY: scroll?.y,
                UpdatedAt: DateTimeOffset.UtcNow);

            try
            {
                await WebBrowserStateStore.SaveAsync(_currentShortcut.Id, state);
            }
            catch
            {
                // Ignore persistence failures.
            }
        }

        private async Task<(double x, double y)?> TryGetScrollAsync()
        {
            if (WebView.CoreWebView2 is null)
            {
                return null;
            }

            try
            {
                string raw = await WebView.CoreWebView2.ExecuteScriptAsync("(() => JSON.stringify({ x: (window.scrollX || 0), y: (window.scrollY || 0) }))()");
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                using JsonDocument outer = JsonDocument.Parse(raw);
                if (outer.RootElement.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                string? innerJson = outer.RootElement.GetString();
                if (string.IsNullOrWhiteSpace(innerJson))
                {
                    return null;
                }

                using JsonDocument inner = JsonDocument.Parse(innerJson);
                JsonElement root = inner.RootElement;
                if (!root.TryGetProperty("x", out JsonElement xEl) || !root.TryGetProperty("y", out JsonElement yEl))
                {
                    return null;
                }

                double x = xEl.GetDouble();
                double y = yEl.GetDouble();
                return (x, y);
            }
            catch
            {
                return null;
            }
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

        private static bool UriEquals(Uri left, Uri right)
        {
            return Uri.Compare(
                       left,
                       right,
                       UriComponents.AbsoluteUri,
                       UriFormat.SafeUnescaped,
                       StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
