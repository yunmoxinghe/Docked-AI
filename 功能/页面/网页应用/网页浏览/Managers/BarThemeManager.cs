using Docked_AI.Features.Pages.WebApp.Browser.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Text.Json;
using Windows.UI;

namespace Docked_AI.Features.Pages.WebApp.Browser.Managers
{
    /// <summary>
    /// 顶部栏和底部栏的主题管理器
    /// 负责根据网页颜色动态调整栏的背景色和前景色
    /// </summary>
    public class BarThemeManager
    {
        private readonly SolidColorBrush _topBarBackgroundBrush = new(Colors.Transparent);
        private readonly SolidColorBrush _bottomBarBackgroundBrush = new(Colors.Transparent);
        private readonly SolidColorBrush _topBarForegroundBrush = new();
        private readonly SolidColorBrush _bottomBarForegroundBrush = new();
        private readonly SolidColorBrush _topBarSecondaryForegroundBrush = new();
        private readonly SolidColorBrush _bottomBarDisabledForegroundBrush = new();
        private readonly SolidColorBrush _bottomBarHoverForegroundBrush = new();

        private bool _hasReceivedFirstTint;
        private bool _hasAppliedThemeColor;

        public SolidColorBrush TopBarBackgroundBrush => _topBarBackgroundBrush;
        public SolidColorBrush BottomBarBackgroundBrush => _bottomBarBackgroundBrush;
        public SolidColorBrush TopBarForegroundBrush => _topBarForegroundBrush;
        public SolidColorBrush BottomBarForegroundBrush => _bottomBarForegroundBrush;
        public SolidColorBrush TopBarSecondaryForegroundBrush => _topBarSecondaryForegroundBrush;
        public SolidColorBrush BottomBarDisabledForegroundBrush => _bottomBarDisabledForegroundBrush;
        public SolidColorBrush BottomBarHoverForegroundBrush => _bottomBarHoverForegroundBrush;

        /// <summary>
        /// 初始化前景色为主题默认文本颜色
        /// </summary>
        public void InitializeForegroundColors()
        {
            // 从主题资源获取默认文本颜色
            if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out object? resource) 
                && resource is SolidColorBrush themeBrush)
            {
                _topBarForegroundBrush.Color = themeBrush.Color;
                _bottomBarForegroundBrush.Color = themeBrush.Color;
            }
            else
            {
                // 回退：根据当前主题选择黑色或白色
                var theme = Application.Current.RequestedTheme;
                var defaultColor = theme == ApplicationTheme.Dark ? Colors.White : Colors.Black;
                _topBarForegroundBrush.Color = defaultColor;
                _bottomBarForegroundBrush.Color = defaultColor;
            }

            // 初始化次要前景色
            if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out object? secondaryResource) 
                && secondaryResource is SolidColorBrush secondaryBrush)
            {
                _topBarSecondaryForegroundBrush.Color = secondaryBrush.Color;
            }
            else
            {
                _topBarSecondaryForegroundBrush.Color = ColorService.CreateSecondaryColor(_topBarForegroundBrush.Color);
            }

            // 初始化禁用状态颜色
            if (Application.Current.Resources.TryGetValue("TextFillColorDisabledBrush", out object? disabledResource) 
                && disabledResource is SolidColorBrush disabledBrush)
            {
                _bottomBarDisabledForegroundBrush.Color = disabledBrush.Color;
            }
            else
            {
                _bottomBarDisabledForegroundBrush.Color = ColorService.CreateSecondaryColor(_bottomBarForegroundBrush.Color, 0.6);
            }
            
            // 初始化悬停状态颜色
            _bottomBarHoverForegroundBrush.Color = ColorService.AdjustColorBrightness(_bottomBarForegroundBrush.Color, 0.15);
        }

        /// <summary>
        /// 重置取色状态（在导航开始时调用）
        /// </summary>
        public void ResetTintState()
        {
            _hasReceivedFirstTint = false;
            _hasAppliedThemeColor = false;
        }

        /// <summary>
        /// 标记已应用主题色
        /// </summary>
        public void MarkThemeColorApplied()
        {
            _hasAppliedThemeColor = true;
        }

        /// <summary>
        /// 是否已应用主题色
        /// </summary>
        public bool HasAppliedThemeColor => _hasAppliedThemeColor;

        /// <summary>
        /// 处理来自 WebView 的取色消息
        /// </summary>
        public bool TryHandleTintMessage(string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                
                if (!root.TryGetProperty("type", out JsonElement typeEl))
                {
                    return false;
                }

                string messageType = typeEl.GetString() ?? string.Empty;

                // 如果已经应用了 theme-color，跳过采样颜色
                if (messageType == "docked_ai_tint" && _hasAppliedThemeColor)
                {
                    return false;
                }

                // 检查是否透明
                bool isTransparent = root.TryGetProperty("isTransparent", out JsonElement transparentEl) && 
                                    transparentEl.GetBoolean();

                if (isTransparent)
                {
                    return false; // 需要截图采样
                }

                // 应用顶部和底部颜色
                if (root.TryGetProperty("top", out JsonElement topEl) &&
                    ColorService.TryParseCssColor(topEl.GetString(), out var topColor))
                {
                    ApplyBarTint(isTop: true, topColor);
                }

                if (root.TryGetProperty("bottom", out JsonElement bottomEl) &&
                    ColorService.TryParseCssColor(bottomEl.GetString(), out var bottomColor))
                {
                    ApplyBarTint(isTop: false, bottomColor);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 应用栏的着色
        /// </summary>
        public void ApplyBarTint(bool isTop, Color sampledColor)
        {
            var tinted = Color.FromArgb(byte.MaxValue, sampledColor.R, sampledColor.G, sampledColor.B);
            SolidColorBrush background = isTop ? _topBarBackgroundBrush : _bottomBarBackgroundBrush;
            SolidColorBrush foreground = isTop ? _topBarForegroundBrush : _bottomBarForegroundBrush;

            // 防闪烁逻辑
            if (!_hasReceivedFirstTint)
            {
                bool isCurrentlyTransparent = background.Color.A == 0 || 
                    (background.Color.R == 0 && background.Color.G == 0 && background.Color.B == 0);
                
                bool isPureWhite = sampledColor.R == 255 && sampledColor.G == 255 && sampledColor.B == 255;
                
                if (isCurrentlyTransparent && isPureWhite)
                {
                    System.Diagnostics.Debug.WriteLine("[BarThemeManager] 首次加载忽略纯白色");
                    return;
                }
                
                _hasReceivedFirstTint = true;
            }

            // 使用动画平滑过渡
            ColorService.AnimateColorChange(background, tinted);
            
            var contrastColor = ColorService.GetContrastingForeground(sampledColor);
            ColorService.AnimateColorChange(foreground, contrastColor);

            // 更新次要前景色
            if (isTop)
            {
                var secondaryColor = ColorService.CreateSecondaryColor(contrastColor);
                ColorService.AnimateColorChange(_topBarSecondaryForegroundBrush, secondaryColor);
            }
            else
            {
                // 更新底部栏的悬停和禁用状态颜色
                double luminance = ColorService.CalculateLuminance(sampledColor);
                double adjustFactor = luminance < 0.179 ? 0.2 : -0.2;
                var hoverColor = ColorService.AdjustColorBrightness(contrastColor, adjustFactor);
                
                ColorService.AnimateColorChange(_bottomBarHoverForegroundBrush, hoverColor);
                
                var disabledColor = ColorService.CreateSecondaryColor(contrastColor, 0.6);
                ColorService.AnimateColorChange(_bottomBarDisabledForegroundBrush, disabledColor);
            }
        }

        /// <summary>
        /// 应用系统强调色作为回退方案
        /// </summary>
        public void ApplySystemAccentColor()
        {
            try
            {
                if (Application.Current.Resources.TryGetValue("SystemAccentColor", out object? accentResource) 
                    && accentResource is Color accentColor)
                {
                    ApplyBarTint(isTop: true, accentColor);
                    ApplyBarTint(isTop: false, accentColor);
                    System.Diagnostics.Debug.WriteLine("[BarThemeManager] 应用系统强调色");
                }
            }
            catch
            {
                // 保持透明
            }
        }
    }
}
