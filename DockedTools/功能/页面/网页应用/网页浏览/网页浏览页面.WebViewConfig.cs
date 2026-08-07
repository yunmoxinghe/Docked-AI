using DockedTools.Features.Pages.Settings;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - WebView配置模块
    /// 包含右键菜单配置、性能设置、脚本注入等
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        private void OnWinUIContextMenuSettingsChanged(object? sender, EventArgs e)
        {
            // 设置改变时，更新右键菜单配置
            bool useWinUIContextMenu = ExperimentalSettings.EnableWinUIContextMenu;
            
            // 更新 WebView 的配置
            if (WebView?.CoreWebView2 != null)
            {
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = !useWinUIContextMenu;
                UpdateContextMenuForWebView(WebView, useWinUIContextMenu);
            }
        }

        private void OnWebViewPerformanceSettingsChanged(object? sender, EventArgs e)
        {
            // 性能设置改变时，应用新设置
            // 注意：某些设置需要重启 WebView 才能生效（如浏览器参数）
            ApplyMemoryModeSettings();
            
            System.Diagnostics.Debug.WriteLine("[OnWebViewPerformanceSettingsChanged] 性能设置已更新，某些设置需要重新加载页面才能生效");
        }

        // ⚠️ UpdateContextMenuConfiguration、UpdateContextMenuForWebView已移至 网页浏览页面.ContextMenu.cs

        private async Task EnsureTintScriptInstalledAsync()
        {
            if (WebView?.CoreWebView2 is null)
            {
                return;
            }

            // 增强版取色脚本：递归向上查找、支持渐变、图片背景等复杂场景
            string script = @"
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
    const maxDepth = 20; // 防止无限循环
    let depth = 0;
    
    while (cur && cur !== document && depth < maxDepth) {
      const style = getComputedStyle(cur);
      const bg = cssToRgbaArray(style.backgroundColor);
      
      // 找到不透明的背景色
      if (bg && bg[3] > minAlpha) {
        return bg;
      }
      
      // 检查是否有渐变背景（取渐变起始色）
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
    
    // 最终回退：返回 null 表示透明，让宿主决定
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
      type: 'DockedTools_tint', 
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
    
    // 滚动时只采样顶部，底部保持不变（大多数页面底部栏固定）
    const bottomColor = sampleAtY(Math.max(minY, window.innerHeight - 2));
    
    const top = rgbaToCss(topColor);
    const bottom = rgbaToCss(bottomColor);
    
    // ✅ 修复：首次采样时即使是 null 也要发送（告诉宿主页面是透明的）
    // 之后的采样才需要去重
    const isFirstSample = (state.lastTop === null && state.lastBottom === null);
    if (!isFirstSample && top === state.lastTop && bottom === state.lastBottom) return;
    
    state.lastTop = top;
    state.lastBottom = bottom;
    post(top, bottom);
  }
  
  function schedule() {
    if (state.scheduled) return;
    state.scheduled = true;
    requestAnimationFrame(sendNow);
  }
  
  // 滚动时使用防抖，避免频繁采样
  function scheduleWithDebounce() {
    if (state.scrollDebounceTimer) {
      clearTimeout(state.scrollDebounceTimer);
    }
    state.scrollDebounceTimer = setTimeout(() => {
      schedule();
      state.scrollDebounceTimer = null;
    }, 300); // 300ms 防抖
  }
  
  window.__dockedAiTint = { updateNow: schedule };
  
  // 滚动使用防抖版本
  window.addEventListener('scroll', scheduleWithDebounce, { passive: true });
  
  // 其他事件立即触发
  window.addEventListener('resize', schedule);
  document.addEventListener('readystatechange', schedule);
  
  // ✅ 修复：不在脚本加载时自动触发，完全由 C# 控制首次采样时机
  // 只监听 DOMContentLoaded 和 load 事件，但不立即执行
  // document.addEventListener('DOMContentLoaded', schedule);
  // window.addEventListener('load', schedule);
  
  // 注释掉自动触发，避免过早采样导致黑屏闪现
  // if (document.readyState === 'complete') {
  //   schedule();
  // } else {
  //   window.addEventListener('load', () => {
  //     setTimeout(schedule, 100);
  //   }, { once: true });
  // }
})();";

            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
        }
    }
}
