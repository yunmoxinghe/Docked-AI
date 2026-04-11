using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using Windows.UI;

namespace Docked_AI.Features.Pages.WebApp.Browser.Services
{
    /// <summary>
    /// 颜色处理服务，负责颜色解析、对比度计算和动画
    /// </summary>
    public class ColorService
    {
        private const double LuminanceThreshold = 0.179; // WCAG 标准阈值
        private const double ColorChannelMax = 255.0;
        private const double PercentageMax = 100.0;
        private const int ColorTransitionDurationMs = 300;

        /// <summary>
        /// 计算颜色的相对亮度（WCAG标准）
        /// </summary>
        public static double CalculateLuminance(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        /// <summary>
        /// 根据背景色获取对比前景色（黑色或白色）
        /// </summary>
        public static Color GetContrastingForeground(Color background)
        {
            double luminance = CalculateLuminance(background);
            return luminance < LuminanceThreshold ? Colors.White : Colors.Black;
        }

        /// <summary>
        /// 调整颜色亮度
        /// </summary>
        /// <param name="color">原始颜色</param>
        /// <param name="factor">调整因子，正数变亮，负数变暗</param>
        public static Color AdjustColorBrightness(Color color, double factor)
        {
            if (factor > 0)
            {
                // 变亮：向白色混合
                return Color.FromArgb(
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
                return Color.FromArgb(
                    color.A,
                    (byte)Math.Max(0, color.R * (1 - factor)),
                    (byte)Math.Max(0, color.G * (1 - factor)),
                    (byte)Math.Max(0, color.B * (1 - factor))
                );
            }
        }

        /// <summary>
        /// 创建次要前景色（带透明度）
        /// </summary>
        public static Color CreateSecondaryColor(Color baseColor, double opacity = 0.7)
        {
            return Color.FromArgb(
                (byte)(baseColor.A * opacity),
                baseColor.R,
                baseColor.G,
                baseColor.B
            );
        }

        /// <summary>
        /// 使用动画平滑过渡颜色
        /// </summary>
        public static void AnimateColorChange(SolidColorBrush brush, Color targetColor)
        {
            if (brush.Color == targetColor)
            {
                return;
            }

            var animation = new ColorAnimation
            {
                To = targetColor,
                Duration = new Duration(TimeSpan.FromMilliseconds(ColorTransitionDurationMs)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, brush);
            Storyboard.SetTargetProperty(animation, "Color");
            
            storyboard.Begin();
        }

        /// <summary>
        /// 解析CSS颜色字符串
        /// </summary>
        public static bool TryParseCssColor(string? cssColor, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(cssColor))
            {
                return false;
            }

            string s = cssColor.Trim();

            // 解析 rgb() 或 rgba()
            if (s.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseRgbColor(s, out color);
            }

            // 解析十六进制颜色
            if (s.StartsWith('#'))
            {
                return TryParseHexColor(s, out color);
            }

            return false;
        }

        private static bool TryParseRgbColor(string cssColor, out Color color)
        {
            color = Colors.Transparent;
            
            int start = cssColor.IndexOf('(');
            int end = cssColor.IndexOf(')');
            if (start < 0 || end <= start)
            {
                return false;
            }

            string inner = cssColor.Substring(start + 1, end - start - 1);
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

            color = Color.FromArgb(byte.MaxValue, r, g, b);
            return true;
        }

        private static bool TryParseHexColor(string cssColor, out Color color)
        {
            color = Colors.Transparent;
            
            string hex = cssColor.Substring(1);
            const int hexColorLength = 6;
            const int hexByteLength = 2;
            
            if (hex.Length == hexColorLength &&
                byte.TryParse(hex.Substring(0, hexByteLength), System.Globalization.NumberStyles.HexNumber, 
                    System.Globalization.CultureInfo.InvariantCulture, out byte r) &&
                byte.TryParse(hex.Substring(hexByteLength, hexByteLength), System.Globalization.NumberStyles.HexNumber, 
                    System.Globalization.CultureInfo.InvariantCulture, out byte g) &&
                byte.TryParse(hex.Substring(hexByteLength * 2, hexByteLength), System.Globalization.NumberStyles.HexNumber, 
                    System.Globalization.CultureInfo.InvariantCulture, out byte b))
            {
                color = Color.FromArgb(byte.MaxValue, r, g, b);
                return true;
            }

            return false;
        }

        private static bool TryParseByte(string part, out byte value)
        {
            value = 0;
            string trimmed = part.Trim();
            
            if (trimmed.EndsWith("%", StringComparison.Ordinal))
            {
                if (!double.TryParse(trimmed.TrimEnd('%'), System.Globalization.NumberStyles.Float, 
                    System.Globalization.CultureInfo.InvariantCulture, out double percent))
                {
                    return false;
                }

                percent = Math.Max(0, Math.Min(PercentageMax, percent));
                value = (byte)Math.Round(percent / PercentageMax * ColorChannelMax);
                return true;
            }

            if (!double.TryParse(trimmed, System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out double raw))
            {
                return false;
            }

            raw = Math.Max(0, Math.Min(ColorChannelMax, raw));
            value = (byte)Math.Round(raw);
            return true;
        }
    }
}
