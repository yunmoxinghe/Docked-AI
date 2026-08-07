using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Globalization;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 颜色工具模块
    /// 包含颜色计算、转换、解析、动画等工具函数
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        /// <summary>
        /// 创建状态叠加颜色（用于按钮 Hover/Pressed 状态）
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
        /// 计算颜色的相对亮度（WCAG 标准）
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

        /// <summary>
        /// 根据背景色获取对比度高的前景色（黑色或白色）
        /// </summary>
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

        /// <summary>
        /// 解析 CSS 颜色字符串（支持 rgb、rgba、hex）
        /// </summary>
        private static bool TryParseCssColor(string? cssColor, out Windows.UI.Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(cssColor))
            {
                return false;
            }

            string s = cssColor.Trim();
            if (s.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
            {
                int start = s.IndexOf('(');
                int end = s.IndexOf(')');
                if (start < 0 || end <= start)
                {
                    return false;
                }

                string inner = s.Substring(start + 1, end - start - 1);
                string[] parts = inner.Split(',');
                if (parts.Length < 3)
                {
                    return false;
                }

                if (!TryParseByte(parts[0], out byte r) ||
                    !TryParseByte(parts[1], out byte g) ||
                    !TryParseByte(parts[2], out byte b))
                {
                    return false;
                }

                color = Windows.UI.Color.FromArgb(byte.MaxValue, r, g, b);
                return true;
            }

            if (s.StartsWith('#'))
            {
                string hex = s.Substring(1);
                const int hexColorLength = 6;
                const int hexByteLength = 2;
                if (hex.Length == hexColorLength &&
                    byte.TryParse(hex.Substring(0, hexByteLength), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                    byte.TryParse(hex.Substring(hexByteLength, hexByteLength), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                    byte.TryParse(hex.Substring(hexByteLength * 2, hexByteLength), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    color = Windows.UI.Color.FromArgb(byte.MaxValue, r, g, b);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 解析字节值（支持百分比和数字）
        /// </summary>
        private static bool TryParseByte(string part, out byte value)
        {
            value = 0;
            string trimmed = part.Trim();
            if (trimmed.EndsWith("%", StringComparison.Ordinal))
            {
                if (!double.TryParse(trimmed.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
                {
                    return false;
                }

                percent = Math.Max(0, Math.Min(PercentageMax, percent));
                value = (byte)Math.Round(percent / PercentageMax * ColorChannelMax);
                return true;
            }

            if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double raw))
            {
                return false;
            }

            raw = Math.Max(0, Math.Min(ColorChannelMax, raw));
            value = (byte)Math.Round(raw);
            return true;
        }
    }
}
