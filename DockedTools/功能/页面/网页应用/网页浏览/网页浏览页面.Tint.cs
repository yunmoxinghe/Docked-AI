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

        // ⚠️ CoreWebView2WebMessageReceivedAsync已移至 网页浏览页面.MessageHandling.cs
        // ⚠️ ApplyBarTint、RestoreSharedTopAppBarBackground已移至 网页浏览页面.TintApplication.cs

        // ⚠️ TryApplyThemeColorAsync已移至 网页浏览页面.Sampling.cs
        // ⚠️ TriggerTintSamplingAsync已移至 网页浏览页面.Sampling.cs
        // ⚠️ TryScreenshotSamplingAsync已移至 网页浏览页面.Sampling.cs
        // ⚠️ SampleRegion已移至 网页浏览页面.Sampling.cs
        // ⚠️ ReInjectTintScriptAsync已移至 网页浏览页面.WebView.cs
        // ⚠️ RefreshPageTintAsync已移至 网页浏览页面.WebView.cs

        // ⚠️ TryParseCssColor、TryParseByte已移至 网页浏览页面.ColorUtils.cs
    }
}


