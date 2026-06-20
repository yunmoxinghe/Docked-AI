using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;

namespace Docked_AI.Features.UnifiedCalls.TopAppBar;

/// <summary>
/// 顶部应用栏的独立 UI 控件。内容区只负责承载，顶栏的视觉状态由这里统一管理。
/// </summary>
public sealed partial class TopAppBarControl : UserControl
{
    public event EventHandler? BackButtonClicked;
    public event EventHandler? MenuButtonClicked;
    private bool _isChromeVisible = true;

    public Grid AppBarBackground => BackgroundLayer;
    public StackPanel LeftContentPanel => LeftPanel;
    public StackPanel RightContentPanel => RightPanel;
    public ContentPresenter CenterContent => CenterContentPresenter;
    public MenuFlyout MoreMenu => MoreMenuFlyout;

    public bool IsAppBarVisible
    {
        get => CenterContentPresenter.Visibility == Visibility.Visible;
        set => SetAppBarVisibleAnimated(value);
    }

    public TopAppBarControl()
    {
        InitializeComponent();
        
        // 监听主题变化，自动刷新资源引用
        ActualThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(FrameworkElement sender, object args)
    {
        System.Diagnostics.Debug.WriteLine("[TopAppBarControl] 系统主题已切换，刷新资源");
        
        // 强制刷新所有 ThemeResource 绑定的资源
        RefreshThemeResources();
    }

    private void RefreshThemeResources()
    {
        System.Diagnostics.Debug.WriteLine("[TopAppBarControl] RefreshThemeResources 开始");
        
        // 刷新默认背景和前景色（使用最新主题资源）
        var defaultBackground = GetDefaultBackgroundBrush();
        var defaultForeground = GetDefaultForegroundBrush();
        
        System.Diagnostics.Debug.WriteLine($"[TopAppBarControl] 新背景画刷类型: {defaultBackground?.GetType().Name}");
        System.Diagnostics.Debug.WriteLine($"[TopAppBarControl] 新前景画刷类型: {defaultForeground?.GetType().Name}");
        
        // ✅ 强制更新背景层的 ThemeResource 引用（直接设置，不检查旧值）
        BackgroundLayer.Background = defaultBackground;
        
        // 更新按钮前景色
        BackButton.Foreground = defaultForeground;
        MenuButton.Foreground = defaultForeground;
        MoreButton.Foreground = defaultForeground;
        
        // 刷新按钮的悬停/按下状态颜色
        RefreshButtonResources(BackButton);
        RefreshButtonResources(MenuButton);
        RefreshButtonResources(MoreButton);
        
        // 刷新左右面板中的控件
        RefreshPanelTheme(LeftPanel, defaultForeground);
        RefreshPanelTheme(RightPanel, defaultForeground);
        
        System.Diagnostics.Debug.WriteLine("[TopAppBarControl] RefreshThemeResources 完成");
    }

    private void RefreshButtonResources(Button button)
    {
        if (button == null) return;
        
        // 更新按钮的 ThemeResource 覆盖
        button.Resources["ButtonBackgroundPointerOver"] = GetResourceOrDefault("SubtleFillColorSecondary");
        button.Resources["ButtonBackgroundPressed"] = GetResourceOrDefault("SubtleFillColorTertiary");
    }

    private void RefreshPanelTheme(Panel panel, Brush foreground)
    {
        foreach (var child in panel.Children)
        {
            if (child is Control control)
            {
                control.Foreground = foreground;
            }
            
            if (child is Button button)
            {
                RefreshButtonResources(button);
                
                if (button.Content is FontIcon icon)
                {
                    icon.Foreground = foreground;
                }
            }
        }
    }

    private object GetResourceOrDefault(string resourceKey)
    {
        if (Application.Current.Resources.TryGetValue(resourceKey, out object? resource))
        {
            return resource;
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public void SetBackground(Brush? brush)
    {
        BackgroundLayer.Background = brush ?? GetDefaultBackgroundBrush();
    }

    public void ResetBackground()
    {
        SetBackground(null);
    }

    public void SetForeground(Brush? brush)
    {
        var foregroundBrush = brush ?? GetDefaultForegroundBrush();
        BackButton.Foreground = foregroundBrush;
        MenuButton.Foreground = foregroundBrush;
        MoreButton.Foreground = foregroundBrush;
        ApplyForegroundToPanel(LeftPanel, foregroundBrush);
        ApplyForegroundToPanel(RightPanel, foregroundBrush);
    }

    public void ResetForeground()
    {
        SetForeground(null);
    }

    public void SetChromeVisible(bool visible)
    {
        _isChromeVisible = visible;
        UpdateChromeVisibility();
    }

    public void ResetChromeVisibility()
    {
        SetChromeVisible(true);
    }

    private static Brush GetDefaultBackgroundBrush()
    {
        if (Application.Current.Resources.TryGetValue("AcrylicInAppFillColorDefaultBrush", out object? resource) &&
            resource is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private static Brush GetDefaultForegroundBrush()
    {
        if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object? resource) &&
            resource is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Black);
    }

    public void SetBackButtonVisible(bool visible)
    {
        SetBackButtonVisibleAnimated(visible);
    }

    public void SetMenuButtonVisible(bool visible)
    {
        MenuButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetMoreButtonVisible(bool visible)
    {
        MoreButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public Button CreateIconButton(string glyph, RoutedEventHandler clickHandler, string? toolTip = null)
    {
        // 安全获取背景画刷
        Brush? backgroundBrush = null;
        if (Application.Current.Resources.TryGetValue("SubtleFillColorTransparent", out object? bgResource))
        {
            backgroundBrush = bgResource is Brush brush ? brush : 
                             bgResource is Windows.UI.Color color ? new SolidColorBrush(color) : null;
        }
        
        var button = new Button
        {
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
            Background = backgroundBrush ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BackgroundSizing = BackgroundSizing.InnerBorderEdge,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Content = new FontIcon
            {
                Glyph = glyph,
                FontSize = 16
            }
        };

        // 添加悬停和按下状态的资源覆盖
        if (Application.Current.Resources.TryGetValue("SubtleFillColorSecondary", out object? secondaryResource))
        {
            var secondaryBrush = secondaryResource is Brush brush ? brush : 
                                 secondaryResource is Windows.UI.Color color ? new SolidColorBrush(color) : null;
            if (secondaryBrush != null)
            {
                button.Resources["ButtonBackgroundPointerOver"] = secondaryBrush;
            }
        }

        if (Application.Current.Resources.TryGetValue("SubtleFillColorTertiary", out object? tertiaryResource))
        {
            var tertiaryBrush = tertiaryResource is Brush brush ? brush : 
                                tertiaryResource is Windows.UI.Color color ? new SolidColorBrush(color) : null;
            if (tertiaryBrush != null)
            {
                button.Resources["ButtonBackgroundPressed"] = tertiaryBrush;
            }
        }

        if (!string.IsNullOrWhiteSpace(toolTip))
        {
            ToolTipService.SetToolTip(button, toolTip);
        }

        button.Click += clickHandler;
        return button;
    }

    private static void ApplyForegroundToPanel(Panel panel, Brush foregroundBrush)
    {
        foreach (var child in panel.Children)
        {
            if (child is Control control)
            {
                control.Foreground = foregroundBrush;
            }

            if (child is Button { Content: FontIcon icon })
            {
                icon.Foreground = foregroundBrush;
            }
        }
    }

    private void SetAppBarVisibleAnimated(bool visible)
    {
        CenterContentPresenter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        if (!visible || !_isChromeVisible)
        {
            BackgroundLayer.Visibility = Visibility.Collapsed;
            ElementCompositionPreview.GetElementVisual(BackgroundLayer).Opacity = 1f;
            return;
        }

        var visual = ElementCompositionPreview.GetElementVisual(BackgroundLayer);
        var compositor = visual.Compositor;

        if (visible)
        {
            if (BackgroundLayer.Visibility == Visibility.Visible) return;

            BackgroundLayer.Visibility = Visibility.Visible;
            var fadeIn = compositor.CreateScalarKeyFrameAnimation();
            fadeIn.InsertKeyFrame(0f, 0f);
            fadeIn.InsertKeyFrame(1f, 1f);
            fadeIn.Duration = TimeSpan.FromMilliseconds(200);
            visual.StartAnimation("Opacity", fadeIn);
        }
    }

    private void UpdateChromeVisibility()
    {
        if (!IsAppBarVisible || !_isChromeVisible)
        {
            BackgroundLayer.Visibility = Visibility.Collapsed;
            ElementCompositionPreview.GetElementVisual(BackgroundLayer).Opacity = 1f;
            return;
        }

        SetAppBarVisibleAnimated(true);
    }

    private void SetBackButtonVisibleAnimated(bool visible)
    {
        var visual = ElementCompositionPreview.GetElementVisual(BackButton);
        var compositor = visual.Compositor;

        if (visible)
        {
            if (BackButton.Visibility == Visibility.Visible) return;

            BackButton.Visibility = Visibility.Visible;
            BackButton.IsEnabled = true;

            var fadeIn = compositor.CreateScalarKeyFrameAnimation();
            fadeIn.InsertKeyFrame(0f, 0f);
            fadeIn.InsertKeyFrame(1f, 1f);
            fadeIn.Duration = TimeSpan.FromMilliseconds(200);
            fadeIn.Target = "Opacity";

            visual.StartAnimation("Opacity", fadeIn);
        }
        else
        {
            if (BackButton.Visibility == Visibility.Collapsed) return;

            var fadeOut = compositor.CreateScalarKeyFrameAnimation();
            fadeOut.InsertKeyFrame(0f, visual.Opacity);
            fadeOut.InsertKeyFrame(1f, 0f);
            fadeOut.Duration = TimeSpan.FromMilliseconds(150);
            fadeOut.Target = "Opacity";

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            visual.StartAnimation("Opacity", fadeOut);
            batch.End();

            batch.Completed += (_, _) =>
            {
                BackButton.Visibility = Visibility.Collapsed;
                visual.Opacity = 1f;
            };
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        BackButtonClicked?.Invoke(this, EventArgs.Empty);
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        MenuButtonClicked?.Invoke(this, EventArgs.Empty);
    }

    private void BackButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState(BackAnimatedIcon, "PointerOver");
    }

    private void BackButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState(BackAnimatedIcon, "Normal");
    }

    private void MenuButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState(MenuAnimatedIcon, "PointerOver");
    }

    private void MenuButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState(MenuAnimatedIcon, "Normal");
    }
}
