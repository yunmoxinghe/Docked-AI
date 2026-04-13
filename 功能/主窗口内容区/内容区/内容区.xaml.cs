using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Numerics;

namespace Docked_AI.Features.MainWindowContent.ContentArea
{
    public sealed partial class ContentArea : UserControl
    {
        private const float DefaultCornerRadius = 4f;
        private const float PinnedCornerRadius = 8f;
        private float _currentCornerRadius = DefaultCornerRadius;
        private CompositionRoundedRectangleGeometry? _clipGeometry;
        private CompositionRoundedRectangleGeometry? _gridClipGeometry;
        private Type? _currentPageType;

        public event EventHandler<NavigationEventArgs>? Navigated;

        /// <summary>
        /// 获取覆盖层容器，用于添加通用控件和装饰
        /// </summary>
        public Grid OverlayContainer => OverlayLayer;

        public ContentArea()
        {
            InitializeComponent();
            ContentFrame.Navigated += ContentFrame_Navigated;
            ContentGrid.Loaded += ContentGrid_Loaded;
            
            // 设置模糊面板的颜色以适配主题
            UpdateBlurPanelTintColor();
            
            // 监听主题变化
            ActualThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            UpdateBlurPanelTintColor();
        }

        private void UpdateBlurPanelTintColor()
        {
            // 根据当前主题设置 TintColor
            // TintColor 需要使用半透明颜色，让模糊效果能够透过
            var isDark = ActualTheme == ElementTheme.Dark || 
                        (ActualTheme == ElementTheme.Default && 
                         Application.Current.RequestedTheme == ApplicationTheme.Dark);
            
            // 使用半透明的背景色，让模糊效果更自然
            // 调整颜色使其更明显，避免显示为纯白色
            TopBlurPanel.TintColor = isDark 
                ? Windows.UI.Color.FromArgb(220, 28, 28, 28)    // 深色背景，更高不透明度
                : Windows.UI.Color.FromArgb(220, 230, 230, 230); // 浅色背景，稍暗的灰色
            
            System.Diagnostics.Debug.WriteLine($"[ContentArea] Updated TintColor for theme: {ActualTheme}, IsDark: {isDark}, Color: {TopBlurPanel.TintColor}");
        }

        private void ContentGrid_Loaded(object sender, RoutedEventArgs e)
        {
            // 为 ContentGrid 应用圆角裁切
            ApplyGridClip();
        }

        private void ApplyGridClip()
        {
            var visual = ElementCompositionPreview.GetElementVisual(ContentGrid);
            var compositor = visual.Compositor;
            
            _gridClipGeometry = compositor.CreateRoundedRectangleGeometry();
            _gridClipGeometry.CornerRadius = new Vector2(_currentCornerRadius, _currentCornerRadius);
            _gridClipGeometry.Offset = Vector2.Zero;
            _gridClipGeometry.Size = new Vector2((float)ContentGrid.ActualWidth, (float)ContentGrid.ActualHeight);
            
            visual.Clip = compositor.CreateGeometricClip(_gridClipGeometry);
            
            System.Diagnostics.Debug.WriteLine($"[ContentArea] Applied grid clip: Size={_gridClipGeometry.Size}, CornerRadius={_gridClipGeometry.CornerRadius}");
        }

        public void SetCornerRadius(bool isPinned)
        {
            _currentCornerRadius = isPinned ? PinnedCornerRadius : DefaultCornerRadius;
            ContentBorder.CornerRadius = new CornerRadius(_currentCornerRadius);
            
            // 更新 Frame 的裁切
            if (_clipGeometry != null)
            {
                _clipGeometry.CornerRadius = new Vector2(_currentCornerRadius, _currentCornerRadius);
            }
            
            // 更新 Grid 的裁切
            if (_gridClipGeometry != null)
            {
                _gridClipGeometry.CornerRadius = new Vector2(_currentCornerRadius, _currentCornerRadius);
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            _currentPageType = e.SourcePageType;
            UpdateTopBlurPanelVisibility();
            Navigated?.Invoke(this, e);
        }

        private void UpdateTopBlurPanelVisibility()
        {
            // 网页浏览页面不显示顶部模糊面板
            var isWebBrowserPage = _currentPageType?.Name == "WebBrowserPage";
            TopBlurPanel.Visibility = isWebBrowserPage ? Visibility.Collapsed : Visibility.Visible;
            
            // 重置模糊面板的状态到初始隐藏状态
            // 这确保每次导航到新页面时，模糊面板都从隐藏状态开始
            TopBlurPanel.Opacity = 0;
            TopBlurTransform.Y = -48;
            
            System.Diagnostics.Debug.WriteLine($"[ContentArea] UpdateTopBlurPanelVisibility - PageType: {_currentPageType?.Name}, IsWebBrowser: {isWebBrowserPage}, Visibility: {TopBlurPanel.Visibility}, Reset to hidden state");
        }

        /// <summary>
        /// 显示顶部模糊面板（带动画）
        /// </summary>
        public void ShowTopBlurPanel()
        {
            System.Diagnostics.Debug.WriteLine($"[ContentArea] ShowTopBlurPanel called");
            System.Diagnostics.Debug.WriteLine($"[ContentArea] TopBlurPanel.Visibility: {TopBlurPanel.Visibility}");
            
            // 如果面板被禁用（Collapsed），不执行动画
            if (TopBlurPanel.Visibility == Visibility.Collapsed)
            {
                System.Diagnostics.Debug.WriteLine($"[ContentArea] TopBlurPanel is Collapsed (disabled for this page), returning");
                return;
            }

            // 如果已经完全显示，不重复执行动画
            if (TopBlurPanel.Opacity >= 1.0 && TopBlurTransform.Y >= 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ContentArea] TopBlurPanel already visible, returning");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ContentArea] Starting show animation");
            var storyboard = new Storyboard();

            // Y 位置动画：从当前位置到 0
            var translateAnimation = new DoubleAnimation
            {
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(translateAnimation, TopBlurTransform);
            Storyboard.SetTargetProperty(translateAnimation, "Y");
            storyboard.Children.Add(translateAnimation);

            // 透明度动画：从当前值到 1
            var opacityAnimation = new DoubleAnimation
            {
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(opacityAnimation, TopBlurPanel);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
            storyboard.Children.Add(opacityAnimation);

            storyboard.Begin();
            System.Diagnostics.Debug.WriteLine($"[ContentArea] Animation started");
        }

        /// <summary>
        /// 隐藏顶部模糊面板（带动画）
        /// </summary>
        public void HideTopBlurPanel()
        {
            System.Diagnostics.Debug.WriteLine($"[ContentArea] HideTopBlurPanel called");
            System.Diagnostics.Debug.WriteLine($"[ContentArea] TopBlurPanel.Visibility: {TopBlurPanel.Visibility}");
            
            // 如果面板被禁用（Collapsed），不执行动画
            if (TopBlurPanel.Visibility == Visibility.Collapsed)
            {
                System.Diagnostics.Debug.WriteLine($"[ContentArea] TopBlurPanel is Collapsed (disabled for this page), returning");
                return;
            }

            // 如果已经完全隐藏，不重复执行动画
            if (TopBlurPanel.Opacity <= 0 && TopBlurTransform.Y <= -48)
            {
                System.Diagnostics.Debug.WriteLine($"[ContentArea] TopBlurPanel already hidden, returning");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ContentArea] Starting hide animation");
            var storyboard = new Storyboard();

            // Y 位置动画：从当前位置到 -48
            var translateAnimation = new DoubleAnimation
            {
                To = -48,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(translateAnimation, TopBlurTransform);
            Storyboard.SetTargetProperty(translateAnimation, "Y");
            storyboard.Children.Add(translateAnimation);

            // 透明度动画：从当前值到 0
            var opacityAnimation = new DoubleAnimation
            {
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(opacityAnimation, TopBlurPanel);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
            storyboard.Children.Add(opacityAnimation);

            storyboard.Begin();
            System.Diagnostics.Debug.WriteLine($"[ContentArea] Animation started");
        }

        public void Navigate(Type pageType, object? parameter = null)
        {
            if (parameter != null)
            {
                ContentFrame.Navigate(pageType, parameter);
            }
            else
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private void ContentFrame_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
            {
                return;
            }

            // 更新 Grid 的裁切大小
            if (_gridClipGeometry != null)
            {
                _gridClipGeometry.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
            }

            // 更新 Frame 的裁切
            var visual = ElementCompositionPreview.GetElementVisual(ContentFrame);
            if (_clipGeometry == null)
            {
                var compositor = visual.Compositor;
                _clipGeometry = compositor.CreateRoundedRectangleGeometry();
                _clipGeometry.CornerRadius = new Vector2(_currentCornerRadius, _currentCornerRadius);
                _clipGeometry.Offset = Vector2.Zero;
                visual.Clip = compositor.CreateGeometricClip(_clipGeometry);
            }

            _clipGeometry.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        }
    }
}
