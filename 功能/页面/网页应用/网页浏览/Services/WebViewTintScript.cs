namespace Docked_AI.Features.Pages.WebApp.Browser.Services
{
    /// <summary>
    /// WebView 取色脚本管理
    /// </summary>
    public static class WebViewTintScript
    {
        /// <summary>
        /// 获取增强版取色脚本
        /// 支持递归向上查找、渐变、图片背景等复杂场景
        /// </summary>
        public static string GetTintScript()
        {
            return @"
(() => {
  if (window.__dockedAiTint) return;
  const state = { 
    lastTop: null, 
    lastBottom: null, 
    scheduled: false,
    scrollDebounceTimer: null 
  };
  
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
  
  // 增强版：递归向上查找有效背景色
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
      
      // 检查渐变背景
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
    
    // 回退到 body
    if (document.body) {
      const bodyBg = cssToRgbaArray(getComputedStyle(document.body).backgroundColor);
      if (bodyBg && bodyBg[3] > minAlpha) return bodyBg;
    }
    
    // 回退到 html
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
    const minAlpha = 0;
    const maxAlpha = 1;
    const a = Math.max(minAlpha, Math.min(maxAlpha, rgba[3]));
    return `rgba(${Math.round(rgba[0])},${Math.round(rgba[1])},${Math.round(rgba[2])},${a})`;
  }
  
  function post(topCss, bottomCss) {
    const msg = { 
      type: 'docked_ai_tint', 
      top: topCss, 
      bottom: bottomCss, 
      title: (document.title || ''),
      isTransparent: !topCss || !bottomCss
    };
    try {
      window.chrome?.webview?.postMessage(JSON.stringify(msg));
    } catch (error) {
      console.warn('Failed to post tint message to host.', error);
    }
  }
  
  function sendNow() {
    state.scheduled = false;
    const minY = 1;
    const topColor = sampleAtY(minY);
    const bottomColor = sampleAtY(Math.max(minY, window.innerHeight - 2));
    
    const top = rgbaToCss(topColor);
    const bottom = rgbaToCss(bottomColor);
    
    if (top === state.lastTop && bottom === state.lastBottom) return;
    state.lastTop = top;
    state.lastBottom = bottom;
    post(top, bottom);
  }
  
  function schedule() {
    if (state.scheduled) return;
    state.scheduled = true;
    requestAnimationFrame(sendNow);
  }
  
  function scheduleWithDebounce() {
    if (state.scrollDebounceTimer) {
      clearTimeout(state.scrollDebounceTimer);
    }
    state.scrollDebounceTimer = setTimeout(() => {
      schedule();
      state.scrollDebounceTimer = null;
    }, 300);
  }
  
  window.__dockedAiTint = { updateNow: schedule };
  
  window.addEventListener('scroll', scheduleWithDebounce, { passive: true });
  window.addEventListener('resize', schedule);
  document.addEventListener('readystatechange', schedule);
  document.addEventListener('DOMContentLoaded', schedule);
  window.addEventListener('load', schedule);
  
  schedule();
})();";
        }
    }
}
