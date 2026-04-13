using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Linq;
using Windows.System;
using Windows.Globalization;
using Windows.ApplicationModel.Resources.Core;
using Windows.ApplicationModel;
using Docked_AI.Features.Localization;
using Docked_AI.Features.AppEntry.AutoLaunch;
using Docked_AI.Features.Hotkey;
using Docked_AI.Features.Settings;
using Windows.UI.Core;

namespace Docked_AI.Features.Pages.Settings
{
    public sealed partial class SettingsPage : Page
    {
        private const double MinResponsiveWidth = 320;
        private const double MaxResponsiveWidth = 760;
        private const double MinHorizontalMargin = 16;
        private const double MaxHorizontalMargin = 36;
        private double _lastAppliedMargin = -1;
        private double _lastMeasuredWidth = -1;

        // ViewModel for startup settings
        public StartupSettingsViewModel ViewModel { get; }

        // Hotkey management
        private HotkeySettings _hotkeySettings;
        private VirtualKey _tempKey = VirtualKey.None;
        private bool _tempCtrl, _tempAlt, _tempShift, _tempWin;
        private bool _isCapturingHotkey;

        public SettingsPage()
        {
            // Initialize ViewModel
            var startupManager = new StartupTaskManager();
            ViewModel = new StartupSettingsViewModel(startupManager);

            // Initialize hotkey settings
            _hotkeySettings = new HotkeySettings();

            InitializeComponent();
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
            // 不再需要 InitializeLanguageComboBox，因为 XAML 中已经设置了 Content
            LoadLanguageSettings();
            LoadHotkeySettings();
            LoadExperimentalSettings();
            
            // Initialize startup settings asynchronously
            _ = InitializeStartupSettingsAsync();
        }

        private string GetGitHubLinkText()
        {
            return LocalizationHelper.GetString("SettingsPage_GitHubLink/Content");
        }

        private string GetFeedbackLinkText()
        {
            return LocalizationHelper.GetString("SettingsPage_FeedbackLink/Content");
        }

        private async System.Threading.Tasks.Task InitializeStartupSettingsAsync()
        {
            try
            {
                await ViewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to initialize startup settings: {ex}");
            }
        }

        private void LoadLanguageSettings()
        {
            var currentLanguage = ApplicationLanguages.PrimaryLanguageOverride;
            if (string.IsNullOrEmpty(currentLanguage))
            {
                currentLanguage = ApplicationLanguages.Languages[0];
            }

            System.Diagnostics.Debug.WriteLine($"[LoadLanguageSettings] Current language: {currentLanguage}");

            // 暂时取消事件订阅，避免在初始化时触发
            LanguageComboBox.SelectionChanged -= OnLanguageChanged;

            bool found = false;
            
            // 第一步：尝试精确匹配
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                var itemTag = item.Tag?.ToString();
                System.Diagnostics.Debug.WriteLine($"  Checking: Tag={itemTag}, Content={item.Content}");
                if (itemTag == currentLanguage)
                {
                    LanguageComboBox.SelectedItem = item;
                    found = true;
                    System.Diagnostics.Debug.WriteLine($"  ✓ Matched! Selected: {item.Content}");
                    break;
                }
            }

            // 第二步：尝试匹配 zh-Hant-TW -> zh-TW 这种情况
            if (!found && currentLanguage.Contains("-"))
            {
                var parts = currentLanguage.Split('-');
                if (parts.Length == 3) // 例如 zh-Hant-TW
                {
                    var simplifiedTag = $"{parts[0]}-{parts[2]}"; // zh-TW
                    System.Diagnostics.Debug.WriteLine($"  No exact match, trying simplified tag: {simplifiedTag}");
                    foreach (ComboBoxItem item in LanguageComboBox.Items)
                    {
                        var itemTag = item.Tag?.ToString();
                        if (itemTag == simplifiedTag)
                        {
                            LanguageComboBox.SelectedItem = item;
                            found = true;
                            System.Diagnostics.Debug.WriteLine($"  ✓ Matched by simplified tag! Selected: {item.Content}");
                            break;
                        }
                    }
                }
            }

            // 第三步：尝试匹配语言代码（忽略地区）
            if (!found)
            {
                var languageCode = currentLanguage.Split('-')[0];
                System.Diagnostics.Debug.WriteLine($"  No match yet, trying language code: {languageCode}");
                foreach (ComboBoxItem item in LanguageComboBox.Items)
                {
                    var itemTag = item.Tag?.ToString();
                    if (itemTag?.StartsWith(languageCode + "-") == true)
                    {
                        LanguageComboBox.SelectedItem = item;
                        found = true;
                        System.Diagnostics.Debug.WriteLine($"  ✓ Matched by code! Selected: {item.Content}");
                        break;
                    }
                }
            }

            if (!found)
            {
                System.Diagnostics.Debug.WriteLine("  ✗ No match found, defaulting to index 0");
                LanguageComboBox.SelectedIndex = 0;
            }

            System.Diagnostics.Debug.WriteLine($"[LoadLanguageSettings] Final selection: {(LanguageComboBox.SelectedItem as ComboBoxItem)?.Content}");

            // 重新订阅事件
            LanguageComboBox.SelectionChanged += OnLanguageChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateVisualStateAndDiagnostic();
            LoadVersionInfo();
        }

        private void LoadVersionInfo()
        {
            try
            {
                var version = Package.Current.Id.Version;
                var versionString = $"{version.Major}.{version.Minor}.{version.Build}";
                
                // 获取本地化的版本前缀（如"版本："、"Version:"等）
                var versionPrefix = LocalizationHelper.GetString("SettingsPage_VersionPrefix");
                VersionText.Text = $"{versionPrefix}v{versionString}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to load version info: {ex}");
                // 如果读取失败，保持使用本地化资源中的默认值
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Math.Abs(e.NewSize.Width - _lastMeasuredWidth) < 1)
            {
                return;
            }
            UpdateVisualStateAndDiagnostic();
        }

        private void UpdateVisualStateAndDiagnostic()
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

            string mode = normalized >= 1 ? "Wide" : (normalized <= 0 ? "Narrow" : "Fluid");
        }

        private async void OnOpenGitHubClick(object sender, RoutedEventArgs args)
        {
            var uri = new Uri("https://github.com/yunmoxinghe/Docked-AI");
            await Launcher.LaunchUriAsync(uri);
        }

        private async void OnSendFeedbackClick(object sender, RoutedEventArgs args)
        {
            var uri = new Uri("https://github.com/yunmoxinghe/Docked-AI/issues");
            await Launcher.LaunchUriAsync(uri);
        }

        private async void OnViewLicenseClick(object sender, RoutedEventArgs args)
        {
            var uri = new Uri("https://github.com/yunmoxinghe/Docked-AI/blob/main/LICENSE");
            await Launcher.LaunchUriAsync(uri);
        }

        private void LoadExperimentalSettings()
        {
            // 暂时取消事件订阅，避免在初始化时触发
            RoundedWebViewToggle.Toggled -= OnRoundedWebViewToggled;
            WinUIContextMenuToggle.Toggled -= OnWinUIContextMenuToggled;
            
            RoundedWebViewToggle.IsOn = ExperimentalSettings.EnableRoundedWebView;
            WinUIContextMenuToggle.IsOn = ExperimentalSettings.EnableWinUIContextMenu;
            
            // 重新订阅事件
            RoundedWebViewToggle.Toggled += OnRoundedWebViewToggled;
            WinUIContextMenuToggle.Toggled += OnWinUIContextMenuToggled;
        }

        private void OnRoundedWebViewToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableRoundedWebView = toggle.IsOn;
                
                // 通知应用更新 WebView2 圆角设置
                RoundedWebViewSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnWinUIContextMenuToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableWinUIContextMenu = toggle.IsOn;
                
                // 通知应用更新右键菜单设置
                WinUIContextMenuSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Event to notify when rounded webview settings change
        public static event EventHandler? RoundedWebViewSettingsChanged;

        // Event to notify when WinUI context menu settings change
        public static event EventHandler? WinUIContextMenuSettingsChanged;

        private void OnLanguageCardClick(object sender, RoutedEventArgs e)
        {
            // 点击语言设置卡片时，打开 ComboBox 的下拉菜单
            if (LanguageComboBox != null)
            {
                LanguageComboBox.IsDropDownOpen = true;
            }
        }

        private async void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            // 确保 XamlRoot 已初始化
            if (this.XamlRoot == null)
            {
                return;
            }

            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var languageTag = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(languageTag))
                {
                    var currentLanguage = ApplicationLanguages.PrimaryLanguageOverride;
                    if (string.IsNullOrEmpty(currentLanguage))
                    {
                        currentLanguage = ApplicationLanguages.Languages[0];
                    }

                    if (languageTag != currentLanguage)
                    {
                        ApplicationLanguages.PrimaryLanguageOverride = languageTag;
                        
                        var dialog = new ContentDialog
                        {
                            Title = LocalizationHelper.GetString("SettingsPage_RestartTitle"),
                            Content = LocalizationHelper.GetString("SettingsPage_RestartContent"),
                            PrimaryButtonText = LocalizationHelper.GetString("SettingsPage_RestartButton"),
                            CloseButtonText = LocalizationHelper.GetString("SettingsPage_LaterButton"),
                            XamlRoot = this.XamlRoot
                        };

                        var result = await dialog.ShowAsync();
                        if (result == ContentDialogResult.Primary)
                        {
                            await Windows.ApplicationModel.Core.CoreApplication.RequestRestartAsync(string.Empty);
                        }
                    }
                }
            }
        }

        // Event handlers for startup settings
        private async void OnToggleSwitched(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is ToggleSwitch toggleSwitch)
                {
                    await ViewModel.HandleToggleAsync(toggleSwitch.IsOn);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] OnToggleSwitched error: {ex}");
                
                // Show error dialog to user
                if (this.XamlRoot != null)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "切换自启动设置时发生错误，请稍后重试。",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
        }

        private async void OnSettingCardClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await ViewModel.NavigateToSystemSettingsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] OnSettingCardClick error: {ex}");
                
                // Show error dialog to user
                if (this.XamlRoot != null)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "无法打开系统设置页面，请手动前往 Windows 设置 > 应用 > 启动。",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
        }

        // Hotkey settings methods
        private void LoadHotkeySettings()
        {
            // 暂时取消事件订阅，避免在初始化时触发
            HotkeyToggle.Toggled -= OnHotkeyToggled;
            
            HotkeyToggle.IsOn = _hotkeySettings.IsEnabled;
            UpdateHotkeyButtonText();
            
            // 重新订阅事件
            HotkeyToggle.Toggled += OnHotkeyToggled;
        }

        private void UpdateHotkeyButtonText()
        {
            var keys = new System.Collections.Generic.List<string>();
            
            if (_hotkeySettings.Ctrl) keys.Add("Ctrl");
            if (_hotkeySettings.Alt) keys.Add("Alt");
            if (_hotkeySettings.Shift) keys.Add("Shift");
            if (_hotkeySettings.Win) keys.Add("Win");
            
            if (_hotkeySettings.Key != VirtualKey.None)
            {
                keys.Add(GetKeyDisplayName(_hotkeySettings.Key));
            }
            
            // 使用 ItemsControl 显示按键，每个按键都有独立的视觉容器
            HotkeyKeysDisplay.ItemsSource = keys;
        }

        private void OnHotkeyToggled(object sender, RoutedEventArgs e)
        {
            if (_hotkeySettings == null) return;
            
            if (sender is ToggleSwitch toggle)
            {
                _hotkeySettings.IsEnabled = toggle.IsOn;

                // 通知应用更新快捷键注册状态
                HotkeySettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private async void OnHotkeyButtonClick(object sender, RoutedEventArgs e)
        {
            _isCapturingHotkey = false;
            _tempKey = VirtualKey.None;
            _tempCtrl = _tempAlt = _tempShift = _tempWin = false;
            HotkeyToggleButton.IsChecked = false;
            HotkeyDisplayText.Text = "点击开始录制";

            HotkeyDialog.XamlRoot = this.XamlRoot;
            await HotkeyDialog.ShowAsync();
        }

        private async void OnTouchpadSettingsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:devices-touchpad"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] OnTouchpadSettingsClick error: {ex}");
                
                // Show error dialog to user
                if (this.XamlRoot != null)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "无法打开触摸板设置页面。",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
        }

        private void HotkeyToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            _isCapturingHotkey = true;
            _tempKey = VirtualKey.None;
            _tempCtrl = _tempAlt = _tempShift = _tempWin = false;
            HotkeyDisplayText.Text = "按下快捷键...";
        }

        private void HotkeyToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            _isCapturingHotkey = false;
        }

        private void HotkeyToggleButton_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_isCapturingHotkey || HotkeyToggleButton.IsChecked != true) return;

            e.Handled = true;
            var key = e.Key;

            // 获取修饰键状态
            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
            var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            var winLeftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows);
            var winRightState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows);

            bool ctrl = (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
            bool alt = (altState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
            bool shift = (shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
            bool win = (winLeftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                       (winRightState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

            // 忽略单独的修饰键
            if (key == VirtualKey.Control || key == VirtualKey.Menu ||
                key == VirtualKey.Shift || key == VirtualKey.LeftWindows || key == VirtualKey.RightWindows)
            {
                return;
            }

            // 必须至少有一个修饰键
            if (!ctrl && !alt && !shift && !win)
            {
                HotkeyDisplayText.Text = "需要至少一个修饰键";
                return;
            }

            // 更新临时值
            _tempKey = key;
            _tempCtrl = ctrl;
            _tempAlt = alt;
            _tempShift = shift;
            _tempWin = win;

            HotkeyDisplayText.Text = GetHotkeyDisplayText(key, ctrl, alt, shift, win);
        }

        private void HotkeyToggleButton_PreviewKeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (!_isCapturingHotkey || HotkeyToggleButton.IsChecked != true) return;

            e.Handled = true;

            // 当所有键都释放后，自动取消选中 ToggleButton
            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
            var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            var winLeftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows);
            var winRightState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows);

            bool anyModifierPressed = 
                (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                (altState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                (shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                (winLeftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                (winRightState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

            // 如果有有效的快捷键且所有键都释放了，自动取消选中
            if (_tempKey != VirtualKey.None && !anyModifierPressed)
            {
                HotkeyToggleButton.IsChecked = false;
            }
        }

        private void OnHotkeyDialogPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _isCapturingHotkey = false;

            if (_tempKey != VirtualKey.None)
            {
                // 保存设置
                _hotkeySettings.Key = _tempKey;
                _hotkeySettings.Ctrl = _tempCtrl;
                _hotkeySettings.Alt = _tempAlt;
                _hotkeySettings.Shift = _tempShift;
                _hotkeySettings.Win = _tempWin;

                UpdateHotkeyButtonText();

                // 通知应用更新快捷键
                HotkeySettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnHotkeyDialogCloseClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _isCapturingHotkey = false;
        }

        private string GetHotkeyDisplayText(VirtualKey key, bool ctrl, bool alt, bool shift, bool win)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (ctrl) parts.Add("Ctrl");
            if (alt) parts.Add("Alt");
            if (shift) parts.Add("Shift");
            if (win) parts.Add("Win");

            if (key != VirtualKey.None)
            {
                parts.Add(GetKeyDisplayName(key));
            }

            return parts.Count > 0 ? string.Join(" + ", parts) : "未设置";
        }

        private string GetKeyDisplayName(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.Space => "Space",
                VirtualKey.Enter => "Enter",
                VirtualKey.Escape => "Esc",
                VirtualKey.Tab => "Tab",
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
                _ when key >= VirtualKey.F1 && key <= VirtualKey.F24 => $"F{(int)key - (int)VirtualKey.F1 + 1}",
                _ when key >= VirtualKey.Number0 && key <= VirtualKey.Number9 => $"{(int)key - (int)VirtualKey.Number0}",
                _ when key >= VirtualKey.A && key <= VirtualKey.Z => key.ToString(),
                _ => key.ToString()
            };
        }

        // Event to notify when hotkey settings change
        public static event EventHandler? HotkeySettingsChanged;
    }
}
