using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.System;
using Windows.Globalization;
using Windows.ApplicationModel.Resources.Core;
using Docked_AI.Features.Localization;
using Docked_AI.Features.AppEntry.AutoLaunch;

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

        public SettingsPage()
        {
            // Initialize ViewModel
            var startupManager = new StartupTaskManager();
            ViewModel = new StartupSettingsViewModel(startupManager);

            InitializeComponent();
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
            // 不再需要 InitializeLanguageComboBox，因为 XAML 中已经设置了 Content
            LoadLanguageSettings();
            
            // Initialize startup settings asynchronously
            _ = InitializeStartupSettingsAsync();
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

        private void OnViewLicenseClick(object sender, RoutedEventArgs args)
        {
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
    }
}
