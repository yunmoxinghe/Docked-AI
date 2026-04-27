using Docked_AI.Features.Pages.Settings;
using Docked_AI.Features.UnifiedCalls.TopAppBar;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
            SizeChanged += OnSizeChanged;
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
            AILabToggle.Toggled -= OnAILabToggled;
            RoundedWebViewToggle.Toggled -= OnRoundedWebViewToggled;
            WinUIContextMenuToggle.Toggled -= OnWinUIContextMenuToggled;
            TopBarMenuButtonToggle.Toggled -= OnTopBarMenuButtonToggled;

            AILabToggle.IsOn = ExperimentalSettings.EnableAILab;
            RoundedWebViewToggle.IsOn = ExperimentalSettings.EnableRoundedWebView;
            WinUIContextMenuToggle.IsOn = ExperimentalSettings.EnableWinUIContextMenu;
            TopBarMenuButtonToggle.IsOn = ExperimentalSettings.EnableTopBarMenuButton;

            AILabToggle.Toggled += OnAILabToggled;
            RoundedWebViewToggle.Toggled += OnRoundedWebViewToggled;
            WinUIContextMenuToggle.Toggled += OnWinUIContextMenuToggled;
            TopBarMenuButtonToggle.Toggled += OnTopBarMenuButtonToggled;

            // 初始化顶部应用栏测试控件状态
            TopBarVisibilityToggle.IsOn = TopAppBarService.IsVisible;
            TopBarVisibilityToggle.Toggled += OnTopBarVisibilityToggled;

            // 应用当前设置（返回按钮由 CanGoBack 自动驱动，无需手动设置）
            TopAppBarService.SetMenuButtonVisible(ExperimentalSettings.EnableTopBarMenuButton);

            UpdateMargin();
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
                RightButtonStatus.Text = "已清除右侧按钮";
            };
            TopAppBarService.SetRightContent(btn);
            RightButtonStatus.Text = "已设置右侧按钮（点击按钮可清除）";
        }

        private void OnClearRightButtonClick(object sender, RoutedEventArgs e)
        {
            TopAppBarService.SetRightContent(null);
            RightButtonStatus.Text = "已清除右侧内容";
        }

        private void OnSetCenterTitleClick(object sender, RoutedEventArgs e)
        {
            var text = CenterTitleInput.Text?.Trim();
            if (string.IsNullOrEmpty(text)) text = "实验室 📦";
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

        private void OnAILabToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableAILab = toggle.IsOn;
                SettingsPage.RaiseAILabSettingsChanged();
            }
        }

        private void OnRoundedWebViewToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableRoundedWebView = toggle.IsOn;
                SettingsPage.RaiseRoundedWebViewSettingsChanged();
            }
        }

        private void OnWinUIContextMenuToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableWinUIContextMenu = toggle.IsOn;
                SettingsPage.RaiseWinUIContextMenuSettingsChanged();
            }
        }

        private void OnTopBarMenuButtonToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                ExperimentalSettings.EnableTopBarMenuButton = toggle.IsOn;
                TopAppBarService.SetMenuButtonVisible(toggle.IsOn);
            }
        }
    }
}
