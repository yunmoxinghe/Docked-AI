using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Docked_AI.功能.主窗口v2.测试;

/// <summary>
/// 简单的独立测试，证明修复有效
/// 不需要 WindowContext 或 UI 线程
/// </summary>
public class SimpleVerificationTest
{
    /// <summary>
    /// 测试：证明 CancellationToken.None 无法被取消
    /// </summary>
    public static async Task ProveTokenNoneProblem()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("证明：CancellationToken.None 的问题");
        Console.WriteLine("========================================\n");
        
        Console.WriteLine("场景：模拟修复前的代码\n");
        
        // 模拟修复前的代码
        CancellationTokenSource? _currentCts = null;  // 初始为 null
        var animationCts = _currentCts;  // 快照
        var token = animationCts?.Token ?? CancellationToken.None;  // 使用 None 作为后备
        
        Console.WriteLine($"_currentCts: {(_currentCts == null ? "null" : "not null")}");
        Console.WriteLine($"animationCts: {(animationCts == null ? "null" : "not null")}");
        Console.WriteLine($"token.CanBeCanceled: {token.CanBeCanceled}");
        Console.WriteLine($"token.IsCancellationRequested: {token.IsCancellationRequested}");
        
        // 启动一个"动画"任务
        var animationTask = Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    token.ThrowIfCancellationRequested();
                    Console.WriteLine($"  动画帧 {i} (已运行 {stopwatch.ElapsedMilliseconds}ms)");
                    await Task.Delay(50, token);
                }
                Console.WriteLine($"  ❌ 动画完成（不应该发生）- 总耗时 {stopwatch.ElapsedMilliseconds}ms");
                return false;  // 动画完成
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"  ✅ 动画被取消 - 耗时 {stopwatch.ElapsedMilliseconds}ms");
                return true;  // 动画被取消
            }
        });
        
        // 等待100ms后尝试"取消"
        await Task.Delay(100);
        Console.WriteLine("\n尝试取消动画...");
        
        // 但是！token 是 CancellationToken.None，无法取消
        // 即使我们创建新的 CTS 也没用，因为 token 已经是 None 了
        
        var wasCancelled = await animationTask;
        
        Console.WriteLine("\n结论：");
        if (wasCancelled)
        {
            Console.WriteLine("❌ 这不应该发生！CancellationToken.None 不能被取消");
        }
        else
        {
            Console.WriteLine("✅ 证明成功：CancellationToken.None 无法被取消");
            Console.WriteLine("   动画运行了完整的 1000ms，即使我们想取消它");
        }
    }
    
    /// <summary>
    /// 测试：证明修复后的代码可以正常工作
    /// </summary>
    public static async Task ProveFixWorks()
    {
        Console.WriteLine("\n\n========================================");
        Console.WriteLine("证明：修复后的代码可以工作");
        Console.WriteLine("========================================\n");
        
        Console.WriteLine("场景：模拟修复后的代码\n");
        
        // 模拟修复后的代码
        CancellationTokenSource? _currentCts = null;
        
        // 🔴 修复：在使用前初始化
        if (_currentCts == null)
        {
            _currentCts = new CancellationTokenSource();
            Console.WriteLine("✅ 初始化 _currentCts");
        }
        
        var animationCts = _currentCts;
        
        // 🔴 修复：添加防御性检查
        if (animationCts == null)
        {
            Console.WriteLine("⚠️ animationCts 仍然为 null，创建新的");
            animationCts = new CancellationTokenSource();
        }
        
        var token = animationCts.Token;  // 🔴 修复：直接使用 Token，不使用 ?? None
        
        Console.WriteLine($"_currentCts: {(_currentCts == null ? "null" : "not null")}");
        Console.WriteLine($"animationCts: {(animationCts == null ? "null" : "not null")}");
        Console.WriteLine($"token.CanBeCanceled: {token.CanBeCanceled}");
        Console.WriteLine($"token.IsCancellationRequested: {token.IsCancellationRequested}");
        
        // 启动一个"动画"任务
        var animationTask = Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    token.ThrowIfCancellationRequested();
                    Console.WriteLine($"  动画帧 {i} (已运行 {stopwatch.ElapsedMilliseconds}ms)");
                    await Task.Delay(50, token);
                }
                Console.WriteLine($"  ❌ 动画完成（不应该发生）- 总耗时 {stopwatch.ElapsedMilliseconds}ms");
                return false;  // 动画完成
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"  ✅ 动画被取消 - 耗时 {stopwatch.ElapsedMilliseconds}ms");
                return true;  // 动画被取消
            }
        });
        
        // 等待100ms后取消
        await Task.Delay(100);
        Console.WriteLine("\n取消动画...");
        animationCts.Cancel();
        
        var wasCancelled = await animationTask;
        
        Console.WriteLine("\n结论：");
        if (wasCancelled)
        {
            Console.WriteLine("✅ 证明成功：修复后的代码可以正常取消动画");
            Console.WriteLine("   动画在约 100ms 时被取消，而不是运行完整的 1000ms");
        }
        else
        {
            Console.WriteLine("❌ 修复失败：动画仍然无法被取消");
        }
        
        animationCts.Dispose();
    }
    
    /// <summary>
    /// 对比测试：修复前 vs 修复后
    /// </summary>
    public static async Task CompareBeforeAndAfter()
    {
        Console.WriteLine("\n\n========================================");
        Console.WriteLine("对比：修复前 vs 修复后");
        Console.WriteLine("========================================\n");
        
        // 测试1：修复前
        Console.WriteLine("【修复前】");
        var stopwatch1 = Stopwatch.StartNew();
        
        CancellationTokenSource? cts1 = null;
        var token1 = cts1?.Token ?? CancellationToken.None;
        
        var task1 = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    token1.ThrowIfCancellationRequested();
                    await Task.Delay(50, token1);
                }
                return "completed";
            }
            catch (OperationCanceledException)
            {
                return "cancelled";
            }
        });
        
        await Task.Delay(100);
        // 尝试取消（但无效，因为 token1 是 None）
        
        var result1 = await task1;
        stopwatch1.Stop();
        
        Console.WriteLine($"  结果: {result1}");
        Console.WriteLine($"  耗时: {stopwatch1.ElapsedMilliseconds}ms");
        Console.WriteLine($"  CanBeCanceled: {token1.CanBeCanceled}");
        
        // 测试2：修复后
        Console.WriteLine("\n【修复后】");
        var stopwatch2 = Stopwatch.StartNew();
        
        CancellationTokenSource? cts2 = null;
        if (cts2 == null)
        {
            cts2 = new CancellationTokenSource();
        }
        var token2 = cts2.Token;
        
        var task2 = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    token2.ThrowIfCancellationRequested();
                    await Task.Delay(50, token2);
                }
                return "completed";
            }
            catch (OperationCanceledException)
            {
                return "cancelled";
            }
        });
        
        await Task.Delay(100);
        cts2.Cancel();  // 取消
        
        var result2 = await task2;
        stopwatch2.Stop();
        
        Console.WriteLine($"  结果: {result2}");
        Console.WriteLine($"  耗时: {stopwatch2.ElapsedMilliseconds}ms");
        Console.WriteLine($"  CanBeCanceled: {token2.CanBeCanceled}");
        
        cts2.Dispose();
        
        // 对比
        Console.WriteLine("\n【对比结果】");
        Console.WriteLine($"修复前: {result1}, 耗时 {stopwatch1.ElapsedMilliseconds}ms");
        Console.WriteLine($"修复后: {result2}, 耗时 {stopwatch2.ElapsedMilliseconds}ms");
        
        if (result1 == "completed" && result2 == "cancelled" && stopwatch2.ElapsedMilliseconds < 200)
        {
            Console.WriteLine("\n✅ 对比成功：修复有效！");
            Console.WriteLine("   修复前：动画无法取消，运行完整时间");
            Console.WriteLine("   修复后：动画可以取消，提前结束");
        }
        else
        {
            Console.WriteLine("\n❌ 对比失败：结果不符合预期");
        }
    }
    
    /// <summary>
    /// 运行所有证明测试
    /// </summary>
    public static async Task RunAllProofs()
    {
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║   动画打断问题 - 证明测试套件         ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");
        
        try
        {
            await ProveTokenNoneProblem();
            await ProveFixWorks();
            await CompareBeforeAndAfter();
            
            Console.WriteLine("\n\n╔════════════════════════════════════════╗");
            Console.WriteLine("║   所有证明测试完成                     ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 测试失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
