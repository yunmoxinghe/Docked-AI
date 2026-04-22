using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.System;
using Windows.Globalization;
using Windows.ApplicationModel;
using Docked_AI.Features.Localization;
using Docked_AI.Features.AppEntry.AutoLaunch;
using Docked_AI.Features.Hotkey;
using Docked_AI.Features.Pages.Settings;
using Docked_AI.功能.统一调用.应用内弹窗;
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
            
            LoadHotkeySettings();
            LoadExperimentalSettings();
            LoadWebSettings();
            
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

            // 更新所有语言按钮的视觉状态
            UpdateLanguageButtonStates(currentLanguage);
            
            // 更新右侧显示的当前语言文本
            UpdateCurrentLanguageText(currentLanguage);
        }

        private void UpdateCurrentLanguageText(string currentLanguage)
        {
            // 检查控件是否已初始化
            if (CurrentLanguageText == null)
            {
                return;
            }

            var languageMap = new Dictionary<string, string>
            {
                { "zh-CN", LocalizationHelper.GetString("Language_SimplifiedChinese") },
                { "zh-TW", LocalizationHelper.GetString("Language_TraditionalChinese") },
                { "en-US", LocalizationHelper.GetString("Language_English") },
                { "ja-JP", LocalizationHelper.GetString("Language_Japanese") },
                { "ko-KR", LocalizationHelper.GetString("Language_Korean") },
                { "fr-FR", LocalizationHelper.GetString("Language_French") },
                { "de-DE", LocalizationHelper.GetString("Language_German") },
                { "es-ES", LocalizationHelper.GetString("Language_Spanish") }
            };

            string languageName = currentLanguage;
            
            // 尝试精确匹配
            if (languageMap.ContainsKey(currentLanguage))
            {
                languageName = languageMap[currentLanguage];
            }
            // 尝试匹配 zh-Hant-TW -> zh-TW
            else if (currentLanguage.Contains("-"))
            {
                var parts = currentLanguage.Split('-');
                if (parts.Length == 3)
                {
                    var simplifiedTag = $"{parts[0]}-{parts[2]}";
                    if (languageMap.ContainsKey(simplifiedTag))
                    {
                        languageName = languageMap[simplifiedTag];
                    }
                }
            }

            CurrentLanguageText.Text = languageName;
        }

        private void UpdateLanguageButtonStates(string currentLanguage)
        {
            var buttons = new[]
            {
                LanguageButton_zhCN,
                LanguageButton_zhTW,
                LanguageButton_enUS,
                LanguageButton_jaJP,
                LanguageButton_koKR,
                LanguageButton_frFR,
                LanguageButton_deDE,
                LanguageButton_esES
            };

            foreach (var button in buttons)
            {
                if (button == null) continue;

                try
                {
                    var buttonTag = button.Tag?.ToString();
                    bool isSelected = false;

                    // 精确匹配
                    if (buttonTag == currentLanguage)
                    {
                        isSelected = true;
                    }
                    // 匹配 zh-Hant-TW -> zh-TW
                    else if (currentLanguage.Contains("-"))
                    {
                        var parts = currentLanguage.Split('-');
                        if (parts.Length == 3)
                        {
                            var simplifiedTag = $"{parts[0]}-{parts[2]}";
                            if (buttonTag == simplifiedTag)
                            {
                                isSelected = true;
                            }
                        }
                    }

                    // 设置选中状态的视觉效果
                    if (isSelected)
                    {
                        button.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                        button.BorderThickness = new Thickness(2);
                    }
                    else
                    {
                        button.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
                        button.BorderThickness = new Thickness(1);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateLanguageButtonStates] Error updating button: {ex.Message}");
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateVisualStateAndDiagnostic();
            LoadVersionInfo();
            
            // 在页面加载完成后初始化语言设置
            LoadLanguageSettings();
            
            // 初始化 Frame 动画设置
            LoadFrameAnimationSettings();
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
            await 应用内弹窗服务.OpenExternalLinkAsync(uri, this.XamlRoot);
        }

        private async void OnSendFeedbackClick(object sender, RoutedEventArgs args)
        {
            var uri = new Uri("https://github.com/yunmoxinghe/Docked-AI/issues");
            await 应用内弹窗服务.OpenExternalLinkAsync(uri, this.XamlRoot);
        }

        private async void OnViewLicenseClick(object sender, RoutedEventArgs args)
        {
            var uri = new Uri("https://github.com/yunmoxinghe/Docked-AI/blob/main/LICENSE");
            await 应用内弹窗服务.OpenExternalLinkAsync(uri, this.XamlRoot);
        }

        private void LoadExperimentalSettings()
        {
            // 暂时取消事件订阅，避免在初始化时触发
            AILabToggle.Toggled -= OnAILabToggled;
            RoundedWebViewToggle.Toggled -= OnRoundedWebViewToggled;
            WinUIContextMenuToggle.Toggled -= OnWinUIContextMenuToggled;
            
            AILabToggle.IsOn = ExperimentalSettings.EnableAILab;
            RoundedWebViewToggle.IsOn = ExperimentalSettings.EnableRoundedWebView;
            WinUIContextMenuToggle.IsOn = ExperimentalSettings.EnableWinUIContextMenu;
            
            // 重新订阅事件
            AILabToggle.Toggled += OnAILabToggled;
            RoundedWebViewToggle.Toggled += OnRoundedWebViewToggled;
            WinUIContextMenuToggle.Toggled += OnWinUIContextMenuToggled;
        }

        private void OnAILabToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableAILab = toggle.IsOn;
                
                // 通知应用更新 AI 实验室显示状态
                AILabSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
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

        // Event to notify when AI Lab settings change
        public static event EventHandler? AILabSettingsChanged;

        private void LoadWebSettings()
        {
            // 暂时取消事件订阅，避免在初始化时触发
            MaxWebViewCountBox.ValueChanged -= OnMaxWebViewCountChanged;
            
            MaxWebViewCountBox.Value = ExperimentalSettings.MaxWebViewCount;
            
            // 重新订阅事件
            MaxWebViewCountBox.ValueChanged += OnMaxWebViewCountChanged;
        }

        private void OnMaxWebViewCountChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (!double.IsNaN(args.NewValue))
            {
                int newValue = (int)args.NewValue;
                ExperimentalSettings.MaxWebViewCount = newValue;
                
                // 通知应用更新 WebView 数量限制
                MaxWebViewCountSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Event to notify when max webview count settings change
        public static event EventHandler? MaxWebViewCountSettingsChanged;

        private void LoadFrameAnimationSettings()
        {
            // 暂时取消事件订阅，避免在初始化时触发
            FrameAnimationComboBox.SelectionChanged -= OnFrameAnimationChanged;
            
            var currentAnimation = ExperimentalSettings.FrameNavigationAnimation;
            FrameAnimationComboBox.SelectedIndex = (int)currentAnimation;
            
            // 重新订阅事件
            FrameAnimationComboBox.SelectionChanged += OnFrameAnimationChanged;
        }

        private void OnFrameAnimationCardClick(object sender, RoutedEventArgs e)
        {
            // 点击卡片时打开 ComboBox 的下拉菜单
            FrameAnimationComboBox.IsDropDownOpen = true;
        }

        private void OnFrameAnimationChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Tag?.ToString(), out int animationType))
                {
                    var newAnimation = (FrameAnimationType)animationType;
                    ExperimentalSettings.FrameNavigationAnimation = newAnimation;
                    
                    // 通知应用更新 Frame 动画设置
                    FrameAnimationSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // Event to notify when frame animation settings change
        public static event EventHandler? FrameAnimationSettingsChanged;

        private async void OnLanguageButtonClick(object sender, RoutedEventArgs e)
        {
            // 确保 XamlRoot 已初始化
            if (this.XamlRoot == null)
            {
                return;
            }

            if (sender is Button button)
            {
                var languageTag = button.Tag?.ToString();
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
                        
                        // 更新按钮视觉状态
                        UpdateLanguageButtonStates(languageTag);
                        
                        // 更新右侧显示的当前语言文本
                        UpdateCurrentLanguageText(languageTag);
                        
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
                        Title = LocalizationHelper.GetString("SettingsPage_ErrorTitle"),
                        Content = LocalizationHelper.GetString("SettingsPage_StartupToggleError"),
                        CloseButtonText = LocalizationHelper.GetString("SettingsPage_ConfirmButton"),
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
                        Title = LocalizationHelper.GetString("SettingsPage_ErrorTitle"),
                        Content = LocalizationHelper.GetString("SettingsPage_OpenSettingsError"),
                        CloseButtonText = LocalizationHelper.GetString("SettingsPage_ConfirmButton"),
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
