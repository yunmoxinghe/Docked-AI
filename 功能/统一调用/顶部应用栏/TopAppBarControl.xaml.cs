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
        var button = new Button
        {
            Width = 40,
            Height = 40,
            MinWidth = 40,
            MinHeight = 40,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Foreground = GetDefaultForegroundBrush(),
            Content = new FontIcon
            {
                Glyph = glyph,
                Width = 16,
                Height = 16,
                FontSize = 16
            }
        };

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
