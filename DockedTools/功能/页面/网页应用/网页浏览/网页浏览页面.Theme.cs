using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;
using DockedTools.Features.UnifiedCalls.AsyncSafety;
using DockedTools.Features.UnifiedCalls.TopAppBar;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 主题和颜色管理部分
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        private void InitializeForegroundColors()
        {
            UpdateForegroundColorsFromTheme();
        }

        /// <summary>
        /// 从当前主题资源更新前景色（支持主题切换）
        /// </summary>
        private void UpdateForegroundColorsFromTheme()
        {
            if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object? resource) 
                && resource is SolidColorBrush themeBrush)
            {
                _topBarForegroundBrush.Color = themeBrush.Color;
                _bottomBarForegroundBrush.Color = themeBrush.Color;
            }
            else
            {
                var theme = Application.Current.RequestedTheme;
                var defaultColor = theme == ApplicationTheme.Dark ? Colors.White : Colors.Black;
                _topBarForegroundBrush.Color = defaultColor;
                _bottomBarForegroundBrush.Color = defaultColor;
            }

            if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out object? secondaryResource) 
                && secondaryResource is SolidColorBrush secondaryBrush)
            {
                _topBarSecondaryForegroundBrush.Color = secondaryBrush.Color;
            }
            else
            {
                var baseColor = _topBarForegroundBrush.Color;
                _topBarSecondaryForegroundBrush.Color = Windows.UI.Color.FromArgb(
                    (byte)(baseColor.A * 0.7),
                    baseColor.R,
                    baseColor.G,
                    baseColor.B
                );
            }

            if (Application.Current.Resources.TryGetValue("TextFillColorDisabledBrush", out object? disabledResource) 
                && disabledResource is SolidColorBrush disabledBrush)
            {
                _bottomBarDisabledForegroundBrush.Color = disabledBrush.Color;
            }
            else
            {
                var baseColor = _bottomBarForegroundBrush.Color;
                _bottomBarDisabledForegroundBrush.Color = Windows.UI.Color.FromArgb(
                    (byte)(baseColor.A * 0.6),
                    baseColor.R,
                    baseColor.G,
                    baseColor.B
                );
            }
            
            _bottomBarHoverForegroundBrush.Color = AdjustColorBrightness(_bottomBarForegroundBrush.Color, 0.15);
        }

        /// <summary>
        /// 系统主题切换时的回调
        /// </summary>
        private void OnSystemThemeChanged(FrameworkElement sender, object args)
        {
            System.Diagnostics.Debug.WriteLine("═══════════════════════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("[WebBrowserPage] ✅✅✅ ActualThemeChanged 事件触发！");
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 当前 ActualTheme: {ActualTheme}");
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] WebView 状态: CoreWebView2={(WebView?.CoreWebView2 != null ? "✓" : "✗")}, IsReady={_isWebViewReady}");
            System.Diagnostics.Debug.WriteLine("═══════════════════════════════════════════════════════");
            
            // 重新从主题资源获取颜色
            UpdateForegroundColorsFromTheme();
            
            // ✅ 立即更新 TopAppBar 的前景色（包括关闭按钮等）
            TopAppBarService.SetForeground(_topBarForegroundBrush);
            System.Diagnostics.Debug.WriteLine("[WebBrowserPage] TopAppBar 前景色已更新");
            
            // ✅ 核心修复：系统主题切换后，WebView2 内部的网页会自动响应（CSS prefers-color-scheme），
            // 但不会触发 NavigationCompleted 事件，所以我们需要手动触发完整的取色逻辑
            
            if (WebView?.CoreWebView2 != null && _isWebViewReady)
            {
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] WebView 已就绪，强制重新提取网页主题色");
                
                // ✅ 重置取色状态，让取色逻辑重新执行
                _hasReceivedFirstTint = false;
                _hasAppliedThemeColor = false;
                
                // ⭐ 任务 6.4：使用 AsyncSafety 包装 DispatcherQueue.TryEnqueue 中的 async lambda
                AsyncSafety.TryEnqueue(
                    DispatcherQueue,
                    async () =>
                    {
                        System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 等待 500ms 让 WebView2 完成主题切换...");
                        
                        // 等待网页重新渲染（prefers-color-scheme CSS 生效）
                        await Task.Delay(500);
                        
                        System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 开始执行主题切换后的取色");
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 取色前背景色: Top={_topBarBackgroundBrush.Color}, Bottom={_bottomBarBackgroundBrush.Color}");
                        
                        // ✅ 步骤1：尝试 meta theme-color
                        await TryApplyThemeColorAsync();
                        
                        // ✅ 步骤2：如果没有 theme-color，使用脚本采样
                        if (!_hasAppliedThemeColor)
                        {
                            System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 没有 theme-color，触发脚本采样取色");
                            await Task.Delay(100);
                            await TriggerTintSamplingAsync();
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 取色完成后背景色: Top={_topBarBackgroundBrush.Color}, Bottom={_bottomBarBackgroundBrush.Color}");
                    },
                    "WebBrowserPage",
                    "ThemeChanged");
            }
            else
            {
                // WebView 还没准备好，只更新前景色
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] ⚠️ WebView 未就绪，仅更新前景色");
            }
        }

        /// <summary>
        /// 应用系统主题的默认颜色（当没有网页主题色时使用）
        /// </summary>
        private void ApplySystemThemeColors()
        {
            // 从系统资源获取强调色或卡片背景色
            if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out object? bgResource) 
                && bgResource is SolidColorBrush bgBrush)
            {
                _topBarBackgroundBrush.Color = bgBrush.Color;
                _bottomBarBackgroundBrush.Color = bgBrush.Color;
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 应用系统卡片背景色");
            }
            else if (Application.Current.Resources.TryGetValue("SystemAccentColor", out object? accentResource) 
                && accentResource is Windows.UI.Color accentColor)
            {
                _topBarBackgroundBrush.Color = accentColor;
                _bottomBarBackgroundBrush.Color = accentColor;
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 应用系统强调色");
            }
            else
            {
                // 回退：根据当前主题选择浅灰或深灰
                var theme = Application.Current.RequestedTheme;
                var defaultBgColor = theme == ApplicationTheme.Dark 
                    ? Windows.UI.Color.FromArgb(255, 32, 32, 32)   // 深色主题：深灰
                    : Windows.UI.Color.FromArgb(255, 243, 243, 243); // 浅色主题：浅灰
                
                _topBarBackgroundBrush.Color = defaultBgColor;
                _bottomBarBackgroundBrush.Color = defaultBgColor;
                System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 应用回退背景色 (主题: {theme})");
            }
        }

        /// <summary>
        /// Fallback：应用系统强调色
        /// </summary>
        private void ApplySystemAccentColor()
        {
            try
            {
                // 尝试获取系统强调色
                if (Application.Current.Resources.TryGetValue("SystemAccentColor", out object? accentResource) 
                    && accentResource is Windows.UI.Color accentColor)
                {
                    ApplyBarTint(isTop: true, accentColor);
                    ApplyBarTint(isTop: false, accentColor);
                    System.Diagnostics.Debug.WriteLine("Applied system accent color as fallback");
                }
            }
            catch
            {
                // 最终 fallback：保持透明
            }
        }

        /// <summary>
        /// 创建状态叠加层颜色（Material Design 最佳实践）
        /// </summary>
        /// <param name="baseColor">基础颜色</param>
        /// <param name="overlayStrength">叠加强度（正数=变亮，负数=变暗）</param>
        private static Windows.UI.Color CreateStateOverlayColor(Windows.UI.Color baseColor, double overlayStrength)
        {
            if (overlayStrength > 0)
            {
                // 叠加白色（变亮）
                return Windows.UI.Color.FromArgb(
                    baseColor.A,
                    (byte)Math.Min(255, baseColor.R + (255 - baseColor.R) * overlayStrength),
                    (byte)Math.Min(255, baseColor.G + (255 - baseColor.G) * overlayStrength),
                    (byte)Math.Min(255, baseColor.B + (255 - baseColor.B) * overlayStrength)
                );
            }
            else
            {
                // 叠加黑色（变暗）
                overlayStrength = -overlayStrength;
                return Windows.UI.Color.FromArgb(
                    baseColor.A,
                    (byte)Math.Max(0, baseColor.R * (1 - overlayStrength)),
                    (byte)Math.Max(0, baseColor.G * (1 - overlayStrength)),
                    (byte)Math.Max(0, baseColor.B * (1 - overlayStrength))
                );
            }
        }

        /// <summary>
        /// 计算颜色的相对亮度
        /// </summary>
        private static double CalculateLuminance(Windows.UI.Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        /// <summary>
        /// 调整颜色亮度
        /// </summary>
        /// <param name="color">原始颜色</param>
        /// <param name="factor">调整因子，正数变亮，负数变暗</param>
        private static Windows.UI.Color AdjustColorBrightness(Windows.UI.Color color, double factor)
        {
            if (factor > 0)
            {
                // 变亮：向白色混合
                return Windows.UI.Color.FromArgb(
                    color.A,
                    (byte)Math.Min(255, color.R + (255 - color.R) * factor),
                    (byte)Math.Min(255, color.G + (255 - color.G) * factor),
                    (byte)Math.Min(255, color.B + (255 - color.B) * factor)
                );
            }
            else
            {
                // 变暗：向黑色混合
                factor = -factor;
                return Windows.UI.Color.FromArgb(
                    color.A,
                    (byte)Math.Max(0, color.R * (1 - factor)),
                    (byte)Math.Max(0, color.G * (1 - factor)),
                    (byte)Math.Max(0, color.B * (1 - factor))
                );
            }
        }

        /// <summary>
        /// 使用动画平滑过渡颜色
        /// </summary>
        private void AnimateColorChange(SolidColorBrush brush, Windows.UI.Color targetColor)
        {
            if (brush.Color == targetColor)
            {
                return; // 颜色相同，无需动画
            }

            // ✅ 修复：首次设置颜色时，先设置目标色，再从透明淡入（避免黑色闪现）
            if (brush.Color == Colors.Transparent)
            {
                // 先直接设置为目标颜色（但保持透明）
                brush.Color = Windows.UI.Color.FromArgb(0, targetColor.R, targetColor.G, targetColor.B);
                
                // 然后用透明度动画淡入
                var fadeInAnimation = new ColorAnimation
                {
                    From = Windows.UI.Color.FromArgb(0, targetColor.R, targetColor.G, targetColor.B),
                    To = targetColor,
                    Duration = new Duration(TimeSpan.FromMilliseconds(ColorTransitionDurationMs)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var storyboard = new Storyboard();
                storyboard.Children.Add(fadeInAnimation);
                Storyboard.SetTarget(fadeInAnimation, brush);
                Storyboard.SetTargetProperty(fadeInAnimation, "Color");
                
                storyboard.Begin();
                return;
            }

            // 后续颜色变化：正常的颜色过渡动画
            var animation = new ColorAnimation
            {
                To = targetColor,
                Duration = new Duration(TimeSpan.FromMilliseconds(ColorTransitionDurationMs)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var storyboard2 = new Storyboard();
            storyboard2.Children.Add(animation);
            Storyboard.SetTarget(animation, brush);
            Storyboard.SetTargetProperty(animation, "Color");
            
            storyboard2.Begin();
        }

        private static Windows.UI.Color GetContrastingForeground(Windows.UI.Color background)
        {
            // WCAG 标准相对亮度公式：先归一化到 [0, 1]
            double r = background.R / 255.0;
            double g = background.G / 255.0;
            double b = background.B / 255.0;
            
            // 相对亮度计算（sRGB）
            double luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            
            // 使用 WCAG 标准阈值 0.179
            return luminance < LuminanceThreshold ? Colors.White : Colors.Black;
        }
    }
}
