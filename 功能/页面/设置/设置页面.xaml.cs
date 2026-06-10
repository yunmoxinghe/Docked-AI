using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
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
        private bool _hasPlayedEntranceAnimation = false; // 标记是否已播放入场动画

        // ViewModel for startup settings
        public StartupSettingsViewModel ViewModel { get; private set; } = null!;

        // Hotkey management
        private HotkeySettings _hotkeySettings = null!;
        private readonly 智能标题 _智能标题 = new();
        
        public SettingsPage()
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] Constructor started at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            
            // BEST PRACTICE: Initialize dependencies BEFORE InitializeComponent
            // This ensures x:Bind expressions have valid references
            InitializeDependencies();
            
            // Initialize XAML components (this evaluates x:Bind expressions)
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[SettingsPage] InitializeComponent completed");
            
            // Register event handlers
            RegisterEventHandlers();
            
            System.Diagnostics.Debug.WriteLine("[SettingsPage] Constructor completed successfully");
        }
        
        /// <summary>
        /// Initialize all dependencies with proper error handling
        /// BEST PRACTICE: Use constructor injection in production (requires DI container)
        /// </summary>
        private void InitializeDependencies()
        {
            // Initialize ViewModel with proper error handling
            // BEST PRACTICE: In production, inject via constructor from DI container
            try
            {
                var startupManager = new StartupTaskManager();
                ViewModel = new StartupSettingsViewModel(startupManager);
                System.Diagnostics.Debug.WriteLine("[SettingsPage] ViewModel initialized");
            }
            catch (Exception vmEx)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] ERROR initializing ViewModel: {vmEx}");
                // BEST PRACTICE: Create a fallback ViewModel instead of null
                // This prevents x:Bind crashes and maintains app stability
                ViewModel = CreateFallbackViewModel();
            }

            // Initialize hotkey settings
            try
            {
                _hotkeySettings = new HotkeySettings();
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Hotkey settings initialized");
            }
            catch (Exception hkEx)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] ERROR initializing hotkey settings: {hkEx}");
                // Create default instance - constructor already provides safe defaults
                _hotkeySettings = new HotkeySettings();
            }
        }
        
        /// <summary>
        /// Creates a fallback ViewModel with safe defaults when primary initialization fails
        /// BEST PRACTICE: Always provide a valid object instead of null to prevent crashes
        /// </summary>
        private StartupSettingsViewModel CreateFallbackViewModel()
        {
            try
            {
                // Try to create with null manager - ViewModel should handle gracefully
                return new StartupSettingsViewModel(null!);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to create fallback ViewModel: {ex}");
                // Last resort: rethrow - we need a valid ViewModel for x:Bind
                throw new InvalidOperationException(
                    "Failed to initialize SettingsPage ViewModel. The page cannot function without it.", ex);
            }
        }
        
        /// <summary>
        /// Register all event handlers in one place for better maintainability
        /// BEST PRACTICE: Centralize event registration for easier debugging and cleanup
        /// </summary>
        private void RegisterEventHandlers()
        {
            try
            {
                // Page lifecycle events
                Loaded += OnLoaded;
                SizeChanged += OnSizeChanged;
                
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Event handlers registered");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] ERROR registering event handlers: {ex}");
                // Continue execution - event handlers are not critical for initial load
            }
        }
        
        /// <summary>
        /// Safely load all settings with isolated exception handling per setting group
        /// BEST PRACTICE: Each setting load is independent to prevent cascade failures
        /// </summary>
        private void SafeLoadSettings()
        {
            // BEST PRACTICE: Use a list of delegates for cleaner iteration
            var settingLoaders = new (string name, Action loader)[]
            {
                ("Hotkey", LoadHotkeySettings),
                ("Experimental", LoadExperimentalSettings),
                ("Web", LoadWebSettings),
                ("TrayCloseWindowBehavior", LoadTrayCloseWindowBehaviorSettings),
                ("ContentAreaBackdrop", LoadContentAreaBackdropSettings)
            };

            foreach (var (name, loader) in settingLoaders)
            {
                try
                {
                    loader();
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] {name} settings loaded");
                }
                catch (Exception ex)
                {
                    // Log but continue - one failing setting shouldn't break all others
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to load {name} settings: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Stack: {ex.StackTrace}");
                }
            }
            
            // Initialize startup settings asynchronously with exception handling
            _ = InitializeStartupSettingsAsync();
        }

        private string GetGitHubLinkText()
        {
            try
            {
                var text = LocalizationHelper.GetString("SettingsPage_GitHubLink/Content");
                if (string.IsNullOrEmpty(text))
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] GitHub link text not found in localization, using default");
                    return "GitHub Repository";
                }
                return text;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to get GitHub link text: {ex}");
                return "GitHub Repository";
            }
        }

        private string GetFeedbackLinkText()
        {
            try
            {
                var text = LocalizationHelper.GetString("SettingsPage_FeedbackLink/Content");
                if (string.IsNullOrEmpty(text))
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] Feedback link text not found in localization, using default");
                    return "Send Feedback";
                }
                return text;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to get feedback link text: {ex}");
                return "Send Feedback";
            }
        }

        /// <summary>
        /// Asynchronously initialize startup settings
        /// BEST PRACTICE: All async operations should have proper error handling and null checks
        /// </summary>
        private async System.Threading.Tasks.Task InitializeStartupSettingsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Starting async startup settings initialization");
                
                // BEST PRACTICE: Validate preconditions before async operations
                if (DispatcherQueue == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: DispatcherQueue is null, cannot initialize startup settings");
                    return;
                }
                
                // ViewModel should never be null after constructor (non-nullable type)
                // But defensive check doesn't hurt
                if (ViewModel == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] CRITICAL: ViewModel is null after initialization");
                    return;
                }
                
                await ViewModel.InitializeAsync();
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Startup settings initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to initialize startup settings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Stack: {ex.StackTrace}");
                
                // BEST PRACTICE: Use fire-and-forget pattern for non-critical notifications
                _ = ShowErrorNotificationAsync("启动设置加载失败", ex.Message);
            }
        }

        /// <summary>
        /// Load language settings with proper null checks and thread safety
        /// BEST PRACTICE: Always validate UI controls before accessing them
        /// </summary>
        private void LoadLanguageSettings()
        {
            try
            {
                // BEST PRACTICE: Validate all preconditions at the start
                if (!ValidateUIContext())
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] Invalid UI context for LoadLanguageSettings");
                    return;
                }
                
                if (LanguageComboBox == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: LanguageComboBox is null");
                    return;
                }
                
                if (LanguageComboBox.Items == null || LanguageComboBox.Items.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: LanguageComboBox has no items");
                    return;
                }
                
                var currentLanguage = ApplicationLanguages.PrimaryLanguageOverride;
                // Empty string means follow system
                if (string.IsNullOrEmpty(currentLanguage))
                {
                    currentLanguage = "";
                }

                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Current language: {currentLanguage}");

                // BEST PRACTICE: Unsubscribe before making changes to prevent unwanted events
                LanguageComboBox.SelectionChanged -= OnLanguageChanged;

                // Try to find matching language
                bool found = TrySetLanguageSelection(currentLanguage);

                // BEST PRACTICE: Always resubscribe in a finally block (done implicitly here)
                LanguageComboBox.SelectionChanged += OnLanguageChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to load language settings: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Validates that we're in a valid UI context
        /// BEST PRACTICE: Centralize validation logic
        /// </summary>
        private bool ValidateUIContext()
        {
            if (DispatcherQueue == null)
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: DispatcherQueue is null");
                return false;
            }
            return true;
        }
        
        /// <summary>
        /// Try to set language selection in ComboBox
        /// BEST PRACTICE: Separate selection logic for better testability
        /// </summary>
        private bool TrySetLanguageSelection(string languageTag)
        {
            if (LanguageComboBox == null) return false;
            
            // Try exact match first
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item == null) continue;
                
                var tag = item.Tag?.ToString() ?? "";
                if (tag == languageTag)
                {
                    LanguageComboBox.SelectedItem = item;
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Found exact language match: {tag}");
                    return true;
                }
            }

            // Try simplified tag (e.g., "zh-Hans-CN" -> "zh-CN")
            if (!string.IsNullOrEmpty(languageTag) && languageTag.Contains("-"))
            {
                var parts = languageTag.Split('-');
                if (parts.Length == 3)
                {
                    var simplifiedTag = $"{parts[0]}-{parts[2]}";
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Trying simplified language tag: {simplifiedTag}");
                    
                    foreach (ComboBoxItem item in LanguageComboBox.Items)
                    {
                        if (item == null) continue;
                        
                        if (item.Tag?.ToString() == simplifiedTag)
                        {
                            LanguageComboBox.SelectedItem = item;
                            System.Diagnostics.Debug.WriteLine($"[SettingsPage] Found simplified language match: {simplifiedTag}");
                            return true;
                        }
                    }
                }
            }

            // Default to "Follow System" (first item)
            System.Diagnostics.Debug.WriteLine("[SettingsPage] No language match found, using system default (index 0)");
            LanguageComboBox.SelectedIndex = 0;
            return false;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] OnLoaded started at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                
                // 🎯 CRITICAL: 立即隐藏卡片以防止闪烁
                // 必须在任何其他操作之前执行
                if (CardsPanel != null && !_hasPlayedEntranceAnimation)
                {
                    foreach (var child in CardsPanel.Children)
                    {
                        if (child != null)
                        {
                            child.Opacity = 0;
                        }
                    }
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] Cards hidden to prevent flicker");
                }
                
                // IMPORTANT: Load settings AFTER UI is fully loaded to prevent null reference crashes
                SafeLoadSettings();
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Settings loaded");
                
                UpdateVisualStateAndDiagnostic();
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Visual state updated");
                
                LoadVersionInfo();
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Version info loaded");
                
                // 在页面加载完成后初始化语言设置
                LoadLanguageSettings();
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Language settings loaded");
                
                // 初始化 Frame 动画设置
                LoadFrameAnimationSettings();
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Frame animation settings loaded");
                
                // 初始化子页面动画设置
                LoadSubPageAnimationSettings();
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Sub-page animation settings loaded");
                
                System.Diagnostics.Debug.WriteLine("[SettingsPage] OnLoaded completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] CRITICAL ERROR in OnLoaded: {ex}");
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Error type: {ex.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Stack trace: {ex.StackTrace}");
                
                // 发送详细的崩溃通知
                try
                {
                    Docked_AI.Features.MainWindow.Entry.DebugNotificationHelper.SendNotification(
                        "设置页面加载失败", 
                        $"错误类型: {ex.GetType().Name}\n错误: {ex.Message}");
                }
                catch (Exception notifyEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to send notification: {notifyEx}");
                }
                
                // 不要重新抛出异常，让页面尽可能继续运行
            }
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _智能标题.Setup(SettingsScrollViewer, PageTitleBlock);
            
            // 🎯 只在首次导航时播放手动交错动画
            if (e.NavigationMode == Microsoft.UI.Xaml.Navigation.NavigationMode.New && 
                !_hasPlayedEntranceAnimation)
            {
                // 延迟执行动画，确保 UI 元素已完全加载
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    StartStaggeredEntranceAnimation();
                });
            }
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
                if (VersionText == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: VersionText is null");
                    return;
                }
                
                var version = Package.Current.Id.Version;
                var versionString = $"{version.Major}.{version.Minor}.{version.Build}";
                
                // 获取本地化的版本前缀（如"版本："、"Version:"等）
                var versionPrefix = LocalizationHelper.GetString("SettingsPage_VersionPrefix");
                
                // 如果本地化字符串获取失败,使用默认值
                if (string.IsNullOrEmpty(versionPrefix))
                {
                    versionPrefix = "Version:";
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] Using default version prefix");
                }
                
                VersionText.Text = $"{versionPrefix}v{versionString}";
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Version info loaded: {VersionText.Text}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to load version info: {ex}");
                // 如果读取失败，设置一个默认值
                if (VersionText != null)
                {
                    VersionText.Text = "Version: Unknown";
                }
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
            // BEST PRACTICE: 不使用 ConfigureAwait(true) 避免页面导航后的潜在崩溃
            // 如果页面已经被 unload，强制回到 UI 线程可能访问已释放的资源
            await OpenExternalLinkAsync(
                "https://github.com/yunmoxinghe/Docked-AI",
                "无法打开GitHub");
        }

        private async void OnSendFeedbackClick(object sender, RoutedEventArgs args)
        {
            await OpenExternalLinkAsync(
                "https://github.com/yunmoxinghe/Docked-AI/issues",
                "无法打开反馈页面");
        }

        /// <summary>
        /// Unified helper for opening external links with user confirmation
        /// </summary>
        private async System.Threading.Tasks.Task OpenExternalLinkAsync(string url, string errorTitle)
        {
            try
            {
                var uri = new Uri(url);
                var dialog = CreateExternalOpenDialog(uri);
                var result = await InAppDialogService.ShowAsync(dialog, this);
                
                if (result == ContentDialogResult.Primary)
                {
                    var success = await Launcher.LaunchUriAsync(uri);
                    if (!success)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to launch URL: {url}");
                        await ShowErrorNotificationAsync(errorTitle, "无法打开链接");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] OpenExternalLink error: {ex}");
                await ShowErrorNotificationAsync(errorTitle, ex.Message);
            }
        }

        private void LoadExperimentalSettings()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Loading experimental settings");
                
                // 加载返回按钮设置
                if (BackButtonToggle != null)
                {
                    BackButtonToggle.Toggled -= OnBackButtonToggled;
                    BackButtonToggle.IsOn = ExperimentalSettings.EnableBackButton;
                    BackButtonToggle.Toggled += OnBackButtonToggled;
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Back button setting loaded: {ExperimentalSettings.EnableBackButton}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: BackButtonToggle is null");
                }

                // 加载停靠位置设置
                if (DockSideComboBox != null)
                {
                    DockSideComboBox.SelectionChanged -= OnDockSideChanged;
                    var dockSideIndex = (int)ExperimentalSettings.DockSide;
                    if (dockSideIndex >= 0 && dockSideIndex < DockSideComboBox.Items.Count)
                    {
                        DockSideComboBox.SelectedIndex = dockSideIndex;
                        System.Diagnostics.Debug.WriteLine($"[SettingsPage] Dock side setting loaded: {dockSideIndex}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[SettingsPage] WARNING: Invalid dock side index {dockSideIndex}, using default 0");
                        DockSideComboBox.SelectedIndex = 0;
                    }
                    DockSideComboBox.SelectionChanged += OnDockSideChanged;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: DockSideComboBox is null");
                }

                if (LeftDockNavigationToggle != null)
                {
                    LeftDockNavigationToggle.Toggled -= OnLeftDockNavigationToggled;
                    LeftDockNavigationToggle.IsOn = ExperimentalSettings.PlaceNavigationBarOnLeftWhenDockedLeft;
                    LeftDockNavigationToggle.IsEnabled = ExperimentalSettings.DockSide == WindowDockSide.Left;
                    LeftDockNavigationToggle.Toggled += OnLeftDockNavigationToggled;
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Left dock navigation setting loaded: {ExperimentalSettings.PlaceNavigationBarOnLeftWhenDockedLeft}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: LeftDockNavigationToggle is null");
                }
                
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Experimental settings loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to load experimental settings: {ex}");
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
            var animationType = ExperimentalSettings.SubPageNavigationAnimation;
            var transitionInfo = GetNavigationTransitionInfo(animationType);
            Frame.Navigate(typeof(LabPage), null, transitionInfo);
        }

        private void OnWebViewPerformanceCardClick(object sender, RoutedEventArgs e)
        {
            var animationType = ExperimentalSettings.SubPageNavigationAnimation;
            var transitionInfo = GetNavigationTransitionInfo(animationType);
            Frame.Navigate(typeof(WebViewPerformancePage), null, transitionInfo);
        }

        /// <summary>
        /// 根据动画类型获取对应的 NavigationTransitionInfo
        /// </summary>
        private Microsoft.UI.Xaml.Media.Animation.NavigationTransitionInfo GetNavigationTransitionInfo(FrameAnimationType animationType)
        {
            return animationType switch
            {
                FrameAnimationType.None => new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo(),
                FrameAnimationType.EntranceTransition => new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo(),
                FrameAnimationType.SlideFromRight => new Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo 
                { 
                    Effect = Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionEffect.FromRight 
                },
                FrameAnimationType.SlideFromLeft => new Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo 
                { 
                    Effect = Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionEffect.FromLeft 
                },
                FrameAnimationType.SlideFromBottom => new Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo 
                { 
                    Effect = Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionEffect.FromBottom 
                },
                FrameAnimationType.DrillIn => new Microsoft.UI.Xaml.Media.Animation.DrillInNavigationTransitionInfo(),
                FrameAnimationType.FadeInOut => new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo(),
                FrameAnimationType.ScaleAnimation => new Microsoft.UI.Xaml.Media.Animation.DrillInNavigationTransitionInfo(),
                _ => new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo()
            };
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
            try
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Loading web settings");
                
                if (MaxWebViewCountBox != null)
                {
                    MaxWebViewCountBox.ValueChanged -= OnMaxWebViewCountChanged;
                    MaxWebViewCountBox.Value = ExperimentalSettings.MaxWebViewCount;
                    MaxWebViewCountBox.ValueChanged += OnMaxWebViewCountChanged;
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Max WebView count loaded: {ExperimentalSettings.MaxWebViewCount}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: MaxWebViewCountBox is null");
                }

                if (HideWebViewCloseButtonToggle != null)
                {
                    HideWebViewCloseButtonToggle.Toggled -= OnHideWebViewCloseButtonToggled;
                    HideWebViewCloseButtonToggle.IsOn = ExperimentalSettings.HideWebViewCloseButton;
                    HideWebViewCloseButtonToggle.Toggled += OnHideWebViewCloseButtonToggled;
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Hide WebView close button loaded: {ExperimentalSettings.HideWebViewCloseButton}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: HideWebViewCloseButtonToggle is null");
                }
                
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Web settings loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to load web settings: {ex}");
            }
        }

        public static void RaiseWebViewPerformanceSettingsChanged()
        {
            WebViewPerformanceSettingsChanged?.Invoke(null, EventArgs.Empty);
        }

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

        private void OnHideWebViewCloseButtonToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch)
            {
                ExperimentalSettings.HideWebViewCloseButton = toggleSwitch.IsOn;
            }
        }

        private void OnHideWebViewCloseButtonCardClick(object sender, RoutedEventArgs e)
        {
            // 点击卡片时切换开关状态
            HideWebViewCloseButtonToggle.IsOn = !HideWebViewCloseButtonToggle.IsOn;
        }

        // Event to notify when max webview count settings change
        public static event EventHandler? MaxWebViewCountSettingsChanged;

        private void LoadFrameAnimationSettings()
        {
            try
            {
                if (FrameAnimationComboBox == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: FrameAnimationComboBox is null");
                    return;
                }
                
                // 暂时取消事件订阅，避免在初始化时触发
                FrameAnimationComboBox.SelectionChanged -= OnFrameAnimationChanged;
                
                var currentAnimation = ExperimentalSettings.FrameNavigationAnimation;
                var animationIndex = (int)currentAnimation;
                
                if (animationIndex >= 0 && animationIndex < FrameAnimationComboBox.Items.Count)
                {
                    FrameAnimationComboBox.SelectedIndex = animationIndex;
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Frame animation setting loaded: {currentAnimation}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] WARNING: Invalid frame animation index {animationIndex}, using default 1");
                    FrameAnimationComboBox.SelectedIndex = 1; // 默认使用 Entrance
                }
                
                // 重新订阅事件
                FrameAnimationComboBox.SelectionChanged += OnFrameAnimationChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to load frame animation settings: {ex}");
            }
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

        private void LoadSubPageAnimationSettings()
        {
            try
            {
                if (SubPageAnimationComboBox == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: SubPageAnimationComboBox is null");
                    return;
                }
                
                // 暂时取消事件订阅，避免在初始化时触发
                SubPageAnimationComboBox.SelectionChanged -= OnSubPageAnimationChanged;
                
                var currentAnimation = ExperimentalSettings.SubPageNavigationAnimation;
                var animationIndex = (int)currentAnimation;
                
                if (animationIndex >= 0 && animationIndex < SubPageAnimationComboBox.Items.Count)
                {
                    SubPageAnimationComboBox.SelectedIndex = animationIndex;
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Sub-page animation setting loaded: {currentAnimation}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] WARNING: Invalid sub-page animation index {animationIndex}, using default 1");
                    SubPageAnimationComboBox.SelectedIndex = 1; // 默认使用 Entrance
                }
                
                // 重新订阅事件
                SubPageAnimationComboBox.SelectionChanged += OnSubPageAnimationChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to load sub-page animation settings: {ex}");
            }
        }

        private void OnSubPageAnimationCardClick(object sender, RoutedEventArgs e)
        {
            // 点击卡片时打开 ComboBox 的下拉菜单
            SubPageAnimationComboBox.IsDropDownOpen = true;
        }

        private void OnSubPageAnimationChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Tag?.ToString(), out int animationType))
                {
                    var newAnimation = (FrameAnimationType)animationType;
                    ExperimentalSettings.SubPageNavigationAnimation = newAnimation;
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Sub-page animation changed to: {newAnimation}");
                }
            }
        }

        private void LoadTrayCloseWindowBehaviorSettings()
        {
            try
            {
                if (TrayCloseWindowBehaviorComboBox == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: TrayCloseWindowBehaviorComboBox is null");
                    return;
                }
                
                // 暂时取消事件订阅，避免在初始化时触发
                TrayCloseWindowBehaviorComboBox.SelectionChanged -= OnTrayCloseWindowBehaviorChanged;
                
                var currentBehavior = ExperimentalSettings.CloseWindowBehavior;
                var behaviorIndex = (int)currentBehavior;
                
                if (behaviorIndex >= 0 && behaviorIndex < TrayCloseWindowBehaviorComboBox.Items.Count)
                {
                    TrayCloseWindowBehaviorComboBox.SelectedIndex = behaviorIndex;
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Tray close window behavior setting loaded: {currentBehavior}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] WARNING: Invalid tray close behavior index {behaviorIndex}, using default 0");
                    TrayCloseWindowBehaviorComboBox.SelectedIndex = 0;
                }
                
                // 重新订阅事件
                TrayCloseWindowBehaviorComboBox.SelectionChanged += OnTrayCloseWindowBehaviorChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to load tray close window behavior settings: {ex}");
            }
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

        private void LoadContentAreaBackdropSettings()
        {
            try
            {
                if (ContentAreaBackdropComboBox == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: ContentAreaBackdropComboBox is null");
                    return;
                }
                
                // 暂时取消事件订阅，避免在初始化时触发
                ContentAreaBackdropComboBox.SelectionChanged -= OnContentAreaBackdropChanged;
                
                var currentBackdrop = ExperimentalSettings.ContentAreaBackdrop;
                var backdropIndex = (int)currentBackdrop;
                
                if (backdropIndex >= 0 && backdropIndex < ContentAreaBackdropComboBox.Items.Count)
                {
                    ContentAreaBackdropComboBox.SelectedIndex = backdropIndex;
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Content area backdrop setting loaded: {currentBackdrop}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] WARNING: Invalid backdrop index {backdropIndex}, using default 1");
                    ContentAreaBackdropComboBox.SelectedIndex = 1; // 默认云母材质
                }
                
                // 重新订阅事件
                ContentAreaBackdropComboBox.SelectionChanged += OnContentAreaBackdropChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to load content area backdrop settings: {ex}");
            }
        }

        private void OnContentAreaBackdropCardClick(object sender, RoutedEventArgs e)
        {
            // 点击卡片时打开 ComboBox 的下拉菜单
            ContentAreaBackdropComboBox.IsDropDownOpen = true;
        }

        private void OnContentAreaBackdropChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Tag?.ToString(), out int backdropValue))
                {
                    var newBackdrop = (ContentAreaBackdropType)backdropValue;
                    ExperimentalSettings.ContentAreaBackdrop = newBackdrop;
                    
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Content area backdrop changed to: {newBackdrop}");
                    
                    // 通知应用更新背景材质
                    ContentAreaBackdropSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // Event to notify when content area backdrop settings change
        public static event EventHandler? ContentAreaBackdropSettingsChanged;

        private void OnLanguageCardClick(object sender, RoutedEventArgs e)
        {
            LanguageComboBox.IsDropDownOpen = true;
        }

        private async void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            // Event handlers must be async void, but we delegate to async Task for better error handling
            await OnLanguageChangedAsync(sender, e).ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task OnLanguageChangedAsync(object sender, SelectionChangedEventArgs e)
        {
            // Early validation
            if (this.XamlRoot == null)
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] XamlRoot is null, cannot change language");
                return;
            }

            if (LanguageComboBox?.SelectedItem is not ComboBoxItem selectedItem)
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] No valid language item selected");
                return;
            }

            var languageTag = selectedItem.Tag?.ToString() ?? "";
            var currentLanguage = ApplicationLanguages.PrimaryLanguageOverride;

            if (languageTag == currentLanguage)
            {
                return; // No change needed
            }

            try
            {
                await ChangeLanguageAsync(languageTag, currentLanguage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] OnLanguageChanged error: {ex}");
                await ShowErrorNotificationAsync("语言切换失败", ex.Message);
            }
        }

        private async System.Threading.Tasks.Task ChangeLanguageAsync(string newLanguage, string currentLanguage)
        {
            try
            {
                // Set language (empty string means follow system)
                ApplicationLanguages.PrimaryLanguageOverride = newLanguage;
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Language changed to: {newLanguage}");

                var shouldRestart = await PromptForRestartAsync();
                if (shouldRestart)
                {
                    AppRestartService.RestartWithArgs("--restart-from=settings-language");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Error changing language: {ex}");
                
                // Rollback on failure
                await RollbackLanguageChangeAsync(currentLanguage);
                throw; // Re-throw to be handled by caller
            }
        }

        private async System.Threading.Tasks.Task<bool> PromptForRestartAsync()
        {
            var dialog = CreateMessageDialog(
                LocalizationHelper.GetString("SettingsPage_RestartTitle") ?? "重启应用",
                LocalizationHelper.GetString("SettingsPage_RestartContent") ?? "需要重启应用以应用语言更改",
                LocalizationHelper.GetString("SettingsPage_RestartButton") ?? "立即重启",
                LocalizationHelper.GetString("SettingsPage_LaterButton") ?? "稍后",
                ContentDialogButton.Primary);
            
            var result = await InAppDialogService.ShowAsync(dialog, this);
            return result == ContentDialogResult.Primary;
        }

        private async System.Threading.Tasks.Task RollbackLanguageChangeAsync(string previousLanguage)
        {
            try
            {
                ApplicationLanguages.PrimaryLanguageOverride = previousLanguage;
                LoadLanguageSettings(); // Reload UI state
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Language rolled back to: {previousLanguage}");
            }
            catch (Exception rollbackEx)
            {
                // Log rollback failure but don't throw - we're already in error handling
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to rollback language: {rollbackEx}");
            }
        }

        private async System.Threading.Tasks.Task ShowErrorNotificationAsync(string title, string message)
        {
            try
            {
                if (DispatcherQueue != null)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        Docked_AI.Features.MainWindow.Entry.DebugNotificationHelper.SendNotification(title, message);
                    });
                }
            }
            catch (Exception notifyEx)
            {
                // Log but don't throw - notification is not critical
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to send notification: {notifyEx}");
            }
        }

        // Event handlers for startup settings
        private async void OnToggleSwitched(object sender, RoutedEventArgs e)
        {
            await OnToggleSwitchedAsync(sender, e).ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task OnToggleSwitchedAsync(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch toggleSwitch)
            {
                return;
            }

            if (ViewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: ViewModel is null, cannot handle toggle");
                return;
            }

            try
            {
                await ViewModel.HandleToggleAsync(toggleSwitch.IsOn);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] OnToggleSwitched error: {ex}");
                await ShowErrorDialogAsync(
                    "SettingsPage_ErrorTitle",
                    "SettingsPage_StartupToggleError");
            }
        }

        private async void OnSettingCardClick(object sender, RoutedEventArgs e)
        {
            await OnSettingCardClickAsync().ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task OnSettingCardClickAsync()
        {
            if (ViewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: ViewModel is null, cannot navigate to settings");
                return;
            }

            try
            {
                await ViewModel.NavigateToSystemSettingsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] OnSettingCardClick error: {ex}");
                await ShowErrorDialogAsync(
                    "SettingsPage_ErrorTitle",
                    "SettingsPage_OpenSettingsError");
            }
        }

        /// <summary>
        /// Reusable helper to show localized error dialogs
        /// </summary>
        private async System.Threading.Tasks.Task ShowErrorDialogAsync(string titleKey, string messageKey)
        {
            if (this.XamlRoot == null)
            {
                return;
            }

            try
            {
                var dialog = CreateMessageDialog(
                    LocalizationHelper.GetString(titleKey) ?? "错误",
                    LocalizationHelper.GetString(messageKey) ?? "操作失败",
                    closeButtonText: LocalizationHelper.GetString("SettingsPage_ConfirmButton") ?? "确定");
                await InAppDialogService.ShowAsync(dialog, this);
            }
            catch (Exception dialogEx)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to show error dialog: {dialogEx}");
            }
        }

        // Hotkey settings methods
        private void LoadHotkeySettings()
        {
            try
            {
                if (_hotkeySettings == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: _hotkeySettings is null, creating new instance");
                    _hotkeySettings = new HotkeySettings();
                }
                
                if (HotkeyToggle == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: HotkeyToggle is null");
                    return;
                }
                
                // 暂时取消事件订阅，避免在初始化时触发
                HotkeyToggle.Toggled -= OnHotkeyToggled;
                
                HotkeyToggle.IsOn = _hotkeySettings.IsEnabled;
                UpdateHotkeyButtonText();
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Hotkey settings loaded: IsEnabled={_hotkeySettings.IsEnabled}");
                
                // 重新订阅事件
                HotkeyToggle.Toggled += OnHotkeyToggled;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to load hotkey settings: {ex}");
            }
        }

        private void UpdateHotkeyButtonText()
        {
            try
            {
                if (HotkeyKeysDisplay == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: HotkeyKeysDisplay is null");
                    return;
                }
                
                if (_hotkeySettings == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: Cannot update hotkey button text - _hotkeySettings is null");
                    HotkeyKeysDisplay.ItemsSource = new System.Collections.Generic.List<string>();
                    return;
                }
                
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to update hotkey button text: {ex}");
            }
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
            if (_hotkeySettings == null)
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] WARNING: Cannot configure hotkey - _hotkeySettings is null");
                return;
            }
            
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
            await OnTouchpadSettingsClickAsync().ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task OnTouchpadSettingsClickAsync()
        {
            try
            {
                var uri = new Uri("ms-settings:devices-touchpad");
                var success = await Launcher.LaunchUriAsync(uri);
                
                if (!success)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] Failed to launch touchpad settings");
                    await ShowErrorDialogAsync("SettingsPage_ErrorTitle", "SettingsPage_OpenSettingsError");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] OnTouchpadSettingsClick error: {ex}");
                await ShowErrorDialogAsync("SettingsPage_ErrorTitle", "SettingsPage_OpenSettingsError");
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
            await OnRateAppClickAsync().ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task OnRateAppClickAsync()
        {
            try
            {
                await StoreRatingService.RequestRatingAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Rate app failed: {ex.Message}");
                await ShowErrorDialogAsync("SettingsPage_ErrorTitle", "SettingsPage_RatingError");
            }
        }

        // 赞助作者
        private async void OnSponsorClick(object sender, RoutedEventArgs e)
        {
            await OnSponsorClickAsync().ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task OnSponsorClickAsync()
        {
            if (this.XamlRoot == null)
            {
                System.Diagnostics.Debug.WriteLine("[SettingsPage] XamlRoot is null, cannot show sponsor dialog");
                return;
            }

            try
            {
                var dialog = CreateSponsorDialog();
                await InAppDialogService.ShowAsync(dialog, this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Show sponsor dialog failed: {ex.Message}");
                await ShowErrorNotificationAsync("显示赞助对话框失败", ex.Message);
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

        /// <summary>
        /// 为静态 XAML 卡片创建交错入场动画
        /// EntranceThemeTransition 只对动态添加的元素有效，对于静态元素需要手动实现
        /// </summary>
        private void StartStaggeredEntranceAnimation()
        {
            try
            {
                if (CardsPanel == null)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] CardsPanel is null");
                    return;
                }

                var children = CardsPanel.Children;
                if (children == null || children.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsPage] CardsPanel has no children");
                    return;
                }

                // 动画参数（可调整）
                const int delayPerCard = 60;      // 每个卡片延迟 60ms
                const int animationDuration = 400; // 动画时长 400ms
                const double offsetY = 40;         // Y 轴偏移 40px

                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Starting staggered entrance animation for {children.Count} cards");

                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    if (child == null) continue;

                    // 设置初始状态
                    child.Opacity = 0;
                    child.RenderTransform = new TranslateTransform { Y = offsetY };

                    // 创建动画
                    var storyboard = new Storyboard();
                    var delay = TimeSpan.FromMilliseconds(i * delayPerCard);

                    // 透明度动画
                    var fadeIn = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = new Duration(TimeSpan.FromMilliseconds(animationDuration)),
                        BeginTime = delay,
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    Storyboard.SetTarget(fadeIn, child);
                    Storyboard.SetTargetProperty(fadeIn, "Opacity");

                    // 位移动画
                    var slideUp = new DoubleAnimation
                    {
                        From = offsetY,
                        To = 0,
                        Duration = new Duration(TimeSpan.FromMilliseconds(animationDuration)),
                        BeginTime = delay,
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    Storyboard.SetTarget(slideUp, child);
                    Storyboard.SetTargetProperty(slideUp, "(UIElement.RenderTransform).(TranslateTransform.Y)");

                    storyboard.Children.Add(fadeIn);
                    storyboard.Children.Add(slideUp);
                    storyboard.Begin();
                }

                _hasPlayedEntranceAnimation = true;
                System.Diagnostics.Debug.WriteLine("[SettingsPage] Staggered entrance animation completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Failed to start staggered animation: {ex}");
                // 动画失败不影响页面功能
            }
        }
    }
}
