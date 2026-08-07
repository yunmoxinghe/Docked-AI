using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 取色采样策略模块
    /// 包含多层取色策略：meta theme-color → JavaScript采样 → 截图采样 → 系统强调色
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        /// <summary>
        /// 分层策略第一步：尝试从 meta[name="theme-color"] 获取主题色
        /// </summary>
        private async Task TryApplyThemeColorAsync()
        {
            if (WebView?.CoreWebView2 is null)
            {
                return;
            }

            try
            {
                string script = @"
(function() {
    const meta = document.querySelector('meta[name=""theme-color""]');
    if (meta && meta.content) {
        return meta.content;
    }
    return null;
})();";

                string result = await WebView.CoreWebView2.ExecuteScriptAsync(script);
                
                // 移除 JSON 字符串的引号
                if (!string.IsNullOrWhiteSpace(result) && result != "null")
                {
                    string colorString = result.Trim('"');
                    if (TryParseCssColor(colorString, out var themeColor))
                    {
                        _hasAppliedThemeColor = true;
                        ApplyBarTint(isTop: true, themeColor);
                        ApplyBarTint(isTop: false, themeColor);
                        System.Diagnostics.Debug.WriteLine($"Applied theme-color: {colorString}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get theme-color: {ex.Message}");
            }
        }


        /// <summary>
        /// 主动触发一次采样取色（用于首次加载页面）
        /// </summary>
        private async Task TriggerTintSamplingAsync()
        {
            if (WebView?.CoreWebView2 is null)
            {
                return;
            }

            try
            {
                // ✅ 方案1：直接在 C# 这边执行完整的采样逻辑（不依赖脚本状态）
                string script = @"
(function() {
    // 完整复制采样逻辑，确保立即执行
    function cssToRgbaArray(css) {
        if (!css) return null;
        const m = css.match(/rgba?\(([^)]+)\)/i);
        if (!m) return null;
        const parts = m[1].split(',').map(p => p.trim());
        if (parts.length < 3) return null;
        const r = parseFloat(parts[0]);
        const g = parseFloat(parts[1]);
        const b = parseFloat(parts[2]);
        const a = parts.length >= 4 ? parseFloat(parts[3]) : 1;
        if (![r,g,b,a].every(n => Number.isFinite(n))) return null;
        return [r, g, b, a];
    }
    
    function effectiveBg(el) {
        if (!el) return null;
        let cur = el;
        const minAlpha = 0.01;
        const maxDepth = 20;
        let depth = 0;
        
        while (cur && cur !== document && depth < maxDepth) {
            const style = getComputedStyle(cur);
            const bg = cssToRgbaArray(style.backgroundColor);
            
            if (bg && bg[3] > minAlpha) {
                return bg;
            }
            
            const bgImage = style.backgroundImage;
            if (bgImage && bgImage !== 'none') {
                const gradientMatch = bgImage.match(/rgba?\([^)]+\)/i);
                if (gradientMatch) {
                    const gradientColor = cssToRgbaArray(gradientMatch[0]);
                    if (gradientColor && gradientColor[3] > minAlpha) {
                        return gradientColor;
                    }
                }
            }
            
            cur = cur.parentElement;
            depth++;
        }
        
        if (document.body) {
            const bodyBg = cssToRgbaArray(getComputedStyle(document.body).backgroundColor);
            if (bodyBg && bodyBg[3] > minAlpha) return bodyBg;
        }
        
        if (document.documentElement) {
            const htmlBg = cssToRgbaArray(getComputedStyle(document.documentElement).backgroundColor);
            if (htmlBg && htmlBg[3] > minAlpha) return htmlBg;
        }
        
        return null;
    }
    
    function sampleAtY(y) {
        const minX = 1;
        const x = Math.max(minX, Math.floor(window.innerWidth / 2));
        const el = document.elementFromPoint(x, y);
        return effectiveBg(el);
    }
    
    function rgbaToCss(rgba) {
        if (!rgba) return null;
        const a = Math.max(0, Math.min(1, rgba[3]));
        return 'rgba(' + Math.round(rgba[0]) + ',' + Math.round(rgba[1]) + ',' + Math.round(rgba[2]) + ',' + a + ')';
    }
    
    // 立即采样
    const topColor = sampleAtY(1);
    const bottomColor = sampleAtY(Math.max(1, window.innerHeight - 2));
    const top = rgbaToCss(topColor);
    const bottom = rgbaToCss(bottomColor);
    
    // 发送消息
    const msg = { 
        type: 'DockedTools_tint', 
        top: top, 
        bottom: bottom, 
        title: (document.title || ''),
        isTransparent: !top || !bottom
    };
    
    try {
        window.chrome?.webview?.postMessage(JSON.stringify(msg));
        return 'sent: top=' + top + ', bottom=' + bottom;
    } catch (error) {
        return 'error: ' + error.message;
    }
})();";

                string result = await WebView.CoreWebView2.ExecuteScriptAsync(script);
                System.Diagnostics.Debug.WriteLine($"[TriggerTintSamplingAsync] 立即采样结果: {result}");
                
                // ✅ 方案2（备选）：如果脚本已经准备好，也调用一次
                await Task.Delay(50);
                await WebView.CoreWebView2.ExecuteScriptAsync(@"
                    if (window.__dockedAiTint && typeof window.__dockedAiTint.updateNow === 'function') {
                        window.__dockedAiTint.updateNow();
                    }
                ");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TriggerTintSamplingAsync] 触发失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 分层策略终极方案：截图采样（仅在页面完全透明时使用）
        /// </summary>
        private async Task TryScreenshotSamplingAsync()
        {
            if (WebView?.CoreWebView2 is null)
            {
                return;
            }

            try
            {
                using var stream = new InMemoryRandomAccessStream();
                await WebView.CoreWebView2.CapturePreviewAsync(
                    CoreWebView2CapturePreviewImageFormat.Png, 
                    stream);

                stream.Seek(0);
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                var pixelData = await decoder.GetPixelDataAsync();
                byte[] pixels = pixelData.DetachPixelData();

                uint width = decoder.PixelWidth;
                uint height = decoder.PixelHeight;

                if (width == 0 || height == 0)
                {
                    return;
                }

                // 采样顶部 10 行的中心区域
                var topColor = SampleRegion(pixels, width, height, 0, 10);
                if (topColor.HasValue)
                {
                    ApplyBarTint(isTop: true, topColor.Value);
                }

                // 采样底部 10 行的中心区域
                var bottomColor = SampleRegion(pixels, width, height, (int)height - 10, (int)height);
                if (bottomColor.HasValue)
                {
                    ApplyBarTint(isTop: false, bottomColor.Value);
                }

                _hasReceivedFirstTint = true;
                System.Diagnostics.Debug.WriteLine("Applied screenshot sampling colors");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Screenshot sampling failed: {ex.Message}");
                // Fallback 到系统主题色
                ApplySystemAccentColor();
            }
        }

        /// <summary>
        /// 从像素数据中采样指定区域的平均颜色
        /// </summary>
        private Windows.UI.Color? SampleRegion(byte[] pixels, uint width, uint height, int startY, int endY)
        {
            if (pixels.Length == 0 || width == 0 || height == 0)
            {
                return null;
            }

            startY = Math.Max(0, startY);
            endY = Math.Min((int)height, endY);

            // 采样中心 50% 的宽度
            int startX = (int)(width * 0.25);
            int endX = (int)(width * 0.75);

            long sumR = 0, sumG = 0, sumB = 0;
            int count = 0;
            int bytesPerPixel = 4; // BGRA

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    int index = (y * (int)width + x) * bytesPerPixel;
                    if (index + 3 < pixels.Length)
                    {
                        byte b = pixels[index];
                        byte g = pixels[index + 1];
                        byte r = pixels[index + 2];
                        byte a = pixels[index + 3];

                        // 忽略透明像素
                        if (a > 10)
                        {
                            sumR += r;
                            sumG += g;
                            sumB += b;
                            count++;
                        }
                    }
                }
            }

            if (count == 0)
            {
                return null;
            }

            return Windows.UI.Color.FromArgb(
                255,
                (byte)(sumR / count),
                (byte)(sumG / count),
                (byte)(sumB / count)
            );
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
    }
}
