using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Diagnostics;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 主题管理模块
    /// 包含系统主题适配和强调色应用逻辑
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        // ⚠️ InitializeForegroundColors、UpdateForegroundColorsFromTheme、OnSystemThemeChanged、ApplySystemThemeColors已移至 网页浏览页面.ForegroundColors.cs
        // ⚠️ CreateStateOverlayColor、CalculateLuminance、AdjustColorBrightness、AnimateColorChange、GetContrastingForeground已移至 网页浏览页面.ColorUtils.cs

        /// <summary>
        /// 应用系统强调色（当没有网页主题色时使用）
        /// </summary>
        private void ApplySystemAccentColor()
        {
            try
            {
                // 获取系统强调色
                if (Application.Current.Resources.TryGetValue("SystemAccentColor", out object? accentResource) &&
                    accentResource is Windows.UI.Color accentColor)
                {
                    Debug.WriteLine($"[ApplySystemAccentColor] 使用系统强调色: #{accentColor.A:X2}{accentColor.R:X2}{accentColor.G:X2}{accentColor.B:X2}");
                    
                    // 应用到顶部和底部栏
                    ApplyBarTint(isTop: true, accentColor);
                    ApplyBarTint(isTop: false, accentColor);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApplySystemAccentColor] 获取强调色失败: {ex.Message}");
            }

            // 回退：使用卡片背景色
            try
            {
                if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out object? cardResource) &&
                    cardResource is SolidColorBrush cardBrush)
                {
                    var cardColor = cardBrush.Color;
                    Debug.WriteLine($"[ApplySystemAccentColor] 回退到卡片背景色: #{cardColor.A:X2}{cardColor.R:X2}{cardColor.G:X2}{cardColor.B:X2}");
                    
                    ApplyBarTint(isTop: true, cardColor);
                    ApplyBarTint(isTop: false, cardColor);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApplySystemAccentColor] 获取卡片背景色失败: {ex.Message}");
            }
        }
    }
}
