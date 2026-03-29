using Docked_AI.Features.Pages.WebApp.Shared;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace Docked_AI.Features.Pages.WebApp
{
    public sealed partial class WebAppPage : Page
    {
        private const float IconCornerRadius = 14f;
        private static readonly HttpClient HttpClient = CreateHttpClient();

        private CancellationTokenSource? _urlLookupCts;
        private CompositionRoundedRectangleGeometry? _iconClipGeometry;
        private bool _hasCustomIcon;
        private bool _isProgrammaticNameUpdate;
        private bool _isAppNameManuallyEdited;
        private string _lastAutoAppName = string.Empty;
        private byte[]? _currentIconBytes;

        public WebAppPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            System.Diagnostics.Debug.WriteLine($"WebAppPage.OnNavigatedTo called with parameter: {e.Parameter}");

            if (e.Parameter is string url && !string.IsNullOrWhiteSpace(url))
            {
                System.Diagnostics.Debug.WriteLine($"WebAppPage: setting URL to: {url}");
                WebsiteUrlTextBox.Text = url;
            }
        }

        private async void WebsiteUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            await RefreshWebsiteMetadataAsync(withDelay: true);
        }

        private void AppNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isProgrammaticNameUpdate)
            {
                return;
            }

            string current = AppNameTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(current))
            {
                _isAppNameManuallyEdited = false;
                return;
            }

            _isAppNameManuallyEdited = !string.Equals(current, _lastAutoAppName, StringComparison.Ordinal);
        }

        private async void RestoreAutoIconButton_Click(object sender, RoutedEventArgs e)
        {
            _hasCustomIcon = false;
            LookupStatusText.Text = "已恢复自动图标";
            await RefreshWebsiteMetadataAsync(withDelay: false);
        }

        private async Task RefreshWebsiteMetadataAsync(bool withDelay)
        {
            _urlLookupCts?.Cancel();
            _urlLookupCts?.Dispose();

            string rawInput = WebsiteUrlTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                if (!_hasCustomIcon)
                {
                    ShowFallbackIcon();
                }

                LookupStatusText.Text = string.Empty;
                return;
            }

            if (!TryNormalizeWebsiteUrl(rawInput, out Uri websiteUri))
            {
                if (!_hasCustomIcon)
                {
                    ShowFallbackIcon();
                }

                LookupStatusText.Text = "网址格式无效";
                return;
            }

            SetAppNameIfAutoMode(websiteUri.Host);

            var cts = new CancellationTokenSource();
            _urlLookupCts = cts;

            try
            {
                LookupStatusText.Text = "正在获取网站信息...";
                if (withDelay)
                {
                    await Task.Delay(450, cts.Token);
                }

                WebsiteMetadata metadata = await FetchWebsiteMetadataAsync(websiteUri, cts.Token);

                if (!ReferenceEquals(_urlLookupCts, cts))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(metadata.Title))
                {
                    SetAppNameIfAutoMode(metadata.Title);
                }

                if (metadata.IconBytes is { Length: > 0 })
                {
                    if (!_hasCustomIcon)
                    {
                        await ShowWebsiteIconAsync(metadata.IconBytes);
                    }

                    LookupStatusText.Text = _hasCustomIcon
                        ? "已获取名称（使用手动图标）"
                        : "已获取名称和图标";
                }
                else
                {
                    if (!_hasCustomIcon)
                    {
                        ShowFallbackIcon();
                    }

                    LookupStatusText.Text = string.IsNullOrWhiteSpace(metadata.Error)
                        ? "已获取名称，未找到图标"
                        : $"部分获取成功：{metadata.Error}";
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!_hasCustomIcon)
                {
                    ShowFallbackIcon();
                }

                LookupStatusText.Text = "获取失败：" + ex.Message;
            }
            finally
            {
                if (ReferenceEquals(_urlLookupCts, cts))
                {
                    _urlLookupCts = null;
                }

                cts.Dispose();
            }
        }

        private async void ChooseIconButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".webp");
                picker.FileTypeFilter.Add(".bmp");
                picker.FileTypeFilter.Add(".ico");

                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    LookupStatusText.Text = "无法打开文件选择器";
                    return;
                }

                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                StorageFile? file = await picker.PickSingleFileAsync();
                if (file is null)
                {
                    return;
                }

                IBuffer buffer = await FileIO.ReadBufferAsync(file);
                byte[] bytes = buffer.ToArray();
                if (bytes.Length == 0)
                {
                    LookupStatusText.Text = "图标文件为空";
                    return;
                }

                _hasCustomIcon = true;
                await ShowWebsiteIconAsync(bytes);
                LookupStatusText.Text = "已替换图标";
            }
            catch (Exception ex)
            {
                LookupStatusText.Text = "替换图标失败：" + ex.Message;
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            string name = AppNameTextBox.Text?.Trim() ?? string.Empty;
            string rawUrl = WebsiteUrlTextBox.Text?.Trim() ?? string.Empty;

            if (!TryNormalizeWebsiteUrl(rawUrl, out Uri websiteUri))
            {
                LookupStatusText.Text = "网址格式无效";
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = websiteUri.Host;
            }

            var shortcut = new WebAppShortcut(
                Guid.NewGuid().ToString("N"),
                name,
                websiteUri.AbsoluteUri,
                _currentIconBytes);

            WebAppEventBus.PublishShortcutCreated(shortcut);
            LookupStatusText.Text = "已创建网页入口";
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(12)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
            return client;
        }

        private static bool TryNormalizeWebsiteUrl(string rawInput, out Uri uri)
        {
            uri = null!;
            string candidate = rawInput;
            if (!candidate.Contains("://", StringComparison.Ordinal))
            {
                candidate = "https://" + candidate;
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? parsed))
            {
                return false;
            }

            if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(parsed.Host))
            {
                return false;
            }

            uri = parsed;
            return true;
        }

        private void SetAppNameIfAutoMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string current = AppNameTextBox.Text?.Trim() ?? string.Empty;
            if (_isAppNameManuallyEdited && !string.Equals(current, _lastAutoAppName, StringComparison.Ordinal))
            {
                return;
            }

            _isProgrammaticNameUpdate = true;
            try
            {
                AppNameTextBox.Text = value;
            }
            finally
            {
                _isProgrammaticNameUpdate = false;
            }

            _lastAutoAppName = value.Trim();
            _isAppNameManuallyEdited = false;
        }

        private static async Task<WebsiteMetadata> FetchWebsiteMetadataAsync(Uri websiteUri, CancellationToken cancellationToken)
        {
            string html = string.Empty;
            string title = string.Empty;
            string? error = null;

            try
            {
                using HttpResponseMessage pageResponse = await HttpClient.GetAsync(websiteUri, cancellationToken);
                pageResponse.EnsureSuccessStatusCode();
                html = await pageResponse.Content.ReadAsStringAsync(cancellationToken);
                title = ParseTitle(html);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = websiteUri.Host;
            }

            byte[]? iconBytes = await TryDownloadBestIconAsync(websiteUri, html, cancellationToken);
            return new WebsiteMetadata(title, iconBytes, error);
        }

        private static string ParseTitle(string html)
        {
            Match titleMatch = Regex.Match(html, "<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!titleMatch.Success)
            {
                return string.Empty;
            }

            string decoded = WebUtility.HtmlDecode(titleMatch.Groups[1].Value);
            return Regex.Replace(decoded, "\\s+", " ").Trim();
        }

        private static async Task<byte[]?> TryDownloadBestIconAsync(Uri websiteUri, string html, CancellationToken cancellationToken)
        {
            List<Uri> candidates = ParseIconCandidates(websiteUri, html);

            Uri faviconUri = new Uri(websiteUri.GetLeftPart(UriPartial.Authority) + "/favicon.ico");
            if (!candidates.Contains(faviconUri))
            {
                candidates.Add(faviconUri);
            }

            foreach (Uri candidate in candidates)
            {
                try
                {
                    using HttpResponseMessage response = await HttpClient.GetAsync(candidate, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    string? contentType = response.Content.Headers.ContentType?.MediaType;
                    if (!string.IsNullOrWhiteSpace(contentType))
                    {
                        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        if (contentType.Contains("svg", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    if (bytes.Length == 0 || bytes.Length > 4 * 1024 * 1024)
                    {
                        continue;
                    }

                    if (!await CanDecodeBitmapAsync(bytes))
                    {
                        continue;
                    }

                    return bytes;
                }
                catch
                {
                }
            }

            return null;
        }

        private static async Task<bool> CanDecodeBitmapAsync(byte[] bytes)
        {
            try
            {
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(bytes.AsBuffer());
                stream.Seek(0);
                _ = await BitmapDecoder.CreateAsync(stream);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static List<Uri> ParseIconCandidates(Uri websiteUri, string html)
        {
            var entries = new List<(Uri Uri, int Score)>();
            if (string.IsNullOrWhiteSpace(html))
            {
                return new List<Uri>();
            }

            MatchCollection linkMatches = Regex.Matches(html, "<link\\b[^>]*>", RegexOptions.IgnoreCase);
            foreach (Match linkMatch in linkMatches)
            {
                string tag = linkMatch.Value;
                string rel = GetHtmlAttribute(tag, "rel");
                string href = GetHtmlAttribute(tag, "href");
                if (string.IsNullOrWhiteSpace(rel) || string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                if (!rel.Contains("icon", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!Uri.TryCreate(websiteUri, WebUtility.HtmlDecode(href), out Uri? iconUri))
                {
                    continue;
                }

                int sizeScore = ParseLargestIconSize(GetHtmlAttribute(tag, "sizes"));
                int relScore = rel.Contains("apple-touch-icon", StringComparison.OrdinalIgnoreCase) ? 20000 : 10000;
                entries.Add((iconUri, relScore + sizeScore));
            }

            return entries
                .OrderByDescending(e => e.Score)
                .Select(e => e.Uri)
                .Distinct()
                .ToList();
        }

        private static string GetHtmlAttribute(string tag, string attributeName)
        {
            string pattern = attributeName + "\\s*=\\s*(['\"])(.*?)\\1";
            Match match = Regex.Match(tag, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? match.Groups[2].Value.Trim() : string.Empty;
        }

        private static int ParseLargestIconSize(string sizesValue)
        {
            if (string.IsNullOrWhiteSpace(sizesValue))
            {
                return 0;
            }

            int best = 0;
            MatchCollection matches = Regex.Matches(sizesValue, "(\\d+)x(\\d+)", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out int width) &&
                    int.TryParse(match.Groups[2].Value, out int height))
                {
                    best = Math.Max(best, width * height);
                }
            }

            return best;
        }

        private async Task ShowWebsiteIconAsync(byte[] iconBytes)
        {
            _currentIconBytes = iconBytes.ToArray();
            bool shouldRound = await ShouldRoundIconCornersAsync(iconBytes);

            var bitmap = new BitmapImage();
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(iconBytes.AsBuffer());
            stream.Seek(0);
            await bitmap.SetSourceAsync(stream);

            SiteIconImage.Source = bitmap;
            SiteIconImage.Visibility = Visibility.Visible;
            SiteIconFallback.Visibility = Visibility.Collapsed;
            ApplyIconClip(shouldRound);
        }

        private static async Task<bool> ShouldRoundIconCornersAsync(byte[] iconBytes)
        {
            try
            {
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(iconBytes.AsBuffer());
                stream.Seek(0);

                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Straight,
                    new BitmapTransform(),
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                byte[] pixels = pixelData.DetachPixelData();
                for (int i = 3; i < pixels.Length; i += 4)
                {
                    if (pixels[i] < 255)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyIconClip(bool shouldRound)
        {
            var visual = ElementCompositionPreview.GetElementVisual(SiteIconImage);
            if (!shouldRound)
            {
                visual.Clip = null;
                _iconClipGeometry = null;
                return;
            }

            if (_iconClipGeometry == null)
            {
                Compositor compositor = visual.Compositor;
                _iconClipGeometry = compositor.CreateRoundedRectangleGeometry();
                _iconClipGeometry.CornerRadius = new Vector2(IconCornerRadius, IconCornerRadius);
                _iconClipGeometry.Offset = Vector2.Zero;
                visual.Clip = compositor.CreateGeometricClip(_iconClipGeometry);
            }

            float width = SiteIconImage.ActualWidth > 0 ? (float)SiteIconImage.ActualWidth : 64f;
            float height = SiteIconImage.ActualHeight > 0 ? (float)SiteIconImage.ActualHeight : 64f;
            _iconClipGeometry.Size = new Vector2(width, height);
        }

        private void ShowFallbackIcon()
        {
            _currentIconBytes = null;
            SiteIconImage.Source = null;
            SiteIconImage.Visibility = Visibility.Collapsed;
            SiteIconFallback.Visibility = Visibility.Visible;
            ApplyIconClip(false);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private sealed record WebsiteMetadata(string Title, byte[]? IconBytes, string? Error);
    }
}
