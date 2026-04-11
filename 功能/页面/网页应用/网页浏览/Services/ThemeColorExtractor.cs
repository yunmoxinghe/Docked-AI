using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI;

namespace Docked_AI.Features.Pages.WebApp.Browser.Services
{
    /// <summary>
    /// 主题色提取服务，负责从网页中提取主题颜色
    /// </summary>
    public class ThemeColorExtractor
    {
        /// <summary>
        /// 从 meta[name="theme-color"] 标签获取主题色
        /// </summary>
        public static async Task<Color?> TryGetThemeColorAsync(CoreWebView2 coreWebView)
        {
            if (coreWebView == null)
            {
                return null;
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

                string result = await coreWebView.ExecuteScriptAsync(script);
                
                if (!string.IsNullOrWhiteSpace(result) && result != "null")
                {
                    string colorString = result.Trim('"');
                    if (ColorService.TryParseCssColor(colorString, out var themeColor))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ThemeColorExtractor] 提取到 theme-color: {colorString}");
                        return themeColor;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeColorExtractor] 获取 theme-color 失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 通过截图采样获取颜色（终极方案）
        /// </summary>
        public static async Task<(Color? topColor, Color? bottomColor)> TryScreenshotSamplingAsync(CoreWebView2 coreWebView)
        {
            if (coreWebView == null)
            {
                return (null, null);
            }

            try
            {
                using var stream = new InMemoryRandomAccessStream();
                await coreWebView.CapturePreviewAsync(
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
                    return (null, null);
                }

                // 采样顶部和底部区域
                var topColor = SampleRegion(pixels, width, height, 0, 10);
                var bottomColor = SampleRegion(pixels, width, height, (int)height - 10, (int)height);

                System.Diagnostics.Debug.WriteLine("[ThemeColorExtractor] 截图采样完成");
                return (topColor, bottomColor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeColorExtractor] 截图采样失败: {ex.Message}");
                return (null, null);
            }
        }

        /// <summary>
        /// 从像素数据中采样指定区域的平均颜色
        /// </summary>
        private static Color? SampleRegion(byte[] pixels, uint width, uint height, int startY, int endY)
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

            return Color.FromArgb(
                255,
                (byte)(sumR / count),
                (byte)(sumG / count),
                (byte)(sumB / count)
            );
        }
    }
}
