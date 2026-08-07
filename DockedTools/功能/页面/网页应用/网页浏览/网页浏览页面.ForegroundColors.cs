using DockedTools.Features.UnifiedCalls.AsyncSafety;
using DockedTools.Features.UnifiedCalls.TopAppBar;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Threading.Tasks;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 前景色管理模块
    /// 包含前景色初始化、主题切换响应、系统主题色应用等
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
                        await TryApplyThemeColorMetaTagAsync();
                        
                        // ✅ 步骤2：尝试 JavaScript 取色（如果步骤1失败）
                        if (!_hasAppliedThemeColor)
                        {
                            System.Diagnostics.Debug.WriteLine("[WebBrowserPage] theme-color meta 未找到，使用脚本取色");
                            await TrySampleTopBarColorAsync();
                        }
                        
                        // ✅ 步骤3：如果都失败，回退到系统主题色
                        if (!_hasAppliedThemeColor)
                        {
                            System.Diagnostics.Debug.WriteLine("[WebBrowserPage] 脚本取色失败，回退到系统主题色");
                            ApplySystemThemeColors();
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 取色后背景色: Top={_topBarBackgroundBrush.Color}, Bottom={_bottomBarBackgroundBrush.Color}");
                    },
                    "[OnSystemThemeChanged]"
                );
            }
            else
            {
                // WebView 未就绪，直接使用系统主题色
                System.Diagnostics.Debug.WriteLine("[WebBrowserPage] WebView 未就绪，应用系统主题色");
                ApplySystemThemeColors();
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
    }
}
