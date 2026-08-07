using Microsoft.Web.WebView2.Core;
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - Tint 颜色采样和应用部分
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        private async Task EnsureTintScriptInstalledAsync()
        {
            if (WebView?.CoreWebView2 is null)
            {
                return;
            }

            string script = @"
(() => {
  if (window.__dockedAiTint) return;
  const state = { lastTop: null, lastBottom: null, scheduled: false, scrollDebounceTimer: null };
  
  function cssToRgbaArray(css) {
    if (!css) return null;
    const m = css.match(/rgba?\(([^)]+)\)/i);
    if (!m) return null;
    const parts = m[1].split(',').map(p => p.trim());
    if (parts.length < 3) return null;
    const r = parseFloat(parts[0]), g = parseFloat(parts[1]), b = parseFloat(parts[2]);
    const a = parts.length >= 4 ? parseFloat(parts[3]) : 1;
    return ![r,g,b,a].every(n => Number.isFinite(n)) ? null : [r, g, b, a];
  }
  
  function effectiveBg(el) {
    if (!el) return null;
    let cur = el, depth = 0;
    const minAlpha = 0.01, maxDepth = 20;
    while (cur && cur !== document && depth < maxDepth) {
      const style = getComputedStyle(cur);
      const bg = cssToRgbaArray(style.backgroundColor);
      if (bg && bg[3] > minAlpha) return bg;
      const bgImage = style.backgroundImage;
      if (bgImage && bgImage !== 'none') {
        const gradientMatch = bgImage.match(/rgba?\([^)]+\)/i);
        if (gradientMatch) {
          const gradientColor = cssToRgbaArray(gradientMatch[0]);
          if (gradientColor && gradientColor[3] > minAlpha) return gradientColor;
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
    const x = Math.max(1, Math.floor(window.innerWidth / 2));
    return effectiveBg(document.elementFromPoint(x, y));
  }
  
  function rgbaToCss(rgba) {
    if (!rgba) return null;
    const a = Math.max(0, Math.min(1, rgba[3]));
    return `rgba(${Math.round(rgba[0])},${Math.round(rgba[1])},${Math.round(rgba[2])},${a})`;
  }
  
  function post(topCss, bottomCss) {
    const msg = { type: 'DockedTools_tint', top: topCss, bottom: bottomCss, title: document.title || '', isTransparent: !topCss || !bottomCss };
    try { window.chrome?.webview?.postMessage(JSON.stringify(msg)); } catch (e) { console.warn('Tint post failed', e); }
  }
  
  function sendNow() {
    state.scheduled = false;
    const top = rgbaToCss(sampleAtY(1)), bottom = rgbaToCss(sampleAtY(Math.max(1, window.innerHeight - 2)));
    const isFirst = state.lastTop === null && state.lastBottom === null;
    if (!isFirst && top === state.lastTop && bottom === state.lastBottom) return;
    state.lastTop = top; state.lastBottom = bottom;
    post(top, bottom);
  }
  
  function schedule() {
    if (state.scheduled) return;
    state.scheduled = true;
    requestAnimationFrame(sendNow);
  }
  
  function scheduleWithDebounce() {
    if (state.scrollDebounceTimer) clearTimeout(state.scrollDebounceTimer);
    state.scrollDebounceTimer = setTimeout(() => { schedule(); state.scrollDebounceTimer = null; }, 300);
  }
  
  window.__dockedAiTint = { updateNow: schedule };
  window.addEventListener('scroll', scheduleWithDebounce, { passive: true });
  window.addEventListener('resize', schedule);
  document.addEventListener('readystatechange', schedule);
})();";

            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
        }

        private async Task CoreWebView2WebMessageReceivedAsync(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string json = e.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                if (!root.TryGetProperty("type", out JsonElement typeEl)) return;

                string messageType = typeEl.GetString() ?? string.Empty;

                if (string.Equals(messageType, ThemeColorMessageType, StringComparison.Ordinal))
                {
                    if (root.TryGetProperty("color", out JsonElement colorEl) &&
                        TryParseCssColor(colorEl.GetString(), out var themeColor))
                    {
                        _hasAppliedThemeColor = true;
                        ApplyBarTint(isTop: true, themeColor);
                        ApplyBarTint(isTop: false, themeColor);
                    }
                    return;
                }

                if (string.Equals(messageType, TintMessageType, StringComparison.Ordinal))
                {
                    if (_hasAppliedThemeColor) return;

                    bool isTransparent = root.TryGetProperty("isTransparent", out JsonElement transparentEl) && transparentEl.GetBoolean();
                    if (isTransparent)
                    {
                        await TryScreenshotSamplingAsync();
                        return;
                    }

                    if (root.TryGetProperty("top", out JsonElement topEl) && TryParseCssColor(topEl.GetString(), out var topColor))
                        ApplyBarTint(isTop: true, topColor);

                    if (root.TryGetProperty("bottom", out JsonElement bottomEl) && TryParseCssColor(bottomEl.GetString(), out var bottomColor))
                        ApplyBarTint(isTop: false, bottomColor);
                }
            }
            catch { }
        }

        private void ApplyBarTint(bool isTop, Windows.UI.Color sampledColor)
        {
            var tinted = Windows.UI.Color.FromArgb(byte.MaxValue, sampledColor.R, sampledColor.G, sampledColor.B);
            var background = isTop ? _topBarBackgroundBrush : _bottomBarBackgroundBrush;
            var foreground = isTop ? _topBarForegroundBrush : _bottomBarForegroundBrush;

            if (!_hasReceivedFirstTint)
            {
                bool isInitial = background.Color.A <= 1 && background.Color.R == 0 && background.Color.G == 0 && background.Color.B == 0;
                bool isPureWhite = sampledColor.R == 255 && sampledColor.G == 255 && sampledColor.B == 255;
                if (isInitial && isPureWhite) return;
                _hasReceivedFirstTint = true;
            }

            AnimateColorChange(background, tinted);
            var contrastColor = GetContrastingForeground(sampledColor);
            AnimateColorChange(foreground, contrastColor);

            if (isTop)
            {
                var secondaryColor = Windows.UI.Color.FromArgb((byte)(contrastColor.A * 0.7), contrastColor.R, contrastColor.G, contrastColor.B);
                AnimateColorChange(_topBarSecondaryForegroundBrush, secondaryColor);
                if (_topBarTitle != null) _topBarTitle.Foreground = _topBarForegroundBrush;
                if (_topBarIconFallback != null) _topBarIconFallback.Foreground = _topBarSecondaryForegroundBrush;
                if (_unpinButton?.Content is FontIcon unpinIcon) unpinIcon.Foreground = _topBarForegroundBrush;
                Features.UnifiedCalls.TopAppBar.TopAppBarService.SetForeground(_topBarForegroundBrush);
            }
            else
            {
                double luminance = CalculateLuminance(sampledColor);
                bool isDark = luminance < LuminanceThreshold;
                var hoverColor = CreateStateOverlayColor(contrastColor, isDark ? ButtonHoverOverlayStrength : -ButtonHoverOverlayStrength);
                AnimateColorChange(_bottomBarHoverForegroundBrush, hoverColor);
                var disabledColor = Windows.UI.Color.FromArgb((byte)(contrastColor.A * ButtonDisabledOpacity), contrastColor.R, contrastColor.G, contrastColor.B);
                AnimateColorChange(_bottomBarDisabledForegroundBrush, disabledColor);
            }
        }

        private static void RestoreSharedTopAppBarBackground()
        {
            Features.UnifiedCalls.TopAppBar.TopAppBarService.ResetBackground();
            Features.UnifiedCalls.TopAppBar.TopAppBarService.ResetForeground();
            Features.UnifiedCalls.TopAppBar.TopAppBarService.ResetChromeVisibility();
        }

        private async Task TryApplyThemeColorAsync()
        {
            if (WebView?.CoreWebView2 is null) return;
            try
            {
                string script = @"(function() { const meta = document.querySelector('meta[name=""theme-color""]'); return meta?.content || null; })();";
                string result = await WebView.CoreWebView2.ExecuteScriptAsync(script);
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to get theme-color: {ex.Message}"); }
        }

        private async Task TriggerTintSamplingAsync()
        {
            if (WebView?.CoreWebView2 is null) return;
            try
            {
                string script = @"(function(){function c(s){if(!s)return null;const m=s.match(/rgba?\(([^)]+)\)/i);if(!m)return null;const p=m[1].split(',').map(x=>x.trim());if(p.length<3)return null;const r=parseFloat(p[0]),g=parseFloat(p[1]),b=parseFloat(p[2]),a=p.length>=4?parseFloat(p[3]):1;return[r,g,b,a].every(n=>Number.isFinite(n))?[r,g,b,a]:null}function e(el){if(!el)return null;let cur=el,d=0;while(cur&&cur!==document&&d<20){const st=getComputedStyle(cur),bg=c(st.backgroundColor);if(bg&&bg[3]>0.01)return bg;const bgi=st.backgroundImage;if(bgi&&bgi!=='none'){const gm=bgi.match(/rgba?\([^)]+\)/i);if(gm){const gc=c(gm[0]);if(gc&&gc[3]>0.01)return gc}}cur=cur.parentElement;d++}if(document.body){const bb=c(getComputedStyle(document.body).backgroundColor);if(bb&&bb[3]>0.01)return bb}if(document.documentElement){const hb=c(getComputedStyle(document.documentElement).backgroundColor);if(hb&&hb[3]>0.01)return hb}return null}function s(y){const x=Math.max(1,Math.floor(window.innerWidth/2));return e(document.elementFromPoint(x,y))}function r(a){if(!a)return null;const al=Math.max(0,Math.min(1,a[3]));return'rgba('+Math.round(a[0])+','+Math.round(a[1])+','+Math.round(a[2])+','+al+')'}const tc=s(1),bc=s(Math.max(1,window.innerHeight-2)),t=r(tc),b=r(bc);try{window.chrome?.webview?.postMessage(JSON.stringify({type:'DockedTools_tint',top:t,bottom:b,title:document.title||'',isTransparent:!t||!b}));return'sent:'+t+','+b}catch(ex){return'error:'+ex.message}})();";
                string result = await WebView.CoreWebView2.ExecuteScriptAsync(script);
                System.Diagnostics.Debug.WriteLine($"[TriggerTintSamplingAsync] {result}");
                await Task.Delay(50);
                await WebView.CoreWebView2.ExecuteScriptAsync(@"window.__dockedAiTint?.updateNow?.();");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[TriggerTintSamplingAsync] {ex.Message}"); }
        }

        private async Task TryScreenshotSamplingAsync()
        {
            if (WebView?.CoreWebView2 is null) return;
            try
            {
                using var stream = new InMemoryRandomAccessStream();
                await WebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
                stream.Seek(0);
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                var pixelData = await decoder.GetPixelDataAsync();
                byte[] pixels = pixelData.DetachPixelData();
                uint width = decoder.PixelWidth, height = decoder.PixelHeight;
                if (width == 0 || height == 0) return;

                var topColor = SampleRegion(pixels, width, height, 0, 10);
                if (topColor.HasValue) ApplyBarTint(isTop: true, topColor.Value);

                var bottomColor = SampleRegion(pixels, width, height, (int)height - 10, (int)height);
                if (bottomColor.HasValue) ApplyBarTint(isTop: false, bottomColor.Value);

                _hasReceivedFirstTint = true;
                System.Diagnostics.Debug.WriteLine("Applied screenshot sampling");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Screenshot sampling failed: {ex.Message}");
                ApplySystemAccentColor();
            }
        }

        private Windows.UI.Color? SampleRegion(byte[] pixels, uint width, uint height, int startY, int endY)
        {
            if (pixels.Length == 0 || width == 0 || height == 0) return null;
            startY = Math.Max(0, startY);
            endY = Math.Min((int)height, endY);
            int startX = (int)(width * 0.25), endX = (int)(width * 0.75);
            long sumR = 0, sumG = 0, sumB = 0;
            int count = 0;

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    int index = (y * (int)width + x) * 4;
                    if (index + 3 < pixels.Length)
                    {
                        byte b = pixels[index], g = pixels[index + 1], r = pixels[index + 2], a = pixels[index + 3];
                        if (a > 10) { sumR += r; sumG += g; sumB += b; count++; }
                    }
                }
            }
            return count == 0 ? null : Windows.UI.Color.FromArgb(255, (byte)(sumR / count), (byte)(sumG / count), (byte)(sumB / count));
        }

        private async Task ReInjectTintScriptAsync()
        {
            if (WebView?.CoreWebView2 == null) return;
            try
            {
                System.Diagnostics.Debug.WriteLine("[ReInjectTintScriptAsync] 重新注入");
                await Task.Delay(100);
                await EnsureTintScriptInstalledAsync();
                await RefreshPageTintAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ReInjectTintScriptAsync] {ex.Message}"); }
        }

        private async Task RefreshPageTintAsync()
        {
            if (WebView?.CoreWebView2 == null) return;
            try
            {
                _hasReceivedFirstTint = false;
                _hasAppliedThemeColor = false;
                await Task.Delay(200);
                await TryApplyThemeColorAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RefreshPageTintAsync] {ex.Message}");
                ApplySystemAccentColor();
            }
        }

        private static bool TryParseCssColor(string? cssColor, out Windows.UI.Color color)
        {
            color = Microsoft.UI.Colors.Transparent;
            if (string.IsNullOrWhiteSpace(cssColor)) return false;

            string s = cssColor.Trim();
            if (s.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) || s.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
            {
                int start = s.IndexOf('('), end = s.IndexOf(')');
                if (start < 0 || end <= start) return false;
                string[] parts = s.Substring(start + 1, end - start - 1).Split(',');
                if (parts.Length < 3) return false;
                if (!TryParseByte(parts[0], out byte r) || !TryParseByte(parts[1], out byte g) || !TryParseByte(parts[2], out byte b)) return false;
                color = Windows.UI.Color.FromArgb(byte.MaxValue, r, g, b);
                return true;
            }

            if (s.StartsWith('#') && s.Length == 7 &&
                byte.TryParse(s.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r2) &&
                byte.TryParse(s.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g2) &&
                byte.TryParse(s.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b2))
            {
                color = Windows.UI.Color.FromArgb(byte.MaxValue, r2, g2, b2);
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
                if (!double.TryParse(trimmed.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double percent)) return false;
                percent = Math.Max(0, Math.Min(PercentageMax, percent));
                value = (byte)Math.Round(percent / PercentageMax * ColorChannelMax);
                return true;
            }
            if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double raw)) return false;
            raw = Math.Max(0, Math.Min(ColorChannelMax, raw));
            value = (byte)Math.Round(raw);
            return true;
        }
    }
}
