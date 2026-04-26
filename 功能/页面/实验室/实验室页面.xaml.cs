using Docked_AI.Features.Pages.Settings;
using Docked_AI.Features.UnifiedCalls.TopAppBar;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.Pages.Lab
{
    public sealed partial class LabPage : Page
    {
        public LabPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 左侧：返回按钮
            var backButton = new Button
            {
                Style = (Style)Application.Current.Resources["NavigationBackButtonNormalStyle"],
            };
            backButton.Click += (_, _) => { if (Frame.CanGoBack) Frame.GoBack(); };
            TopAppBarService.SetLeftContent(backButton);

            // 中间：页面标题
            TopAppBarService.SetCenterContent(new TextBlock
            {
                Text = "实验室 📦",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            TopAppBarService.ClearAll();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AILabToggle.Toggled -= OnAILabToggled;
            RoundedWebViewToggle.Toggled -= OnRoundedWebViewToggled;
            WinUIContextMenuToggle.Toggled -= OnWinUIContextMenuToggled;

            AILabToggle.IsOn = ExperimentalSettings.EnableAILab;
            RoundedWebViewToggle.IsOn = ExperimentalSettings.EnableRoundedWebView;
            WinUIContextMenuToggle.IsOn = ExperimentalSettings.EnableWinUIContextMenu;

            AILabToggle.Toggled += OnAILabToggled;
            RoundedWebViewToggle.Toggled += OnRoundedWebViewToggled;
            WinUIContextMenuToggle.Toggled += OnWinUIContextMenuToggled;

            // 初始化顶部应用栏测试控件状态
            TopBarVisibilityToggle.IsOn = TopAppBarService.IsVisible;
            TopBarVisibilityToggle.Toggled += OnTopBarVisibilityToggled;
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
    }
}
