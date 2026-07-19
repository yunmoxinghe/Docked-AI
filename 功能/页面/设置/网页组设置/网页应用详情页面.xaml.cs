using Docked_AI.Features.Pages.WebApp.Shared;
using Docked_AI.Features.UnifiedCalls.TopAppBar;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace Docked_AI.Features.Pages.Settings.WebSettings
{
    public sealed partial class WebAppDetailPage : Page
    {
        private const double MinResponsiveWidth = 320;
        private const double MaxResponsiveWidth = 760;
        private const double MinHorizontalMargin = 16;
        private const double MaxHorizontalMargin = 36;
        private double _lastAppliedMargin = -1;
        private double _lastMeasuredWidth = -1;

        private readonly 智能标题 _智能标题 = new();
        private static readonly HttpClient HttpClient = CreateHttpClient();

        private string? _appId;
        private string _originalName = string.Empty;
        private string _originalUrl = string.Empty;
        private byte[]? _originalIconBytes;
        private byte[]? _currentIconBytes;
        private bool _hasChanges;
        private Button? _saveButton;

        public WebAppDetailPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _智能标题.Setup(PageScrollViewer, PageTitleBlock);

            // 获取传递的应用 ID
            if (e.Parameter is string appId)
            {
                _appId = appId;
            }

            // 添加保存按钮到标题栏（使用 C# Unicode 转义格式）
            _saveButton = TopAppBarService.SetRightIconButton("\uE74E", OnSaveClick, "保存");
            if (_saveButton != null)
            {
                _saveButton.IsEnabled = false;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _智能标题.Cleanup();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadAppDataAsync();
            UpdateVisualState();
        }

        private async Task LoadAppDataAsync()
        {
            if (string.IsNullOrEmpty(_appId))
            {
                return;
            }

            try
            {
                var shortcuts = await WebAppShortcutStore.LoadAsync();
                var app = shortcuts.FirstOrDefault(s => s.Id == _appId);
                
                if (app == null)
                {
                    ShowStatus("应用不存在", InfoBarSeverity.Error);
                    return;
                }

                // 保存原始数据
                _originalName = app.Name;
                _originalUrl = app.Url;
                _originalIconBytes = app.IconBytes;
                _currentIconBytes = app.IconBytes;

                // 填充界面
                NameTextBox.Text = app.Name;
                UrlTextBox.Text = app.Url;
                
                if (app.IconBytes != null && app.IconBytes.Length > 0)
                {
                    await ShowIconAsync(app.IconBytes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] Failed to load app: {ex}");
                ShowStatus("加载失败", InfoBarSeverity.Error);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Math.Abs(e.NewSize.Width - _lastMeasuredWidth) < 1)
            {
                return;
            }
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            double width = RootGrid?.ActualWidth ?? 0;
            if (width <= 0 && RootGrid != null)
            {
                width = RootGrid.ActualWidth;
            }
            if (width <= 0)
            {
                width = ActualWidth;
            }

            double normalized = (width - MinResponsiveWidth) / (MaxResponsiveWidth - MinResponsiveWidth);
            normalized = Math.Clamp(normalized, 0, 1);
            double horizontalMargin = Math.Round(MinHorizontalMargin + ((MaxHorizontalMargin - MinHorizontalMargin) * normalized));

            if (Math.Abs(horizontalMargin - _lastAppliedMargin) > 0.01)
            {
                PageContentPanel.Margin = new Thickness(horizontalMargin, 0, horizontalMargin, 0);
                _lastAppliedMargin = horizontalMargin;
            }
            _lastMeasuredWidth = width;
        }

        private void OnFieldChanged(object sender, TextChangedEventArgs e)
        {
            CheckForChanges();
        }

        private void OnNameCardClick(object sender, RoutedEventArgs e)
        {
            NameTextBox?.Focus(FocusState.Programmatic);
        }

        private void OnUrlCardClick(object sender, RoutedEventArgs e)
        {
            UrlTextBox?.Focus(FocusState.Programmatic);
        }

        private void OnIconUrlChanged(object sender, TextChangedEventArgs e)
        {
            string url = IconUrlTextBox.Text?.Trim() ?? string.Empty;
            LoadIconFromUrlButton.IsEnabled = Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                                               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private void CheckForChanges()
        {
            string currentName = NameTextBox.Text?.Trim() ?? string.Empty;
            string currentUrl = UrlTextBox.Text?.Trim() ?? string.Empty;

            bool nameChanged = currentName != _originalName;
            bool urlChanged = currentUrl != _originalUrl;
            bool iconChanged = !ByteArrayEquals(_currentIconBytes, _originalIconBytes);

            _hasChanges = nameChanged || urlChanged || iconChanged;

            if (_saveButton != null)
            {
                _saveButton.IsEnabled = _hasChanges && !string.IsNullOrWhiteSpace(currentName) && !string.IsNullOrWhiteSpace(currentUrl);
            }
        }

        private async void OnChooseLocalIconClick(object sender, RoutedEventArgs e)
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
                    ShowStatus("无法打开文件选择器", InfoBarSeverity.Error);
                    return;
                }

                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                StorageFile? file = await picker.PickSingleFileAsync();
                
                if (file == null)
                {
                    return;
                }

                IBuffer buffer = await FileIO.ReadBufferAsync(file);
                byte[] bytes = buffer.ToArray();
                
                if (bytes.Length == 0)
                {
                    ShowStatus("图片文件为空", InfoBarSeverity.Warning);
                    return;
                }

                if (bytes.Length > 4 * 1024 * 1024)
                {
                    ShowStatus("图片文件过大（最大 4MB）", InfoBarSeverity.Warning);
                    return;
                }

                _currentIconBytes = bytes;
                await ShowIconAsync(bytes);
                CheckForChanges();
                ShowStatus("图标已更新", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] Failed to choose icon: {ex}");
                ShowStatus("选择图标失败", InfoBarSeverity.Error);
            }
        }

        private async void OnLoadIconFromUrlClick(object sender, RoutedEventArgs e)
        {
            string url = IconUrlTextBox.Text?.Trim() ?? string.Empty;
            
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                ShowStatus("URL 格式不正确", InfoBarSeverity.Warning);
                return;
            }

            try
            {
                LoadIconFromUrlButton.IsEnabled = false;
                ShowStatus("正在下载图标...", InfoBarSeverity.Informational);

                using var response = await HttpClient.GetAsync(uri);
                response.EnsureSuccessStatusCode();

                string? contentType = response.Content.Headers.ContentType?.MediaType;
                if (!string.IsNullOrWhiteSpace(contentType))
                {
                    if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowStatus("URL 不是有效的图片", InfoBarSeverity.Warning);
                        return;
                    }
                    if (contentType.Contains("svg", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowStatus("不支持 SVG 格式", InfoBarSeverity.Warning);
                        return;
                    }
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                
                if (bytes.Length == 0)
                {
                    ShowStatus("图片为空", InfoBarSeverity.Warning);
                    return;
                }

                if (bytes.Length > 4 * 1024 * 1024)
                {
                    ShowStatus("图片过大（最大 4MB）", InfoBarSeverity.Warning);
                    return;
                }

                // 验证是否可以解码
                if (!await CanDecodeBitmapAsync(bytes))
                {
                    ShowStatus("无法解码图片", InfoBarSeverity.Warning);
                    return;
                }

                _currentIconBytes = bytes;
                await ShowIconAsync(bytes);
                CheckForChanges();
                ShowStatus("图标加载成功", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] Failed to load icon from URL: {ex}");
                ShowStatus($"加载失败：{ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                LoadIconFromUrlButton.IsEnabled = true;
            }
        }

        private void OnResetIconClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] OnResetIconClick: _originalIconBytes={((_originalIconBytes?.Length ?? 0) > 0 ? "有" : "无")}");
            
            _currentIconBytes = null;
            IconPreviewImage.Source = null;
            IconPreviewImage.Visibility = Visibility.Collapsed;
            IconPreviewFallback.Visibility = Visibility.Visible;
            CheckForChanges();
            
            System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] OnResetIconClick: _hasChanges={_hasChanges}, SaveButton.IsEnabled={_saveButton?.IsEnabled}");
            ShowStatus("图标已重置", InfoBarSeverity.Success);
        }

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[WebAppDetailPage] OnSaveClick started");
            
            if (string.IsNullOrEmpty(_appId))
            {
                System.Diagnostics.Debug.WriteLine("[WebAppDetailPage] OnSaveClick: _appId is empty");
                return;
            }

            string name = NameTextBox.Text?.Trim() ?? string.Empty;
            string url = UrlTextBox.Text?.Trim() ?? string.Empty;

            System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] OnSaveClick: name='{name}', url='{url}', iconBytes={((_currentIconBytes?.Length ?? 0) > 0 ? "有" : "无")}");

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowStatus("请输入应用名称", InfoBarSeverity.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                ShowStatus("请输入网站地址", InfoBarSeverity.Warning);
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ShowStatus("网站地址格式不正确", InfoBarSeverity.Warning);
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[WebAppDetailPage] OnSaveClick: Starting save process");
                
                if (_saveButton != null)
                {
                    _saveButton.IsEnabled = false;
                }

                // ⭐ 检测变化类型（用于细粒度更新）
                var updateType = WebAppUpdateType.None;
                if (name != _originalName)
                {
                    updateType |= WebAppUpdateType.Name;
                    System.Diagnostics.Debug.WriteLine("[WebAppDetailPage] 检测到名称变化");
                }
                if (uri.AbsoluteUri != _originalUrl)
                {
                    updateType |= WebAppUpdateType.Url;
                    System.Diagnostics.Debug.WriteLine("[WebAppDetailPage] 检测到 URL 变化");
                }
                if (!ByteArrayEquals(_currentIconBytes, _originalIconBytes))
                {
                    updateType |= WebAppUpdateType.Icon;
                    System.Diagnostics.Debug.WriteLine("[WebAppDetailPage] 检测到图标变化");
                }

                // 读取现有数据
                var shortcuts = await WebAppShortcutStore.LoadAsync();
                var updatedShortcuts = shortcuts.ToList();

                System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] OnSaveClick: Loaded {updatedShortcuts.Count} shortcuts");

                // 查找并更新
                var index = updatedShortcuts.FindIndex(s => s.Id == _appId);
                if (index >= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] OnSaveClick: Found app at index {index}, updating");
                    updatedShortcuts[index] = new WebAppShortcut(_appId, name, uri.AbsoluteUri, _currentIconBytes);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[WebAppDetailPage] OnSaveClick: App not found in list!");
                }

                // 保存
                System.Diagnostics.Debug.WriteLine("[WebAppDetailPage] OnSaveClick: Saving shortcuts");
                await WebAppShortcutStore.SaveAsync(updatedShortcuts);

                // ⭐ 使用新的更新服务（细粒度通知）
                if (updateType != WebAppUpdateType.None)
                {
                    System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] 通知更新: {_appId}, 类型: {updateType}");
                    WebAppUpdateService.NotifyUpdate(_appId, updateType);
                }

                // 更新原始数据
                _originalName = name;
                _originalUrl = uri.AbsoluteUri;
                _originalIconBytes = _currentIconBytes;
                _hasChanges = false;

                System.Diagnostics.Debug.WriteLine("[WebAppDetailPage] OnSaveClick: Save successful");
                ShowStatus("保存成功", InfoBarSeverity.Success);

                // 延迟返回上一页
                await Task.Delay(1000);
                System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] OnSaveClick: CanGoBack={Frame.CanGoBack}");
                if (Frame.CanGoBack)
                {
                    System.Diagnostics.Debug.WriteLine("[WebAppDetailPage] OnSaveClick: Going back");
                    Frame.GoBack();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] Failed to save: {ex}");
                ShowStatus($"保存失败：{ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                if (_saveButton != null)
                {
                    _saveButton.IsEnabled = _hasChanges;
                }
                System.Diagnostics.Debug.WriteLine("[WebAppDetailPage] OnSaveClick completed");
            }
        }

        private async Task ShowIconAsync(byte[] iconBytes)
        {
            try
            {
                var bitmap = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(iconBytes.AsBuffer());
                stream.Seek(0);
                await bitmap.SetSourceAsync(stream);

                IconPreviewImage.Source = bitmap;
                IconPreviewImage.Visibility = Visibility.Visible;
                IconPreviewFallback.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] Failed to show icon: {ex}");
                IconPreviewImage.Visibility = Visibility.Collapsed;
                IconPreviewFallback.Visibility = Visibility.Visible;
            }
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

        private static bool ByteArrayEquals(byte[]? a, byte[]? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            return a.SequenceEqual(b);
        }

        private void ShowStatus(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
            return client;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}
