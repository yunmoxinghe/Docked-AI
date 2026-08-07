using DockedTools.Features.UnifiedCalls.TopAppBar;
using Microsoft.UI.Xaml.Media;
using System;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - Tint应用模块
    /// 包含顶部栏和底部栏的Tint应用逻辑
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        private void ApplyBarTint(bool isTop, Windows.UI.Color sampledColor)
        {
            var tinted = Windows.UI.Color.FromArgb(byte.MaxValue, sampledColor.R, sampledColor.G, sampledColor.B);
            SolidColorBrush background = isTop ? _topBarBackgroundBrush : _bottomBarBackgroundBrush;
            SolidColorBrush foreground = isTop ? _topBarForegroundBrush : _bottomBarForegroundBrush;

            // 防闪烁逻辑
            if (!_hasReceivedFirstTint)
            {
                bool isCurrentlyInitial = background.Color.A <= 1 && 
                    background.Color.R == 0 && background.Color.G == 0 && background.Color.B == 0;
                
                bool isPureWhite = sampledColor.R == 255 && sampledColor.G == 255 && sampledColor.B == 255;
                
                if (isCurrentlyInitial && isPureWhite)
                {
                    return;
                }
                
                _hasReceivedFirstTint = true;
            }

            AnimateColorChange(background, tinted);
            
            var contrastColor = GetContrastingForeground(sampledColor);
            AnimateColorChange(foreground, contrastColor);

            if (isTop)
            {
                // 更新次要前景色
                var secondaryColor = Windows.UI.Color.FromArgb(
                    (byte)(contrastColor.A * 0.7),
                    contrastColor.R,
                    contrastColor.G,
                    contrastColor.B
                );
                AnimateColorChange(_topBarSecondaryForegroundBrush, secondaryColor);
                
                // 更新顶部栏UI元素的颜色
                if (_topBarTitle != null)
                {
                    _topBarTitle.Foreground = _topBarForegroundBrush;
                }
                if (_topBarIconFallback != null)
                {
                    _topBarIconFallback.Foreground = _topBarSecondaryForegroundBrush;
                }
                if (_unpinButton?.Content is FontIcon unpinIcon)
                {
                    unpinIcon.Foreground = _topBarForegroundBrush;
                }
                TopAppBarService.SetForeground(_topBarForegroundBrush);
            }
            else
            {
                // ✅ 底部栏按钮颜色 - 使用 Material Design 最佳实践
                double luminance = CalculateLuminance(sampledColor);
                bool isDarkBackground = luminance < LuminanceThreshold;
                
                // Hover: 在前景色上叠加 8% 的白色/黑色（Material Design 规范）
                var hoverColor = CreateStateOverlayColor(
                    contrastColor, 
                    isDarkBackground ? ButtonHoverOverlayStrength : -ButtonHoverOverlayStrength
                );
                AnimateColorChange(_bottomBarHoverForegroundBrush, hoverColor);
                
                // Disabled: 38% 透明度（WCAG 豁免，禁用组件无对比度要求）
                var disabledColor = Windows.UI.Color.FromArgb(
                    (byte)(contrastColor.A * ButtonDisabledOpacity),
                    contrastColor.R,
                    contrastColor.G,
                    contrastColor.B
                );
                AnimateColorChange(_bottomBarDisabledForegroundBrush, disabledColor);
                
                // ⚠️ Reactor 组件会自动使用更新后的 Brush，无需手动更新资源
            }
        }

        private static void RestoreSharedTopAppBarBackground()
        {
            TopAppBarService.ResetBackground();
            TopAppBarService.ResetForeground();
            TopAppBarService.ResetChromeVisibility();
        }
    }
}
