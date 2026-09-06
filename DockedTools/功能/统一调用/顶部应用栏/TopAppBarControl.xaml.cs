using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;

namespace DockedTools.Features.UnifiedCalls.TopAppBar;

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
        
        // ✅ 不需要监听主题变化手动刷新
        // 自定义的 TopAppBarAcrylicBrush 会自动跟随 RequestedTheme
    }

    public void SetBackground(Brush? brush)
    {
        if (brush != null)
        {
            // 自定义背景：直接设置
            BackgroundLayer.Background = brush;
        }
        // ⚠️ 如果传入 null，不做任何操作，保持 XAML 中的 ThemeResource 绑定
    }

    public void ResetBackground()
    {
        // ⚠️ 无法真正重置到 XAML 的 ThemeResource，因为代码设置会覆盖绑定
        // 建议重新加载控件或避免调用 SetBackground
        System.Diagnostics.Debug.WriteLine("[TopAppBarControl] ResetBackground 无法恢复 XAML 的 ThemeResource 绑定");
    }

    public void SetForeground(Brush? brush)
    {
        if (brush != null)
        {
            BackButton.Foreground = brush;
            MenuButton.Foreground = brush;
            MoreButton.Foreground = brush;
            ApplyForegroundToPanel(LeftPanel, brush);
            ApplyForegroundToPanel(RightPanel, brush);
        }
    }

    public void ResetForeground()
    {
        System.Diagnostics.Debug.WriteLine("[TopAppBarControl] ResetForeground 无法恢复默认前景色");
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
        var button = new Button
        {
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
            BackgroundSizing = BackgroundSizing.InnerBorderEdge,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Content = new FontIcon
            {
                Glyph = glyph,
                FontSize = 16
            }
        };

        // 使用 ResourceDictionary 引用 ThemeResource，而不是缓存 Brush
        var resources = new ResourceDictionary();
        resources.ThemeDictionaries["Default"] = Application.Current.Resources;
        resources.ThemeDictionaries["Light"] = Application.Current.Resources;
        resources.ThemeDictionaries["Dark"] = Application.Current.Resources;
        
        // 设置按钮样式资源（会跟随主题变化）
        button.Resources["ButtonBackground"] = Application.Current.Resources["SubtleFillColorTransparent"];
        button.Resources["ButtonBackgroundPointerOver"] = Application.Current.Resources["SubtleFillColorSecondary"];
        button.Resources["ButtonBackgroundPressed"] = Application.Current.Resources["SubtleFillColorTertiary"];

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
