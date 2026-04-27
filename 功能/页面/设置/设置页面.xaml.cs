using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.System;
using Windows.Globalization;
using Windows.ApplicationModel;
using Windows.UI.Core;
using Docked_AI.Features.Localization;
using Docked_AI.Features.AppEntry.AutoLaunch;
using Docked_AI.Features.Hotkey;
using Docked_AI.Features.Pages.Settings;
using Docked_AI.Features.Pages.Lab;
using Docked_AI.Features.UnifiedCalls.InAppDialog;
using Docked_AI.Features.UnifiedCalls.TopAppBar;
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
        private readonly 智能标题 _智能标题 = new();
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

            LanguageComboBox.SelectionChanged -= OnLanguageChanged;

            bool found = false;
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag?.ToString() == currentLanguage)
                {
                    LanguageComboBox.SelectedItem = item;
                    found = true;
                    break;
                }
            }

            if (!found && currentLanguage.Contains("-"))
            {
                var parts = currentLanguage.Split('-');
                if (parts.Length == 3)
                {
                    var simplifiedTag = $"{parts[0]}-{parts[2]}";
                    foreach (ComboBoxItem item in LanguageComboBox.Items)
                    {
                        if (item.Tag?.ToString() == simplifiedTag)
                        {
                            LanguageComboBox.SelectedItem = item;
                            found = true;
                            break;
                        }
                    }
                }
            }

            if (!found)
                LanguageComboBox.SelectedIndex = 0;

            LanguageComboBox.SelectionChanged += OnLanguageChanged;
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

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _智能标题.Setup(SettingsScrollViewer, PageTitleBlock);
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _智能标题.Cleanup();
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
            var dialog = CreateExternalOpenDialog(uri);
            var result = await InAppDialogService.ShowAsync(dialog, this);
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private async void OnSendFeedbackClick(object sender, RoutedEventArgs args)
        {
            var uri = new Uri("https://github.com/yunmoxinghe/Docked-AI/issues");
            var dialog = CreateExternalOpenDialog(uri);
            var result = await InAppDialogService.ShowAsync(dialog, this);
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private async void OnViewLicenseClick(object sender, RoutedEventArgs args)
        {
            var uri = new Uri("https://github.com/yunmoxinghe/Docked-AI/blob/main/LICENSE");
            var dialog = CreateExternalOpenDialog(uri);
            var result = await InAppDialogService.ShowAsync(dialog, this);
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private void LoadExperimentalSettings()
        {
            // 加载返回按钮设置
            if (BackButtonToggle != null)
            {
                BackButtonToggle.Toggled -= OnBackButtonToggled;
                BackButtonToggle.IsOn = ExperimentalSettings.EnableBackButton;
                BackButtonToggle.Toggled += OnBackButtonToggled;
            }

            // 加载三个实验特性开关
            AILabToggle.Toggled -= OnAILabToggled;
            RoundedWebViewToggle.Toggled -= OnRoundedWebViewToggled;
            WinUIContextMenuToggle.Toggled -= OnWinUIContextMenuToggled;

            AILabToggle.IsOn = ExperimentalSettings.EnableAILab;
            RoundedWebViewToggle.IsOn = ExperimentalSettings.EnableRoundedWebView;
            WinUIContextMenuToggle.IsOn = ExperimentalSettings.EnableWinUIContextMenu;

            AILabToggle.Toggled += OnAILabToggled;
            RoundedWebViewToggle.Toggled += OnRoundedWebViewToggled;
            WinUIContextMenuToggle.Toggled += OnWinUIContextMenuToggled;
        }

        private void OnBackButtonToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableBackButton = toggle.IsOn;
                RaiseBackButtonSettingsChanged();
            }
        }

        private void OnAILabToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableAILab = toggle.IsOn;
                RaiseAILabSettingsChanged();
            }
        }

        private void OnRoundedWebViewToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableRoundedWebView = toggle.IsOn;
                RaiseRoundedWebViewSettingsChanged();
            }
        }

        private void OnWinUIContextMenuToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableWinUIContextMenu = toggle.IsOn;
                RaiseWinUIContextMenuSettingsChanged();
            }
        }

        private void OnLabCardClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LabPage), null, new Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo
            {
                Effect = Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionEffect.FromRight
            });
        }

        // Event to notify when rounded webview settings change
        public static event EventHandler? RoundedWebViewSettingsChanged;
        internal static void RaiseRoundedWebViewSettingsChanged() => RoundedWebViewSettingsChanged?.Invoke(null, EventArgs.Empty);

        // Event to notify when WinUI context menu settings change
        public static event EventHandler? WinUIContextMenuSettingsChanged;
        internal static void RaiseWinUIContextMenuSettingsChanged() => WinUIContextMenuSettingsChanged?.Invoke(null, EventArgs.Empty);

        // Event to notify when AI Lab settings change
        public static event EventHandler? AILabSettingsChanged;
        internal static void RaiseAILabSettingsChanged() => AILabSettingsChanged?.Invoke(null, EventArgs.Empty);

        // Event to notify when back button settings change
        public static event EventHandler? BackButtonSettingsChanged;
        internal static void RaiseBackButtonSettingsChanged() => BackButtonSettingsChanged?.Invoke(null, EventArgs.Empty);

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

        private void OnLanguageCardClick(object sender, RoutedEventArgs e)
        {
            LanguageComboBox.IsDropDownOpen = true;
        }

        private async void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.XamlRoot == null) return;

            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var languageTag = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(languageTag))
                {
                    var currentLanguage = ApplicationLanguages.PrimaryLanguageOverride;
                    if (string.IsNullOrEmpty(currentLanguage))
                        currentLanguage = ApplicationLanguages.Languages[0];

                    if (languageTag != currentLanguage)
                    {
                        ApplicationLanguages.PrimaryLanguageOverride = languageTag;

                        var dialog = CreateMessageDialog(
                            LocalizationHelper.GetString("SettingsPage_RestartTitle"),
                            LocalizationHelper.GetString("SettingsPage_RestartContent"),
                            LocalizationHelper.GetString("SettingsPage_RestartButton"),
                            LocalizationHelper.GetString("SettingsPage_LaterButton"),
                            ContentDialogButton.Primary);
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
                    var dialog = CreateMessageDialog(
                        LocalizationHelper.GetString("SettingsPage_ErrorTitle"),
                        LocalizationHelper.GetString("SettingsPage_StartupToggleError"),
                        closeButtonText: LocalizationHelper.GetString("SettingsPage_ConfirmButton"));
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
                    var dialog = CreateMessageDialog(
                        LocalizationHelper.GetString("SettingsPage_ErrorTitle"),
                        LocalizationHelper.GetString("SettingsPage_OpenSettingsError"),
                        closeButtonText: LocalizationHelper.GetString("SettingsPage_ConfirmButton"));
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
            VirtualKey tempKey = VirtualKey.None;
            bool tempCtrl = false;
            bool tempAlt = false;
            bool tempShift = false;
            bool tempWin = false;
            bool isCapturingHotkey = false;

            var displayText = new TextBlock
            {
                Text = LocalizationHelper.GetString("SettingsPage_HotkeyDialogRecordText.Text"),
                FontSize = 16,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            string GetHotkeyDisplayText(VirtualKey key, bool ctrl, bool alt, bool shift, bool win)
            {
                var parts = new List<string>();
                if (ctrl) parts.Add("Ctrl");
                if (alt) parts.Add("Alt");
                if (shift) parts.Add("Shift");
                if (win) parts.Add("Win");
                if (key != VirtualKey.None) parts.Add(GetKeyDisplayName(key));
                return parts.Count > 0 ? string.Join(" + ", parts) : "未设置";
            }

            var recordButton = new ToggleButton
            {
                MinHeight = 80,
                Padding = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = displayText
            };

            recordButton.Checked += (_, _) =>
            {
                isCapturingHotkey = true;
                tempKey = VirtualKey.None;
                tempCtrl = tempAlt = tempShift = tempWin = false;
                displayText.Text = "按下快捷键...";
            };

            recordButton.Unchecked += (_, _) => isCapturingHotkey = false;

            recordButton.PreviewKeyDown += (_, args) =>
            {
                if (!isCapturingHotkey || recordButton.IsChecked != true)
                {
                    return;
                }

                args.Handled = true;
                var key = args.Key;

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

                if (key == VirtualKey.Control || key == VirtualKey.Menu ||
                    key == VirtualKey.Shift || key == VirtualKey.LeftWindows || key == VirtualKey.RightWindows)
                {
                    return;
                }

                if (!ctrl && !alt && !shift && !win)
                {
                    displayText.Text = "需要至少一个修饰键";
                    return;
                }

                tempKey = key;
                tempCtrl = ctrl;
                tempAlt = alt;
                tempShift = shift;
                tempWin = win;
                displayText.Text = GetHotkeyDisplayText(key, ctrl, alt, shift, win);
            };

            recordButton.PreviewKeyUp += (_, args) =>
            {
                if (!isCapturingHotkey || recordButton.IsChecked != true)
                {
                    return;
                }

                args.Handled = true;

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

                if (tempKey != VirtualKey.None && !anyModifierPressed)
                {
                    recordButton.IsChecked = false;
                }
            };

            var content = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = LocalizationHelper.GetString("SettingsPage_HotkeyDialogPrompt.Text"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    recordButton,
                    new TextBlock
                    {
                        Text = LocalizationHelper.GetString("SettingsPage_HotkeyDialogHint.Text"),
                        FontSize = 12,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            var dialog = new UnifiedInAppDialog();
            dialog.Configure(
                LocalizationHelper.GetString("SettingsPage_HotkeyDialog.Title"),
                content,
                LocalizationHelper.GetString("SettingsPage_HotkeyDialog.PrimaryButtonText"),
                LocalizationHelper.GetString("SettingsPage_HotkeyDialog.CloseButtonText"),
                defaultButton: ContentDialogButton.Primary);

            var result = await InAppDialogService.ShowAsync(dialog, this);
            isCapturingHotkey = false;

            if (result != ContentDialogResult.Primary || tempKey == VirtualKey.None)
            {
                return;
            }

            _hotkeySettings.Key = tempKey;
            _hotkeySettings.Ctrl = tempCtrl;
            _hotkeySettings.Alt = tempAlt;
            _hotkeySettings.Shift = tempShift;
            _hotkeySettings.Win = tempWin;

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
                    var dialog = CreateMessageDialog(
                        "错误",
                        "无法打开触摸板设置页面。",
                        closeButtonText: "确定");
                    await InAppDialogService.ShowAsync(dialog, this);
                }
            }
        }

        private static UnifiedInAppDialog CreateMessageDialog(
            string title,
            string message,
            string? primaryButtonText = null,
            string? closeButtonText = null,
            ContentDialogButton defaultButton = ContentDialogButton.Close)
        {
            var dialog = new UnifiedInAppDialog();
            dialog.Configure(
                title,
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14
                },
                primaryButtonText,
                closeButtonText,
                defaultButton: defaultButton);
            return dialog;
        }

        private static UnifiedInAppDialog CreateExternalOpenDialog(Uri uri)
        {
            var dialog = new UnifiedInAppDialog();
            dialog.Configure(
                LocalizationHelper.GetString("InAppDialog_OpenExternal_Title"),
                new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = LocalizationHelper.GetString("InAppDialog_OpenExternal_Content"),
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
                LocalizationHelper.GetString("InAppDialog_OpenExternal_OpenButton"),
                LocalizationHelper.GetString("InAppDialog_OpenExternal_CancelButton"),
                defaultButton: ContentDialogButton.Primary);
            return dialog;
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
