using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Docked_AI.功能.主窗口v2.测试;

/// <summary>
/// 调试取消令牌的传递和响应
/// </summary>
public class CancellationTokenDebugTest
{
    /// <summary>
    /// 测试CancellationToken.None是否能被取消
    /// </summary>
    public static async Task TestCancellationTokenNone()
    {
        Console.WriteLine("=== 测试 CancellationToken.None ===");
        
        var token = CancellationToken.None;
        
        Console.WriteLine($"CanBeCanceled: {token.CanBeCanceled}");
        Console.WriteLine($"IsCancellationRequested: {token.IsCancellationRequested}");
        
        try
        {
            await Task.Delay(100, token);
            Console.WriteLine("✅ Task.Delay 完成（预期行为）");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("❌ Task.Delay 被取消（不应该发生）");
        }
        
        Console.WriteLine("\n结论：CancellationToken.None 永远不会被取消！");
    }
    
    /// <summary>
    /// 测试null CTS的Token
    /// </summary>
    public static async Task TestNullCtsToken()
    {
        Console.WriteLine("\n=== 测试 null CTS 的 Token ===");
        
        CancellationTokenSource? cts = null;
        var token = cts?.Token ?? CancellationToken.None;
        
        Console.WriteLine($"CanBeCanceled: {token.CanBeCanceled}");
        Console.WriteLine($"IsCancellationRequested: {token.IsCancellationRequested}");
        
        Console.WriteLine("\n结论：null CTS 会导致使用 CancellationToken.None！");
    }
    
    /// <summary>
    /// 测试正确的CTS使用
    /// </summary>
    public static async Task TestCorrectCtsUsage()
    {
        Console.WriteLine("\n=== 测试正确的 CTS 使用 ===");
        
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        
        Console.WriteLine($"CanBeCanceled: {token.CanBeCanceled}");
        Console.WriteLine($"IsCancellationRequested: {token.IsCancellationRequested}");
        
        // 启动一个任务
        var task = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    token.ThrowIfCancellationRequested();
                    Console.WriteLine($"  迭代 {i}");
                    await Task.Delay(50, token);
                }
                Console.WriteLine("  ❌ 任务完成（不应该发生）");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("  ✅ 任务被取消");
            }
        });
        
        // 等待150ms后取消
        await Task.Delay(150);
        Console.WriteLine("触发取消...");
        cts.Cancel();
        
        await task;
        
        cts.Dispose();
        
        Console.WriteLine("\n结论：正确的 CTS 可以被取消！");
    }
    
    /// <summary>
    /// 模拟ExecuteTransitionAsync的问题
    /// </summary>
    public static async Task TestExecuteTransitionAsyncProblem()
    {
        Console.WriteLine("\n=== 模拟 ExecuteTransitionAsync 的问题 ===");
        
        CancellationTokenSource? _currentCts = null; // 模拟初始状态
        
        Console.WriteLine("场景1：第一次转换（_currentCts 为 null）");
        var animationCts = _currentCts; // 快照
        var token = animationCts?.Token ?? CancellationToken.None;
        
        Console.WriteLine($"  animationCts: {(animationCts == null ? "null" : "not null")}");
        Console.WriteLine($"  token.CanBeCanceled: {token.CanBeCanceled}");
        Console.WriteLine($"  ❌ 问题：使用了 CancellationToken.None，无法取消！");
        
        Console.WriteLine("\n场景2：第二次转换（_currentCts 已创建）");
        _currentCts = new CancellationTokenSource();
        animationCts = _currentCts; // 快照
        token = animationCts?.Token ?? CancellationToken.None;
        
        Console.WriteLine($"  animationCts: {(animationCts == null ? "null" : "not null")}");
        Console.WriteLine($"  token.CanBeCanceled: {token.CanBeCanceled}");
        Console.WriteLine($"  ✅ 正常：可以被取消");
        
        _currentCts.Dispose();
    }
    
    /// <summary>
    /// 运行所有测试
    /// </summary>
    public static async Task RunAllTests()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("取消令牌调试测试");
        Console.WriteLine("========================================\n");
        
        await TestCancellationTokenNone();
        await TestNullCtsToken();
        await TestCorrectCtsUsage();
        await TestExecuteTransitionAsyncProblem();
        
        Console.WriteLine("\n========================================");
        Console.WriteLine("测试完成");
        Console.WriteLine("========================================");
    }
}
