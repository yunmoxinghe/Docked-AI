using Docked_AI.Features.Pages.Settings;
using Docked_AI.Features.UnifiedCalls.TopAppBar;
using Docked_AI.Features.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

namespace Docked_AI.Features.Pages.Lab
{
    public sealed partial class LabPage : Page
    {
        private readonly 智能标题 _智能标题 = new();
        private const double MinResponsiveWidth = 320;
        private const double MaxResponsiveWidth = 760;
        private const double MinHorizontalMargin = 16;
        private const double MaxHorizontalMargin = 36;
        private double _lastAppliedMargin = -1;
        private double _lastMeasuredWidth = -1;

        public LabPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
            
            // 订阅窗口最大化状态变化事件
            WindowMaximizedStateChanged += OnWindowMaximizedStateChanged;
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _智能标题.Setup(PageScrollViewer, PageTitleBlock);
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _智能标题.Cleanup();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 初始化顶部应用栏菜单按钮设置
            TopBarMenuButtonToggle.IsOn = ExperimentalSettings.EnableTopBarMenuButton;

            // 初始化顶部应用栏可见性测试控件状态
            TopBarVisibilityToggle.IsOn = TopAppBarService.IsVisible;
            TopBarVisibilityToggle.Toggled += OnTopBarVisibilityToggled;

            // 应用当前设置（返回按钮由 CanGoBack 自动驱动，无需手动设置）
            TopAppBarService.SetMenuButtonVisible(ExperimentalSettings.EnableTopBarMenuButton);

            // 初始化托盘评价按钮设置
            HideTrayRateButtonToggle.IsOn = ExperimentalSettings.HideTrayRateButton;

            // 初始化 AI 实验室设置
            AILabToggle.IsOn = ExperimentalSettings.EnableAILab;

            // 初始化 WinUI 右键菜单设置
            WinUIContextMenuToggle.IsOn = ExperimentalSettings.EnableWinUIContextMenu;

            // 请求刷新监听器状态
            RequestRefreshMonitorState();

            UpdateMargin();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // 取消订阅事件
            WindowMaximizedStateChanged -= OnWindowMaximizedStateChanged;
        }

        /// <summary>
        /// 窗口最大化状态变化处理
        /// </summary>
        private void OnWindowMaximizedStateChanged(object? sender, bool isMaximized)
        {
            // 确保在 UI 线程上更新
            DispatcherQueue.TryEnqueue(() =>
            {
                if (isMaximized)
                {
                    MaximizedStateIcon.Glyph = "\uE740"; // 最大化图标
                    MaximizedStateIcon.Foreground = new SolidColorBrush(Colors.Orange);
                    MaximizedStateText.Text = "已最大化";
                }
                else
                {
                    MaximizedStateIcon.Glyph = "\uE73F"; // 还原图标
                    MaximizedStateIcon.Foreground = new SolidColorBrush(Colors.Green);
                    MaximizedStateText.Text = "未最大化";
                }
                
                System.Diagnostics.Debug.WriteLine($"[LabPage] UI updated: isMaximized={isMaximized}");
            });
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (System.Math.Abs(e.NewSize.Width - _lastMeasuredWidth) < 1) return;
            UpdateMargin();
        }

        private void UpdateMargin()
        {
            double width = RootGrid?.ActualWidth ?? ActualWidth;
            if (width <= 0) return;
            double normalized = System.Math.Clamp((width - MinResponsiveWidth) / (MaxResponsiveWidth - MinResponsiveWidth), 0, 1);
            double margin = System.Math.Round(MinHorizontalMargin + (MaxHorizontalMargin - MinHorizontalMargin) * normalized);
            if (System.Math.Abs(margin - _lastAppliedMargin) > 0.01)
            {
                PageContentPanel.Margin = new Thickness(margin, 0, margin, 0);
                _lastAppliedMargin = margin;
            }
            _lastMeasuredWidth = width;
        }

        private void OnTopBarVisibilityToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
                TopAppBarService.IsVisible = toggle.IsOn;
        }

        private void OnSetRightButtonClick(object sender, RoutedEventArgs e)
        {
            var btn = new Button
            {
                Content = new FontIcon { Glyph = "\uE713", FontSize = 16 },
                Style = (Style)Application.Current.Resources["NavigationBackButtonNormalStyle"],
                Width = 36,
                Height = 36,
            };
            btn.Click += (_, _) =>
            {
                TopAppBarService.SetRightContent(null);
                RightButtonStatus.Text = LocalizationHelper.GetString("LabPage_RightButtonCleared");
            };
            TopAppBarService.SetRightContent(btn);
            RightButtonStatus.Text = LocalizationHelper.GetString("LabPage_RightButtonSet");
        }

        private void OnClearRightButtonClick(object sender, RoutedEventArgs e)
        {
            TopAppBarService.SetRightContent(null);
            RightButtonStatus.Text = LocalizationHelper.GetString("LabPage_RightContentCleared");
        }

        private void OnSetCenterTitleClick(object sender, RoutedEventArgs e)
        {
            var text = CenterTitleInput.Text?.Trim();
            if (string.IsNullOrEmpty(text)) text = LocalizationHelper.GetString("LabPage_DefaultTitle");
            TopAppBarService.SetCenterContent(new TextBlock
            {
                Text = text,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        private void OnClearCenterClick(object sender, RoutedEventArgs e)
        {
            TopAppBarService.SetCenterContent(null);
        }

        private void OnTopBarMenuButtonToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableTopBarMenuButton = toggle.IsOn;
                TopAppBarService.SetMenuButtonVisible(toggle.IsOn);
            }
        }

        private void OnHideTrayRateButtonToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.HideTrayRateButton = toggle.IsOn;
                RaiseHideTrayRateButtonSettingsChanged();
            }
        }

        private void OnHideTrayRateButtonCardClick(object sender, RoutedEventArgs e)
        {
            // 点击卡片时切换 ToggleSwitch 状态
            HideTrayRateButtonToggle.IsOn = !HideTrayRateButtonToggle.IsOn;
        }

        private void OnAILabToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableAILab = toggle.IsOn;
                SettingsPage.RaiseAILabSettingsChanged();
            }
        }

        private void OnAILabCardClick(object sender, RoutedEventArgs e)
        {
            // 点击卡片时切换 ToggleSwitch 状态
            AILabToggle.IsOn = !AILabToggle.IsOn;
        }

        private void OnWinUIContextMenuToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableWinUIContextMenu = toggle.IsOn;
                SettingsPage.RaiseWinUIContextMenuSettingsChanged();
            }
        }

        private void OnWinUIContextMenuCardClick(object sender, RoutedEventArgs e)
        {
            // 点击卡片时切换 ToggleSwitch 状态
            WinUIContextMenuToggle.IsOn = !WinUIContextMenuToggle.IsOn;
        }

        // Event to notify when hide tray rate button settings change
        public static event System.EventHandler? HideTrayRateButtonSettingsChanged;
        internal static void RaiseHideTrayRateButtonSettingsChanged() => HideTrayRateButtonSettingsChanged?.Invoke(null, System.EventArgs.Empty);

        // Event to notify when window maximized state changes
        public static event System.EventHandler<bool>? WindowMaximizedStateChanged;
        internal static void RaiseWindowMaximizedStateChanged(bool isMaximized)
        {
            System.Diagnostics.Debug.WriteLine($"[LabPage] RaiseWindowMaximizedStateChanged: isMaximized={isMaximized}");
            WindowMaximizedStateChanged?.Invoke(null, isMaximized);
        }

        // Event to request refresh of monitor state
        public static event System.EventHandler? RefreshMonitorStateRequested;
        internal static void RequestRefreshMonitorState()
        {
            System.Diagnostics.Debug.WriteLine("[LabPage] RequestRefreshMonitorState called");
            RefreshMonitorStateRequested?.Invoke(null, System.EventArgs.Empty);
        }
    }
}
