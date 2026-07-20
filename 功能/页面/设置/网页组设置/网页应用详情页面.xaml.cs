using Docked_AI.Features.Pages.WebApp.Shared;
using Docked_AI.Features.UnifiedCalls.TopAppBar;
using Docked_AI.Features.Localization;
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
using Windows.System;

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

        // 图标 Code 存储（十六进制，如 "E707"）
        private string? _leftButtonIconCode;
        private string? _rightButtonIconCode;

        private string? _appId;
        private string _originalName = string.Empty;
        private string _originalUrl = string.Empty;
        private byte[]? _originalIconBytes;
        private byte[]? _currentIconBytes;
        private KeyboardMappingButtonConfig? _originalLeftButtonConfig;
        private KeyboardMappingButtonConfig? _originalRightButtonConfig;
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
            _saveButton = TopAppBarService.SetRightIconButton("\uE74E", OnSaveClick, LocalizationHelper.GetString("WebAppDetailPage_SaveButton"));
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
                    ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_AppNotFound"), InfoBarSeverity.Error);
                    return;
                }

                // 保存原始数据
                _originalName = app.Name;
                _originalUrl = app.Url;
                _originalIconBytes = app.IconBytes;
                _currentIconBytes = app.IconBytes;
                _originalLeftButtonConfig = app.LeftButtonConfig;
                _originalRightButtonConfig = app.RightButtonConfig;

                // 填充界面
                NameTextBox.Text = app.Name;
                UrlTextBox.Text = app.Url;
                
                if (app.IconBytes != null && app.IconBytes.Length > 0)
                {
                    await ShowIconAsync(app.IconBytes);
                }
                
                // 填充左侧按钮配置
                LoadLeftButtonConfig(app.LeftButton);
                
                // 填充右侧按钮配置
                LoadRightButtonConfig(app.RightButton);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] Failed to load app: {ex}");
                ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_LoadFailed"), InfoBarSeverity.Error);
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
            bool leftButtonChanged = IsLeftButtonConfigChanged();
            bool rightButtonChanged = IsRightButtonConfigChanged();

            _hasChanges = nameChanged || urlChanged || iconChanged || leftButtonChanged || rightButtonChanged;

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
                    ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_CannotOpenPicker"), InfoBarSeverity.Error);
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
                    ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_ImageFileEmpty"), InfoBarSeverity.Warning);
                    return;
                }

                if (bytes.Length > 4 * 1024 * 1024)
                {
                    ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_ImageFileTooLarge"), InfoBarSeverity.Warning);
                    return;
                }

                _currentIconBytes = bytes;
                await ShowIconAsync(bytes);
                CheckForChanges();
                ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_IconUpdated"), InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] Failed to choose icon: {ex}");
                ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_SelectIconFailed"), InfoBarSeverity.Error);
            }
        }

        private async void OnLoadIconFromUrlClick(object sender, RoutedEventArgs e)
        {
            string url = IconUrlTextBox.Text?.Trim() ?? string.Empty;
            
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_InvalidUrl"), InfoBarSeverity.Warning);
                return;
            }

            try
            {
                LoadIconFromUrlButton.IsEnabled = false;
                ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_DownloadingIcon"), InfoBarSeverity.Informational);

                using var response = await HttpClient.GetAsync(uri);
                response.EnsureSuccessStatusCode();

                string? contentType = response.Content.Headers.ContentType?.MediaType;
                if (!string.IsNullOrWhiteSpace(contentType))
                {
                    if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_UrlNotValidImage"), InfoBarSeverity.Warning);
                        return;
                    }
                    if (contentType.Contains("svg", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_SvgNotSupported"), InfoBarSeverity.Warning);
                        return;
                    }
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                
                if (bytes.Length == 0)
                {
                    ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_ImageEmpty"), InfoBarSeverity.Warning);
                    return;
                }

                if (bytes.Length > 4 * 1024 * 1024)
                {
                    ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_ImageTooLarge"), InfoBarSeverity.Warning);
                    return;
                }

                // 验证是否可以解码
                if (!await CanDecodeBitmapAsync(bytes))
                {
                    ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_CannotDecodeImage"), InfoBarSeverity.Warning);
                    return;
                }

                _currentIconBytes = bytes;
                await ShowIconAsync(bytes);
                CheckForChanges();
                ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_IconLoadSuccess"), InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebAppDetailPage] Failed to load icon from URL: {ex}");
                ShowStatus(string.Format(LocalizationHelper.GetString("WebAppDetailPage_LoadFailedWithReason"), ex.Message), InfoBarSeverity.Error);
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
            ShowStatus(LocalizationHelper.GetString("WebAppDetailPage_IconReset"), InfoBarSeverity.Success);
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
                
                // 检测按钮配置变化
                var currentLeftConfig = GetCurrentLeftButtonConfig();
                var currentRightConfig = GetCurrentRightButtonConfig();
                if (!IsButtonConfigEqual(currentLeftConfig, _originalLeftButtonConfig) ||
                    !IsButtonConfigEqual(currentRightConfig, _originalRightButtonConfig))
                {
                    updateType |= WebAppUpdateType.ButtonConfig;
                    System.Diagnostics.Debug.WriteLine("[WebAppDetailPage] 检测到按钮配置变化");
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
                    
                    // 获取当前按钮配置
                    var leftButtonConfig = GetCurrentLeftButtonConfig();
                    var rightButtonConfig = GetCurrentRightButtonConfig();
                    
                    updatedShortcuts[index] = new WebAppShortcut(
                        _appId, 
                        name, 
                        uri.AbsoluteUri, 
                        _currentIconBytes,
                        leftButtonConfig,
                        rightButtonConfig);
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
                _originalLeftButtonConfig = GetCurrentLeftButtonConfig();
                _originalRightButtonConfig = GetCurrentRightButtonConfig();
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
        
        #region 键盘映射按钮配置

        // 快捷键录制状态
        private VirtualKey _leftButtonRecordedKey = VirtualKey.None;
        private bool _leftButtonRecordedCtrl;
        private bool _leftButtonRecordedShift;
        private bool _leftButtonRecordedAlt;
        
        private VirtualKey _rightButtonRecordedKey = VirtualKey.None;
        private bool _rightButtonRecordedCtrl;
        private bool _rightButtonRecordedShift;
        private bool _rightButtonRecordedAlt;

        private void LoadLeftButtonConfig(KeyboardMappingButtonConfig config)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadLeftButtonConfig] config.StaticIconGlyph='{config.StaticIconGlyph}' (长度={config.StaticIconGlyph?.Length ?? 0})");
            
            LeftButtonEnabledToggle.Toggled -= OnLeftButtonEnabledToggled;
            LeftButtonEnabledToggle.IsOn = config.IsEnabled;
            LeftButtonEnabledToggle.Toggled += OnLeftButtonEnabledToggled;
            
            // 加载图标类型
            SelectComboBoxItemByTag(LeftButtonIconTypeComboBox, config.IconType);
            
            // 加载静态图标：将 Glyph (Unicode) 转换为 Code (十六进制)
            _leftButtonIconCode = GlyphToCode(config.StaticIconGlyph);
            UpdateLeftButtonIconPreview();
            
            // 加载动态图标类型
            SelectComboBoxItemByTag(LeftButtonAnimatedIconTypeComboBox, config.AnimatedIconType);
            
            // 加载工具提示
            LeftButtonTooltipTextBox.Text = config.Tooltip;
            
            // 加载快捷键
            _leftButtonRecordedKey = config.Key;
            _leftButtonRecordedCtrl = config.Ctrl;
            _leftButtonRecordedShift = config.Shift;
            _leftButtonRecordedAlt = config.Alt;
            
            UpdateLeftButtonIconVisibility();
            UpdateLeftButtonHotkeyPreview();
            UpdateLeftButtonExpanderItemsEnabled();
        }

        private void LoadRightButtonConfig(KeyboardMappingButtonConfig config)
        {
            RightButtonEnabledToggle.Toggled -= OnRightButtonEnabledToggled;
            RightButtonEnabledToggle.IsOn = config.IsEnabled;
            RightButtonEnabledToggle.Toggled += OnRightButtonEnabledToggled;
            
            // 加载图标类型
            SelectComboBoxItemByTag(RightButtonIconTypeComboBox, config.IconType);
            
            // 加载静态图标：将 Glyph (Unicode) 转换为 Code (十六进制)
            _rightButtonIconCode = GlyphToCode(config.StaticIconGlyph);
            UpdateRightButtonIconPreview();
            
            // 加载动态图标类型
            SelectComboBoxItemByTag(RightButtonAnimatedIconTypeComboBox, config.AnimatedIconType);
            
            // 加载工具提示
            RightButtonTooltipTextBox.Text = config.Tooltip;
            
            // 加载快捷键
            _rightButtonRecordedKey = config.Key;
            _rightButtonRecordedCtrl = config.Ctrl;
            _rightButtonRecordedShift = config.Shift;
            _rightButtonRecordedAlt = config.Alt;
            
            UpdateRightButtonIconVisibility();
            UpdateRightButtonHotkeyPreview();
            UpdateRightButtonExpanderItemsEnabled();
        }

        private void OnLeftButtonEnabledToggled(object sender, RoutedEventArgs e)
        {
            UpdateLeftButtonExpanderItemsEnabled();
            CheckForChanges();
        }

        private void OnRightButtonEnabledToggled(object sender, RoutedEventArgs e)
        {
            UpdateRightButtonExpanderItemsEnabled();
            CheckForChanges();
        }

        private void OnLeftButtonFieldChanged(object sender, RoutedEventArgs e)
        {
            // ✅ 使用 ReferenceEquals 进行引用比较（消除 CS0252 警告）
            if (ReferenceEquals(sender, LeftButtonIconTypeComboBox))
            {
                UpdateLeftButtonIconVisibility();
            }
            
            CheckForChanges();
        }

        private void OnRightButtonFieldChanged(object sender, RoutedEventArgs e)
        {
            // ✅ 使用 ReferenceEquals 进行引用比较（消除 CS0252 警告）
            if (ReferenceEquals(sender, RightButtonIconTypeComboBox))
            {
                UpdateRightButtonIconVisibility();
            }
            
            CheckForChanges();
        }

        private void UpdateLeftButtonIconVisibility()
        {
            var iconType = GetSelectedComboBoxTag(LeftButtonIconTypeComboBox);
            bool isStatic = iconType == "Static";
            
            if (LeftButtonStaticIconCard != null)
            {
                LeftButtonStaticIconCard.Visibility = isStatic ? Visibility.Visible : Visibility.Collapsed;
            }
            
            if (LeftButtonAnimatedIconCard != null)
            {
                LeftButtonAnimatedIconCard.Visibility = isStatic ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void UpdateRightButtonIconVisibility()
        {
            var iconType = GetSelectedComboBoxTag(RightButtonIconTypeComboBox);
            bool isStatic = iconType == "Static";
            
            if (RightButtonStaticIconCard != null)
            {
                RightButtonStaticIconCard.Visibility = isStatic ? Visibility.Visible : Visibility.Collapsed;
            }
            
            if (RightButtonAnimatedIconCard != null)
            {
                RightButtonAnimatedIconCard.Visibility = isStatic ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private async void OnLeftButtonHotkeyButtonClick(object sender, RoutedEventArgs e)
        {
            var result = await HotkeyRecorderHelper.ShowRecorderAsync(
                this,
                _leftButtonRecordedKey,
                _leftButtonRecordedCtrl,
                _leftButtonRecordedShift,
                _leftButtonRecordedAlt);

            if (result != null)
            {
                _leftButtonRecordedKey = result.Key;
                _leftButtonRecordedCtrl = result.Ctrl;
                _leftButtonRecordedShift = result.Shift;
                _leftButtonRecordedAlt = result.Alt;
                
                UpdateLeftButtonHotkeyPreview();
                CheckForChanges();
            }
        }

        private async void OnLeftButtonPickIconClick(object sender, RoutedEventArgs e)
        {
            var result = await IconPickerHelper.ShowPickerAsync(this, _leftButtonIconCode);
            
            if (result != null)
            {
                _leftButtonIconCode = result; // 存储 Code（十六进制）
                UpdateLeftButtonIconPreview(); // 更新预览
                CheckForChanges();
            }
        }

        private async void OnRightButtonHotkeyButtonClick(object sender, RoutedEventArgs e)
        {
            var result = await HotkeyRecorderHelper.ShowRecorderAsync(
                this,
                _rightButtonRecordedKey,
                _rightButtonRecordedCtrl,
                _rightButtonRecordedShift,
                _rightButtonRecordedAlt);

            if (result != null)
            {
                _rightButtonRecordedKey = result.Key;
                _rightButtonRecordedCtrl = result.Ctrl;
                _rightButtonRecordedShift = result.Shift;
                _rightButtonRecordedAlt = result.Alt;
                
                UpdateRightButtonHotkeyPreview();
                CheckForChanges();
            }
        }

        private async void OnRightButtonPickIconClick(object sender, RoutedEventArgs e)
        {
            var result = await IconPickerHelper.ShowPickerAsync(this, _rightButtonIconCode);
            
            if (result != null)
            {
                _rightButtonIconCode = result; // 存储 Code（十六进制）
                UpdateRightButtonIconPreview(); // 更新预览
                CheckForChanges();
            }
        }

        private void UpdateLeftButtonExpanderItemsEnabled()
        {
            bool isEnabled = LeftButtonEnabledToggle.IsOn;
            
            if (LeftButtonExpander?.Items != null)
            {
                foreach (var item in LeftButtonExpander.Items)
                {
                    if (item is CommunityToolkit.WinUI.Controls.SettingsCard card)
                    {
                        card.IsEnabled = isEnabled;
                    }
                }
            }
        }

        private void UpdateRightButtonExpanderItemsEnabled()
        {
            bool isEnabled = RightButtonEnabledToggle.IsOn;
            
            if (RightButtonExpander?.Items != null)
            {
                foreach (var item in RightButtonExpander.Items)
                {
                    if (item is CommunityToolkit.WinUI.Controls.SettingsCard card)
                    {
                        card.IsEnabled = isEnabled;
                    }
                }
            }
        }

        private void UpdateLeftButtonHotkeyPreview()
        {
            if (LeftButtonHotkeyPreview == null)
            {
                return;
            }

            if (_leftButtonRecordedKey == VirtualKey.None)
            {
                LeftButtonHotkeyPreview.Text = "未设置";
                return;
            }

            var parts = new System.Collections.Generic.List<string>();
            if (_leftButtonRecordedCtrl) parts.Add("Ctrl");
            if (_leftButtonRecordedShift) parts.Add("Shift");
            if (_leftButtonRecordedAlt) parts.Add("Alt");
            parts.Add(GetKeyDisplayName(_leftButtonRecordedKey));

            LeftButtonHotkeyPreview.Text = string.Join(" + ", parts);
        }

        private void UpdateRightButtonHotkeyPreview()
        {
            if (RightButtonHotkeyPreview == null)
            {
                return;
            }

            if (_rightButtonRecordedKey == VirtualKey.None)
            {
                RightButtonHotkeyPreview.Text = "未设置";
                return;
            }

            var parts = new System.Collections.Generic.List<string>();
            if (_rightButtonRecordedCtrl) parts.Add("Ctrl");
            if (_rightButtonRecordedShift) parts.Add("Shift");
            if (_rightButtonRecordedAlt) parts.Add("Alt");
            parts.Add(GetKeyDisplayName(_rightButtonRecordedKey));

            RightButtonHotkeyPreview.Text = string.Join(" + ", parts);
        }

        private static string GetKeyDisplayName(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.Space => "Space",
                VirtualKey.Enter => "Enter",
                VirtualKey.Tab => "Tab",
                VirtualKey.Escape => "Esc",
                VirtualKey.Back => "Backspace",
                VirtualKey.Delete => "Delete",
                VirtualKey.Home => "Home",
                VirtualKey.End => "End",
                VirtualKey.PageUp => "PageUp",
                VirtualKey.PageDown => "PageDown",
                VirtualKey.Left => "←",
                VirtualKey.Right => "→",
                VirtualKey.Up => "↑",
                VirtualKey.Down => "↓",
                _ => key.ToString()
            };
        }

        private KeyboardMappingButtonConfig GetCurrentLeftButtonConfig()
        {
            // 防止在控件加载前调用
            if (LeftButtonEnabledToggle == null || 
                LeftButtonIconTypeComboBox == null || 
                LeftButtonTooltipTextBox == null)
            {
                return KeyboardMappingButtonConfig.CreateDefault();
            }

            return new KeyboardMappingButtonConfig
            {
                IsEnabled = LeftButtonEnabledToggle.IsOn,
                IconType = GetSelectedComboBoxTag(LeftButtonIconTypeComboBox) ?? "Static",
                StaticIconGlyph = CodeToGlyph(_leftButtonIconCode) ?? "\uE92E", // Code → Glyph
                AnimatedIconType = GetSelectedComboBoxTag(LeftButtonAnimatedIconTypeComboBox) ?? "AnimatedChevronDownSmallVisualSource",
                Tooltip = LeftButtonTooltipTextBox.Text?.Trim() ?? "执行快捷键",
                Key = _leftButtonRecordedKey,
                Ctrl = _leftButtonRecordedCtrl,
                Shift = _leftButtonRecordedShift,
                Alt = _leftButtonRecordedAlt
            };
        }

        private KeyboardMappingButtonConfig GetCurrentRightButtonConfig()
        {
            // 防止在控件加载前调用
            if (RightButtonEnabledToggle == null || 
                RightButtonIconTypeComboBox == null || 
                RightButtonTooltipTextBox == null)
            {
                return KeyboardMappingButtonConfig.CreateDefault();
            }

            return new KeyboardMappingButtonConfig
            {
                IsEnabled = RightButtonEnabledToggle.IsOn,
                IconType = GetSelectedComboBoxTag(RightButtonIconTypeComboBox) ?? "Static",
                StaticIconGlyph = CodeToGlyph(_rightButtonIconCode) ?? "\uE92E", // Code → Glyph
                AnimatedIconType = GetSelectedComboBoxTag(RightButtonAnimatedIconTypeComboBox) ?? "AnimatedChevronDownSmallVisualSource",
                Tooltip = RightButtonTooltipTextBox.Text?.Trim() ?? "执行快捷键",
                Key = _rightButtonRecordedKey,
                Ctrl = _rightButtonRecordedCtrl,
                Shift = _rightButtonRecordedShift,
                Alt = _rightButtonRecordedAlt
            };
        }

        private bool IsLeftButtonConfigChanged()
        {
            var current = GetCurrentLeftButtonConfig();
            var original = _originalLeftButtonConfig ?? KeyboardMappingButtonConfig.CreateDefault();
            
            return current.IsEnabled != original.IsEnabled ||
                   current.IconType != original.IconType ||
                   current.StaticIconGlyph != original.StaticIconGlyph ||
                   current.AnimatedIconType != original.AnimatedIconType ||
                   current.Tooltip != original.Tooltip ||
                   current.Key != original.Key ||
                   current.Ctrl != original.Ctrl ||
                   current.Shift != original.Shift ||
                   current.Alt != original.Alt;
        }

        private bool IsRightButtonConfigChanged()
        {
            var current = GetCurrentRightButtonConfig();
            var original = _originalRightButtonConfig ?? KeyboardMappingButtonConfig.CreateDefault();
            
            return current.IsEnabled != original.IsEnabled ||
                   current.IconType != original.IconType ||
                   current.StaticIconGlyph != original.StaticIconGlyph ||
                   current.AnimatedIconType != original.AnimatedIconType ||
                   current.Tooltip != original.Tooltip ||
                   current.Key != original.Key ||
                   current.Ctrl != original.Ctrl ||
                   current.Shift != original.Shift ||
                   current.Alt != original.Alt;
        }

        // 辅助方法：从 ComboBox 中选择指定 Tag 的项
        private void SelectComboBoxItemByTag(ComboBox comboBox, string tag)
        {
            if (comboBox == null || string.IsNullOrEmpty(tag))
            {
                return;
            }

            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag?.ToString() == tag)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        // 辅助方法：获取 ComboBox 选中项的 Tag
        private string? GetSelectedComboBoxTag(ComboBox? comboBox)
        {
            return (comboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        }

        // 辅助方法：比较两个按钮配置是否相等
        private static bool IsButtonConfigEqual(KeyboardMappingButtonConfig? a, KeyboardMappingButtonConfig? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            
            return a.IsEnabled == b.IsEnabled &&
                   a.IconType == b.IconType &&
                   a.StaticIconGlyph == b.StaticIconGlyph &&
                   a.AnimatedIconType == b.AnimatedIconType &&
                   a.Tooltip == b.Tooltip &&
                   a.Key == b.Key &&
                   a.Ctrl == b.Ctrl &&
                   a.Shift == b.Shift &&
                   a.Alt == b.Alt;
        }

        #endregion
        
        #region 图标 Code ↔ Glyph 转换和预览更新

        /// <summary>
        /// 将 Glyph (Unicode 字符) 转换为 Code (十六进制字符串)
        /// </summary>
        private static string? GlyphToCode(string? glyph)
        {
            if (string.IsNullOrEmpty(glyph) || glyph.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[GlyphToCode] 输入为空");
                return null;
            }

            // 获取第一个字符的 Unicode 码点
            int codePoint = char.ConvertToUtf32(glyph, 0);
            string code = codePoint.ToString("X"); // 转换为十六进制（大写）
            
            System.Diagnostics.Debug.WriteLine($"[GlyphToCode] 输入: '{glyph}' (长度={glyph.Length}), 输出: {code}, 码点={codePoint}");
            return code;
        }

        /// <summary>
        /// 将 Code (十六进制字符串) 转换为 Glyph (Unicode 字符)
        /// </summary>
        private static string? CodeToGlyph(string? code)
        {
            if (string.IsNullOrEmpty(code))
            {
                System.Diagnostics.Debug.WriteLine($"[CodeToGlyph] 输入为空");
                return null;
            }

            try
            {
                // 解析十六进制字符串
                int codePoint = Convert.ToInt32(code, 16);
                string glyph = char.ConvertFromUtf32(codePoint);
                
                System.Diagnostics.Debug.WriteLine($"[CodeToGlyph] 输入: {code}, 输出: '{glyph}' (长度={glyph.Length}), 码点={codePoint}");
                return glyph;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CodeToGlyph] 转换失败: code={code}, 错误={ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 更新左侧按钮图标预览
        /// </summary>
        private void UpdateLeftButtonIconPreview()
        {
            if (LeftButtonStaticIconPreview == null)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateLeftButtonIconPreview] LeftButtonStaticIconPreview is null");
                return;
            }

            string? glyph = CodeToGlyph(_leftButtonIconCode);
            System.Diagnostics.Debug.WriteLine($"[UpdateLeftButtonIconPreview] _leftButtonIconCode={_leftButtonIconCode}, glyph={(glyph != null ? $"长度={glyph.Length}" : "null")}");
            
            if (!string.IsNullOrEmpty(glyph))
            {
                LeftButtonStaticIconPreview.Glyph = glyph;
                System.Diagnostics.Debug.WriteLine($"[UpdateLeftButtonIconPreview] 已设置 Glyph, FontFamily={LeftButtonStaticIconPreview.FontFamily}");
            }
            else
            {
                // 默认图标
                LeftButtonStaticIconPreview.Glyph = "\uE92E";
                System.Diagnostics.Debug.WriteLine($"[UpdateLeftButtonIconPreview] 使用默认 Glyph E92E");
            }
        }

        /// <summary>
        /// 更新右侧按钮图标预览
        /// </summary>
        private void UpdateRightButtonIconPreview()
        {
            if (RightButtonStaticIconPreview == null)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateRightButtonIconPreview] RightButtonStaticIconPreview is null");
                return;
            }

            string? glyph = CodeToGlyph(_rightButtonIconCode);
            System.Diagnostics.Debug.WriteLine($"[UpdateRightButtonIconPreview] _rightButtonIconCode={_rightButtonIconCode}, glyph={(glyph != null ? $"长度={glyph.Length}" : "null")}");
            
            if (!string.IsNullOrEmpty(glyph))
            {
                RightButtonStaticIconPreview.Glyph = glyph;
                System.Diagnostics.Debug.WriteLine($"[UpdateRightButtonIconPreview] 已设置 Glyph, FontFamily={RightButtonStaticIconPreview.FontFamily}");
            }
            else
            {
                // 默认图标
                RightButtonStaticIconPreview.Glyph = "\uE92E";
                System.Diagnostics.Debug.WriteLine($"[UpdateRightButtonIconPreview] 使用默认 Glyph E92E");
            }
        }

        #endregion
    }
}
