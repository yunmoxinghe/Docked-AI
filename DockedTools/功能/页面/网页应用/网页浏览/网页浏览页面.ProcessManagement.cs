using DockedTools.Features.UnifiedCalls.Logging;
using Microsoft.Web.WebView2.Core;
using System;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 进程管理模块
    /// 包含浏览器进程退出和故障恢复逻辑
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        private void CoreWebView2Environment_BrowserProcessExited(object? sender, CoreWebView2BrowserProcessExitedEventArgs e)
        {
            // TODO: 任务 3.3 将实现完整的日志记录逻辑
            // TODO: 任务 3.4 将实现恢复策略
            System.Diagnostics.Debug.WriteLine($"[CoreWebView2Environment_BrowserProcessExited] 浏览器进程退出 (占位方法)");
        }
        
        /// <summary>
        /// ⭐ 任务 3.3：WebView2 进程失败事件处理器（已完成）
        /// 记录 ProcessFailedKind、Reason、当前 URL、Shortcut ID、是否正在恢复等诊断信息
        /// 捕获 handler 内部异常，避免二次崩溃
        /// </summary>
        private void CoreWebView2_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
        {
            try
            {
                // ✅ 记录 ProcessFailedKind
                var processFailedKind = e.ProcessFailedKind;
                
                // ✅ 记录 Reason
                var reason = e.Reason;
                
                // ✅ 记录当前 URL
                string? currentUrl = null;
                try
                {
                    currentUrl = WebView?.CoreWebView2?.Source ?? _pendingNavigationUri?.ToString() ?? "未知";
                }
                catch
                {
                    currentUrl = "无法获取";
                }
                
                // ✅ 记录 Shortcut ID
                var shortcutId = _currentShortcut?.Id ?? "null";
                var shortcutName = _currentShortcut?.Name ?? "未知";
                
                // ✅ 记录是否正在恢复（通过检查相关标志）
                var isRecovering = _needsWebViewRecreation ? "是" : "否";
                
                // 记录进程描述信息（如果可用）
                var processDescription = !string.IsNullOrEmpty(e.ProcessDescription) 
                    ? e.ProcessDescription 
                    : "无描述";
                
                // 记录 ExitCode（如果可用）
                int? exitCode = null;
                try
                {
                    exitCode = e.ExitCode;
                }
                catch
                {
                    // ExitCode 可能不可用（某些失败类型）
                }
                
                // ✅ 构建详细的日志消息（包含所有需求字段）
                var logMessage = $"WebView2 进程失败\n" +
                                $"  ProcessFailedKind: {processFailedKind}\n" +
                                $"  Reason: {reason}\n" +
                                $"  ProcessDescription: {processDescription}\n" +
                                $"  ExitCode: {(exitCode.HasValue ? exitCode.Value.ToString() : "N/A")}\n" +
                                $"  当前 URL: {currentUrl}\n" +
                                $"  Shortcut ID: {shortcutId}\n" +
                                $"  Shortcut 名称: {shortcutName}\n" +
                                $"  是否正在恢复: {isRecovering}\n" +
                                $"  IsDisposed: {_isDisposed}\n" +
                                $"  IsWebViewReady: {_isWebViewReady}\n" +
                                $"  实例 ID: {_instanceId}";
                
                // ✅ 输出到调试控制台
                System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] {logMessage}");
                
                // ✅ 使用 LogService 记录到文件（需求：2.3.2）
                Features.UnifiedCalls.Logging.LogService.Error(
                    "WebView2.ProcessFailed",
                    logMessage);
                
                // ⭐ 任务 3.4：实现恢复策略（需求：2.3.3、3.2.1、3.2.2、3.2.3）
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    try
                    {
                        // ⭐ 任务 3.5：防重入检查 - 如果正在恢复中，直接返回（需求：2.3.3）
                        if (_isRecoveringWebView)
                        {
                            System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ⚠️ 正在恢复中，忽略本次事件以防止重入");
                            Features.UnifiedCalls.Logging.LogService.Warning(
                                "WebView2.ProcessFailed",
                                $"防重入保护：忽略 {processFailedKind} 事件（恢复正在进行中）");
                            return;
                        }
                        
                        switch (processFailedKind)
                        {
                            case CoreWebView2ProcessFailedKind.RenderProcessExited:
                                // ⭐ 渲染进程退出：优先调用 Reload（需求：3.2.1）
                                System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] 检测到 RenderProcessExited，尝试 Reload");
                                
                                // ⭐ 任务 3.5：设置恢复标志
                                _isRecoveringWebView = true;
                                
                                if (WebView?.CoreWebView2 != null)
                                {
                                    try
                                    {
                                        WebView.CoreWebView2.Reload();
                                        System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ Reload 已调用");
                                        
                                        // 重置无响应计数器（Reload 后重置）
                                        _unresponsiveCount = 0;
                                    }
                                    catch (Exception reloadEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] ❌ Reload 失败: {reloadEx.Message}");
                                        Features.UnifiedCalls.Logging.LogService.Error(
                                            "WebView2.ProcessFailed.Reload",
                                            "渲染进程退出后尝试 Reload 失败",
                                            reloadEx);
                                    }
                                    finally
                                    {
                                        // ⭐ 任务 3.5：恢复结束后重置 guard
                                        _isRecoveringWebView = false;
                                        System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ 恢复标志已重置（RenderProcessExited）");
                                    }
                                }
                                else
                                {
                                    // WebView 不可用，重置标志
                                    _isRecoveringWebView = false;
                                }
                                break;
                            
                            case CoreWebView2ProcessFailedKind.BrowserProcessExited:
                                // ⭐ 主浏览器进程退出：标记需要重建，关闭旧 WebView（需求：3.2.2）
                                System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] 检测到 BrowserProcessExited，标记需要重建 WebView");
                                
                                // ⭐ 任务 3.5：设置恢复标志
                                _isRecoveringWebView = true;
                                
                                _needsWebViewRecreation = true;
                                
                                // 关闭旧 WebView（清理资源）
                                try
                                {
                                    if (WebView?.CoreWebView2 != null)
                                    {
                                        WebView.CoreWebView2.ProcessFailed -= CoreWebView2_ProcessFailed;
                                        WebView.Close();
                                        System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ 旧 WebView 已关闭");
                                    }
                                    
                                    // 重新创建 WebView
                                    RecreateWebView();
                                    _needsWebViewRecreation = false;
                                    
                                    System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ WebView 已重建");
                                }
                                catch (Exception recreateEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] ❌ 重建 WebView 失败: {recreateEx.Message}");
                                    Features.UnifiedCalls.Logging.LogService.Error(
                                        "WebView2.ProcessFailed.Recreate",
                                        "主浏览器进程退出后尝试重建 WebView 失败",
                                        recreateEx);
                                }
                                finally
                                {
                                    // ⭐ 任务 3.5：恢复结束后重置 guard
                                    _isRecoveringWebView = false;
                                    System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ 恢复标志已重置（BrowserProcessExited）");
                                }
                                break;
                            
                            case CoreWebView2ProcessFailedKind.RenderProcessUnresponsive:
                                // ⭐ 渲染进程无响应：记录次数，连续多次后 reload（需求：3.2.3）
                                _unresponsiveCount++;
                                System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] 检测到 RenderProcessUnresponsive，计数: {_unresponsiveCount}/{MaxUnresponsiveCountBeforeReload}");
                                
                                if (_unresponsiveCount >= MaxUnresponsiveCountBeforeReload)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] 连续无响应达到 {MaxUnresponsiveCountBeforeReload} 次，触发 Reload");
                                    
                                    // ⭐ 任务 3.5：设置恢复标志
                                    _isRecoveringWebView = true;
                                    
                                    if (WebView?.CoreWebView2 != null)
                                    {
                                        try
                                        {
                                            WebView.CoreWebView2.Reload();
                                            System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ Reload 已调用（无响应恢复）");
                                            
                                            // 重置计数器
                                            _unresponsiveCount = 0;
                                        }
                                        catch (Exception reloadEx)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] ❌ Reload 失败（无响应恢复）: {reloadEx.Message}");
                                            Features.UnifiedCalls.Logging.LogService.Error(
                                                "WebView2.ProcessFailed.UnresponsiveReload",
                                                $"连续无响应 {MaxUnresponsiveCountBeforeReload} 次后尝试 Reload 失败",
                                                reloadEx);
                                        }
                                        finally
                                        {
                                            // ⭐ 任务 3.5：恢复结束后重置 guard
                                            _isRecoveringWebView = false;
                                            System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ 恢复标志已重置（RenderProcessUnresponsive）");
                                        }
                                    }
                                    else
                                    {
                                        // WebView 不可用，重置标志
                                        _isRecoveringWebView = false;
                                    }
                                }
                                break;
                            
                            case CoreWebView2ProcessFailedKind.FrameRenderProcessExited:
                                // Frame 渲染进程退出：仅记录日志，通常不需要恢复
                                System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] 检测到 FrameRenderProcessExited，仅记录日志");
                                break;
                            
                            case CoreWebView2ProcessFailedKind.UtilityProcessExited:
                            case CoreWebView2ProcessFailedKind.SandboxHelperProcessExited:
                            case CoreWebView2ProcessFailedKind.GpuProcessExited:
                                // 辅助进程退出：通常无需恢复，仅记录诊断信息
                                System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] 检测到 {processFailedKind}，仅记录诊断信息");
                                break;
                            
                            default:
                                // 未知类型：记录日志
                                System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] 检测到未知的 ProcessFailedKind: {processFailedKind}");
                                break;
                        }
                    }
                    catch (Exception recoveryEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] ⚠️ 恢复策略执行失败: {recoveryEx.Message}");
                        Features.UnifiedCalls.Logging.LogService.Error(
                            "WebView2.ProcessFailed.Recovery",
                            "恢复策略执行过程中发生异常",
                            recoveryEx);
                        
                        // ⭐ 任务 3.5：异常情况下也要重置 guard
                        _isRecoveringWebView = false;
                        System.Diagnostics.Debug.WriteLine("[CoreWebView2_ProcessFailed] ✅ 恢复标志已重置（异常恢复）");
                    }
                });
            }
            catch (Exception ex)
            {
                // ✅ 捕获处理器内部异常，避免二次崩溃（需求：2.3.2）
                System.Diagnostics.Debug.WriteLine($"[CoreWebView2_ProcessFailed] ⚠️ 处理器内部异常: {ex.Message}");
                
                // 记录处理器自身的异常
                try
                {
                    Features.UnifiedCalls.Logging.LogService.Error(
                        "WebView2.ProcessFailed",
                        "ProcessFailed 处理器内部发生异常（已捕获，避免二次崩溃）",
                        ex);
                }
                catch
                {
                    // 如果日志服务本身失败，也不抛出异常
                }
            }
        }
    }
}
