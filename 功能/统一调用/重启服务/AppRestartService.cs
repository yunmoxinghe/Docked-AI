using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Xaml;

namespace Docked_AI.功能.统一调用;

/// <summary>
/// 应用重启服务
/// WinUI 3 无包应用的正确重启实现
/// </summary>
public static class AppRestartService
{
    /// <summary>
    /// 重启应用（基础版）
    /// </summary>
    public static void Restart()
    {
        RestartWithArgs("--restart");
    }

    /// <summary>
    /// 带参数重启应用
    /// </summary>
    /// <param name="args">启动参数，例如 "--restart-from=update"</param>
    public static void RestartWithArgs(params string[] args)
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            
            if (string.IsNullOrEmpty(exePath))
            {
                throw new InvalidOperationException("无法获取当前程序路径");
            }

            // 确保包含 --restart 标记（用于绕过单实例检测）
            var argsList = args.ToList();
            if (!argsList.Any(a => a.Contains("--restart")))
            {
                argsList.Insert(0, "--restart");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Arguments = string.Join(" ", argsList)
            };

            Process.Start(startInfo);
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"重启失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 以管理员权限重启
    /// </summary>
    public static void RestartAsAdmin()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            
            if (string.IsNullOrEmpty(exePath))
            {
                throw new InvalidOperationException("无法获取当前程序路径");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas", // 请求管理员权限
                Arguments = "--restart" // 确保包含重启标记
            };

            Process.Start(startInfo);
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"以管理员权限重启失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 延迟重启（给应用时间保存状态）
    /// </summary>
    /// <param name="delayMilliseconds">延迟毫秒数</param>
    /// <param name="onBeforeRestart">重启前的回调（用于保存状态）</param>
    public static async void RestartWithDelay(int delayMilliseconds = 500, Action? onBeforeRestart = null)
    {
        try
        {
            // 执行重启前的操作
            onBeforeRestart?.Invoke();

            // 延迟
            await System.Threading.Tasks.Task.Delay(delayMilliseconds);

            Restart();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"延迟重启失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 检查是否从重启启动
    /// </summary>
    /// <returns>如果是重启启动返回 true</returns>
    public static bool IsRestartedLaunch()
    {
        var args = Environment.GetCommandLineArgs();
        return args.Any(arg => arg.Contains("--restart"));
    }

    /// <summary>
    /// 获取重启来源
    /// </summary>
    /// <returns>重启来源标识，如 "update", "crash", "settings" 等</returns>
    public static string? GetRestartSource()
    {
        var args = Environment.GetCommandLineArgs();
        var restartArg = args.FirstOrDefault(arg => arg.StartsWith("--restart-from="));
        
        if (restartArg != null)
        {
            return restartArg.Replace("--restart-from=", "");
        }

        return null;
    }
}
