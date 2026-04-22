using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
using Docked_AI.Features.UnifiedCalls.InAppDialog;
using Docked_AI.功能.统一调用;

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
            var dialog = ExternalOpenConfirmDialogFactory.Create(uri);
            var result = await InAppDialogService.ShowAsync(dialog, this);
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private async void OnSendFeedbackClick(object sender, RoutedEventArgs args)
        {
            var uri = new Uri("https://github.com/yunmoxinghe/Docked-AI/issues");
            var dialog = ExternalOpenConfirmDialogFactory.Create(uri);
            var result = await InAppDialogService.ShowAsync(dialog, this);
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private async void OnViewLicenseClick(object sender, RoutedEventArgs args)
        {
            var uri = new Uri("https://github.com/yunmoxinghe/Docked-AI/blob/main/LICENSE");
            var dialog = ExternalOpenConfirmDialogFactory.Create(uri);
            var result = await InAppDialogService.ShowAsync(dialog, this);
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
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
                        
                        var dialog = LanguageRestartConfirmDialogFactory.Create();
                        var result = await InAppDialogService.ShowAsync(dialog, this);
                        if (result == ContentDialogResult.Primary)
                        {
                            AppRestartService.RestartWithArgs("--restart-from=settings-language");
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
                    var dialog = MessageDialogFactory.Create(
                        LocalizationHelper.GetString("SettingsPage_ErrorTitle"),
                        LocalizationHelper.GetString("SettingsPage_StartupToggleError"),
                        string.Empty,
                        LocalizationHelper.GetString("SettingsPage_ConfirmButton"));
                    await InAppDialogService.ShowAsync(dialog, this);
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
                    var dialog = MessageDialogFactory.Create(
                        LocalizationHelper.GetString("SettingsPage_ErrorTitle"),
                        LocalizationHelper.GetString("SettingsPage_OpenSettingsError"),
                        string.Empty,
                        LocalizationHelper.GetString("SettingsPage_ConfirmButton"));
                    await InAppDialogService.ShowAsync(dialog, this);
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
            var dialog = new HotkeyConfigDialog();
            dialog.ResetCapture();
            var result = await InAppDialogService.ShowAsync(dialog, this);
            if (result != ContentDialogResult.Primary || dialog.Result is null)
            {
                return;
            }

            _hotkeySettings.Key = dialog.Result.Key;
            _hotkeySettings.Ctrl = dialog.Result.Ctrl;
            _hotkeySettings.Alt = dialog.Result.Alt;
            _hotkeySettings.Shift = dialog.Result.Shift;
            _hotkeySettings.Win = dialog.Result.Win;

            UpdateHotkeyButtonText();
            HotkeySettingsChanged?.Invoke(this, EventArgs.Empty);
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
                    var dialog = MessageDialogFactory.Create(
                        "错误",
                        "无法打开触摸板设置页面。",
                        string.Empty,
                        "确定");
                    await InAppDialogService.ShowAsync(dialog, this);
                }
            }
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
