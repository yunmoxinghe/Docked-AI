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
using Docked_AI.功能.统一调用.应用评价;

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
            LoadTrayCloseWindowBehaviorSettings();
            
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
            // 空字符串表示跟随系统
            if (string.IsNullOrEmpty(currentLanguage))
            {
                currentLanguage = "";
            }

            LanguageComboBox.SelectionChanged -= OnLanguageChanged;

            bool found = false;
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                var tag = item.Tag?.ToString() ?? "";
                if (tag == currentLanguage)
                {
                    LanguageComboBox.SelectedItem = item;
                    found = true;
                    break;
                }
            }

            // 如果没找到匹配项，尝试简化的语言标签匹配
            if (!found && !string.IsNullOrEmpty(currentLanguage) && currentLanguage.Contains("-"))
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

            // 如果还是没找到，默认选择"跟随系统"（第一项）
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

        private void LoadExperimentalSettings()
        {
            // 加载返回按钮设置
            if (BackButtonToggle != null)
            {
                BackButtonToggle.Toggled -= OnBackButtonToggled;
                BackButtonToggle.IsOn = ExperimentalSettings.EnableBackButton;
                BackButtonToggle.Toggled += OnBackButtonToggled;
            }

            // 加载停靠位置设置
            if (DockSideComboBox != null)
            {
                DockSideComboBox.SelectionChanged -= OnDockSideChanged;
                DockSideComboBox.SelectedIndex = (int)ExperimentalSettings.DockSide;
                DockSideComboBox.SelectionChanged += OnDockSideChanged;
            }

            if (LeftDockNavigationToggle != null)
            {
                LeftDockNavigationToggle.Toggled -= OnLeftDockNavigationToggled;
                LeftDockNavigationToggle.IsOn = ExperimentalSettings.PlaceNavigationBarOnLeftWhenDockedLeft;
                LeftDockNavigationToggle.IsEnabled = ExperimentalSettings.DockSide == WindowDockSide.Left;
                LeftDockNavigationToggle.Toggled += OnLeftDockNavigationToggled;
            }
        }

        private void OnBackButtonToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableBackButton = toggle.IsOn;
                RaiseBackButtonSettingsChanged();
            }
        }

        private void OnBackButtonCardClick(object sender, RoutedEventArgs e)
        {
            // 点击卡片时切换 ToggleSwitch 状态
            BackButtonToggle.IsOn = !BackButtonToggle.IsOn;
        }

        private void OnLabCardClick(object sender, RoutedEventArgs e)
        {
            // 使用 EntranceNavigationTransitionInfo（官方推荐的轻量级动画）
            // 或使用 SuppressNavigationTransitionInfo 完全禁用动画以获得最快速度
            Frame.Navigate(typeof(LabPage), null, new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
        }

        // Event to notify when WinUI context menu settings change
        public static event EventHandler? WinUIContextMenuSettingsChanged;
        internal static void RaiseWinUIContextMenuSettingsChanged() => WinUIContextMenuSettingsChanged?.Invoke(null, EventArgs.Empty);

        // Event to notify when AI Lab settings change
        public static event EventHandler? AILabSettingsChanged;
        internal static void RaiseAILabSettingsChanged() => AILabSettingsChanged?.Invoke(null, EventArgs.Empty);

        // Event to notify when back button settings change
        public static event EventHandler? BackButtonSettingsChanged;
        internal static void RaiseBackButtonSettingsChanged() => BackButtonSettingsChanged?.Invoke(null, EventArgs.Empty);

        // Event to notify when dock side settings change
        public static event EventHandler? DockSideSettingsChanged;
        internal static void RaiseDockSideSettingsChanged() => DockSideSettingsChanged?.Invoke(null, EventArgs.Empty);

        private async void OnDockSideChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Tag?.ToString(), out int dockSideValue))
                {
                    var newDockSide = (WindowDockSide)dockSideValue;
                    var currentDockSide = ExperimentalSettings.DockSide;
                    if (LeftDockNavigationToggle != null)
                    {
                        LeftDockNavigationToggle.IsEnabled = newDockSide == WindowDockSide.Left;
                    }

                    if (newDockSide != currentDockSide)
                    {
                        // 保存设置
                        ExperimentalSettings.DockSide = newDockSide;

                        // 显示提示对话框
                        var dialog = CreateMessageDialog(
                            LocalizationHelper.GetString("SettingsPage_DockSideChangedTitle") ?? "窗口位置已更改",
                            LocalizationHelper.GetString("SettingsPage_DockSideChangedContent") ?? "窗口停靠位置已更改。下次显示窗口时将应用新的位置。",
                            closeButtonText: LocalizationHelper.GetString("SettingsPage_ConfirmButton") ?? "确定");
                        await InAppDialogService.ShowAsync(dialog, this);

                        // 通知应用更新窗口位置
                        RaiseDockSideSettingsChanged();
                    }
                }
            }
        }

        private void OnLeftDockNavigationToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.PlaceNavigationBarOnLeftWhenDockedLeft = toggle.IsOn;
                RaiseDockSideSettingsChanged();
            }
        }

        private void LoadWebSettings()
        {
            // 暂时取消事件订阅，避免在初始化时触发
            MaxWebViewCountBox.ValueChanged -= OnMaxWebViewCountChanged;
            
            MaxWebViewCountBox.Value = ExperimentalSettings.MaxWebViewCount;
            
            // 重新订阅事件
            MaxWebViewCountBox.ValueChanged += OnMaxWebViewCountChanged;
            
            // 加载性能优化设置
            LoadWebViewPerformanceSettings();
        }

        private void LoadWebViewPerformanceSettings()
        {
            // 快速启动模式
            FastStartupModeToggle.Toggled -= OnFastStartupModeToggled;
            FastStartupModeToggle.IsOn = ExperimentalSettings.FastStartupMode;
            FastStartupModeToggle.Toggled += OnFastStartupModeToggled;

            // 单进程模式
            SingleProcessModeToggle.Toggled -= OnSingleProcessModeToggled;
            SingleProcessModeToggle.IsOn = ExperimentalSettings.SingleProcessMode;
            SingleProcessModeToggle.Toggled += OnSingleProcessModeToggled;

            // 内存模式
            MemoryModeComboBox.SelectionChanged -= OnMemoryModeChanged;
            MemoryModeComboBox.SelectedIndex = (int)ExperimentalSettings.MemoryMode;
            MemoryModeComboBox.SelectionChanged += OnMemoryModeChanged;

            // 自动清理缓存
            AutoClearCacheToggle.Toggled -= OnAutoClearCacheToggled;
            AutoClearCacheToggle.IsOn = ExperimentalSettings.AutoClearCache;
            AutoClearCacheToggle.Toggled += OnAutoClearCacheToggled;

            // 暂停不活跃的 WebView
            SuspendInactiveToggle.Toggled -= OnSuspendInactiveToggled;
            SuspendInactiveToggle.IsOn = ExperimentalSettings.SuspendInactiveWebView;
            SuspendInactiveToggle.Toggled += OnSuspendInactiveToggled;

            // 禁用后台网络
            DisableBackgroundNetworkToggle.Toggled -= OnDisableBackgroundNetworkToggled;
            DisableBackgroundNetworkToggle.IsOn = ExperimentalSettings.DisableBackgroundNetwork;
            DisableBackgroundNetworkToggle.Toggled += OnDisableBackgroundNetworkToggled;

            // 禁用扩展
            DisableExtensionsToggle.Toggled -= OnDisableExtensionsToggled;
            DisableExtensionsToggle.IsOn = ExperimentalSettings.DisableExtensions;
            DisableExtensionsToggle.Toggled += OnDisableExtensionsToggled;

            // 禁用插件
            DisablePluginsToggle.Toggled -= OnDisablePluginsToggled;
            DisablePluginsToggle.IsOn = ExperimentalSettings.DisablePlugins;
            DisablePluginsToggle.Toggled += OnDisablePluginsToggled;

            // 磁盘缓存大小
            DiskCacheSizeBox.ValueChanged -= OnDiskCacheSizeChanged;
            DiskCacheSizeBox.Value = ExperimentalSettings.DiskCacheSize;
            DiskCacheSizeBox.ValueChanged += OnDiskCacheSizeChanged;

            // GPU 优化设置
            EnableHardwareAccelerationToggle.Toggled -= OnEnableHardwareAccelerationToggled;
            EnableHardwareAccelerationToggle.IsOn = ExperimentalSettings.EnableHardwareAcceleration;
            EnableHardwareAccelerationToggle.Toggled += OnEnableHardwareAccelerationToggled;

            EnableHardwareOverlaysToggle.Toggled -= OnEnableHardwareOverlaysToggled;
            EnableHardwareOverlaysToggle.IsOn = ExperimentalSettings.EnableHardwareOverlays;
            EnableHardwareOverlaysToggle.Toggled += OnEnableHardwareOverlaysToggled;

            EnableHardwareVideoDecoderToggle.Toggled -= OnEnableHardwareVideoDecoderToggled;
            EnableHardwareVideoDecoderToggle.IsOn = ExperimentalSettings.EnableHardwareVideoDecoder;
            EnableHardwareVideoDecoderToggle.Toggled += OnEnableHardwareVideoDecoderToggled;

            DisableSoftwareRasterizerToggle.Toggled -= OnDisableSoftwareRasterizerToggled;
            DisableSoftwareRasterizerToggle.IsOn = ExperimentalSettings.DisableSoftwareRasterizer;
            DisableSoftwareRasterizerToggle.Toggled += OnDisableSoftwareRasterizerToggled;
        }

        private void OnMemoryModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Tag?.ToString(), out int modeValue))
                {
                    ExperimentalSettings.MemoryMode = (WebViewMemoryMode)modeValue;
                    RaiseWebViewPerformanceSettingsChanged();
                }
            }
        }

        private void OnFastStartupModeToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.FastStartupMode = toggle.IsOn;
                RaiseWebViewPerformanceSettingsChanged();
            }
        }

        private void OnSingleProcessModeToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.SingleProcessMode = toggle.IsOn;
                RaiseWebViewPerformanceSettingsChanged();
            }
        }

        private void OnAutoClearCacheToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.AutoClearCache = toggle.IsOn;
                RaiseWebViewPerformanceSettingsChanged();
            }
        }

        private void OnSuspendInactiveToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.SuspendInactiveWebView = toggle.IsOn;
                RaiseWebViewPerformanceSettingsChanged();
            }
        }

        private void OnDisableBackgroundNetworkToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.DisableBackgroundNetwork = toggle.IsOn;
                RaiseWebViewPerformanceSettingsChanged();
            }
        }

        private void OnDisableExtensionsToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.DisableExtensions = toggle.IsOn;
                RaiseWebViewPerformanceSettingsChanged();
            }
        }

        private void OnDisablePluginsToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.DisablePlugins = toggle.IsOn;
                RaiseWebViewPerformanceSettingsChanged();
            }
        }

        private void OnDiskCacheSizeChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (!double.IsNaN(args.NewValue))
            {
                int newValue = (int)args.NewValue;
                ExperimentalSettings.DiskCacheSize = newValue;
                RaiseWebViewPerformanceSettingsChanged();
            }
        }

        private void OnEnableHardwareAccelerationToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableHardwareAcceleration = toggle.IsOn;
                RaiseWebViewPerformanceSettingsChanged();
            }
        }

        private void OnEnableHardwareOverlaysToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableHardwareOverlays = toggle.IsOn;
                RaiseWebViewPerformanceSettingsChanged();
            }
        }

        private void OnEnableHardwareVideoDecoderToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableHardwareVideoDecoder = toggle.IsOn;
                RaiseWebViewPerformanceSettingsChanged();
            }
        }

        private void OnDisableSoftwareRasterizerToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.DisableSoftwareRasterizer = toggle.IsOn;
                RaiseWebViewPerformanceSettingsChanged();
            }
        }

        private void RaiseWebViewPerformanceSettingsChanged()
        {
            WebViewPerformanceSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        // Event to notify when WebView performance settings change
        public static event EventHandler? WebViewPerformanceSettingsChanged;

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

        private void OnMaxWebViewCardClick(object sender, RoutedEventArgs e)
        {
            // 点击卡片时聚焦到 NumberBox
            MaxWebViewCountBox.Focus(FocusState.Programmatic);
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

        private void LoadTrayCloseWindowBehaviorSettings()
        {
            // 暂时取消事件订阅，避免在初始化时触发
            TrayCloseWindowBehaviorComboBox.SelectionChanged -= OnTrayCloseWindowBehaviorChanged;
            
            var currentBehavior = ExperimentalSettings.CloseWindowBehavior;
            TrayCloseWindowBehaviorComboBox.SelectedIndex = (int)currentBehavior;
            
            // 重新订阅事件
            TrayCloseWindowBehaviorComboBox.SelectionChanged += OnTrayCloseWindowBehaviorChanged;
        }

        private void OnTrayCloseWindowBehaviorCardClick(object sender, RoutedEventArgs e)
        {
            // 点击卡片时打开 ComboBox 的下拉菜单
            TrayCloseWindowBehaviorComboBox.IsDropDownOpen = true;
        }

        private void OnTrayCloseWindowBehaviorChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Tag?.ToString(), out int behaviorValue))
                {
                    var newBehavior = (TrayCloseWindowBehavior)behaviorValue;
                    ExperimentalSettings.CloseWindowBehavior = newBehavior;
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Tray close window behavior changed to: {newBehavior}");
                }
            }
        }

        private void OnLanguageCardClick(object sender, RoutedEventArgs e)
        {
            LanguageComboBox.IsDropDownOpen = true;
        }

        private async void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.XamlRoot == null) return;

            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var languageTag = selectedItem.Tag?.ToString() ?? "";
                var currentLanguage = ApplicationLanguages.PrimaryLanguageOverride;

                if (languageTag != currentLanguage)
                {
                    // 设置语言（空字符串表示跟随系统）
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
                displayText.Text = LocalizationHelper.GetString("SettingsPage_HotkeyRecording");
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
                    displayText.Text = LocalizationHelper.GetString("SettingsPage_HotkeyNeedModifier");
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

        // 评价应用
        private async void OnRateAppClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await StoreRatingService.RequestRatingAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Rate app failed: {ex.Message}");
                
                // 显示错误提示
                if (this.XamlRoot != null)
                {
                    var dialog = CreateMessageDialog(
                        LocalizationHelper.GetString("SettingsPage_ErrorTitle"),
                        "无法打开评价功能，请稍后重试。",
                        closeButtonText: LocalizationHelper.GetString("SettingsPage_ConfirmButton"));
                    await InAppDialogService.ShowAsync(dialog, this);
                }
            }
        }

        // 赞助作者
        private async void OnSponsorClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = CreateSponsorDialog();
                await InAppDialogService.ShowAsync(dialog, this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Show sponsor dialog failed: {ex.Message}");
            }
        }

        private static UnifiedInAppDialog CreateSponsorDialog()
        {
            // 创建包含两个收款码的布局
            var stackPanel = new StackPanel
            {
                Spacing = 24,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 添加说明文字
            var descriptionText = new TextBlock
            {
                Text = LocalizationHelper.GetString("SettingsPage_SponsorDescription") ?? "感谢您的支持！您可以通过以下方式赞助：",
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stackPanel.Children.Add(descriptionText);

            // 创建收款码容器（水平排列）
            var qrCodesPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 32,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 微信收款码
            var wechatPanel = CreateQRCodePanel(
                "ms-appx:///Assets/赞助/微信.png",
                LocalizationHelper.GetString("SettingsPage_WeChatPay") ?? "微信支付"
            );
            qrCodesPanel.Children.Add(wechatPanel);

            // 支付宝收款码
            var alipayPanel = CreateQRCodePanel(
                "ms-appx:///Assets/赞助/支付宝.jpg",
                LocalizationHelper.GetString("SettingsPage_Alipay") ?? "支付宝"
            );
            qrCodesPanel.Children.Add(alipayPanel);

            stackPanel.Children.Add(qrCodesPanel);

            // 使用统一弹窗接口
            var dialog = new UnifiedInAppDialog();
            dialog.Configure(
                LocalizationHelper.GetString("SettingsPage_SponsorDialogTitle") ?? "赞助作者",
                stackPanel,
                primaryButtonText: null,
                closeButtonText: LocalizationHelper.GetString("SettingsPage_CloseButton") ?? "关闭",
                defaultButton: ContentDialogButton.Close);
            
            return dialog;
        }

        private static StackPanel CreateQRCodePanel(string imageSource, string label)
        {
            var panel = new StackPanel
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 收款码图片
            var image = new Microsoft.UI.Xaml.Controls.Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(imageSource)),
                Width = 200,
                Height = 200,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform
            };
            panel.Children.Add(image);

            // 标签文字
            var textBlock = new TextBlock
            {
                Text = label,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            panel.Children.Add(textBlock);

            return panel;
        }
    }
}
