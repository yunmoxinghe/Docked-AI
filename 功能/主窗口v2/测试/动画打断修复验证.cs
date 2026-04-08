using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;

namespace Docked_AI.功能.主窗口v2.测试;

/// <summary>
/// 验证动画打断修复的测试
/// 测试取消令牌在各个关键点的响应速度
/// </summary>
public class AnimationInterruptionFixVerification
{
    /// <summary>
    /// 测试1：验证在onProgress回调执行期间取消能立即响应
    /// </summary>
    public static async Task TestCancellationDuringCallback()
    {
        Console.WriteLine("=== 测试1：回调期间取消 ===");
        
        var engine = new AnimationEngine();
        var from = new WindowVisualState
        {
            Bounds = new Rect(0, 0, 400, 600),
            Opacity = 0.0
        };
        
        var to = new WindowVisualState
        {
            Bounds = new Rect(1000, 500, 400, 600),
            Opacity = 1.0
        };
        
        var spec = new AnimationSpec
        {
            Duration = TimeSpan.FromSeconds(2),
            Easing = Easing.Linear
        };
        
        var cts = new CancellationTokenSource();
        var callbackCount = 0;
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await engine.Animate(
                from,
                to,
                spec,
                onProgress: (visual) =>
                {
                    callbackCount++;
                    
                    // 在第3次回调时取消（约48ms后）
                    if (callbackCount == 3)
                    {
                        Console.WriteLine($"  第{callbackCount}次回调：触发取消");
                        cts.Cancel();
                    }
                    
                    // 模拟回调中的一些处理时间
                    Thread.Sleep(5);
                },
                cancellationToken: cts.Token
            );
            
            Console.WriteLine("  ❌ 失败：动画应该被取消");
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"  ✅ 成功：动画在 {elapsed}ms 后被取消");
            Console.WriteLine($"  回调被调用了 {callbackCount} 次");
            
            // 验证响应时间（应该 <100ms，因为有3次回调 + 每次5ms延迟）
            if (elapsed < 100)
            {
                Console.WriteLine($"  ✅ 响应时间合格（<100ms）");
            }
            else
            {
                Console.WriteLine($"  ⚠️ 警告：响应时间过长（{elapsed}ms）");
            }
        }
        finally
        {
            cts.Dispose();
        }
    }
    
    /// <summary>
    /// 测试2：验证在计算期间取消能立即响应
    /// </summary>
    public static async Task TestCancellationDuringComputation()
    {
        Console.WriteLine("\n=== 测试2：计算期间取消 ===");
        
        var engine = new AnimationEngine();
        var from = new WindowVisualState
        {
            Bounds = new Rect(0, 0, 400, 600),
            Opacity = 0.0
        };
        
        var to = new WindowVisualState
        {
            Bounds = new Rect(1000, 500, 400, 600),
            Opacity = 1.0
        };
        
        var spec = new AnimationSpec
        {
            Duration = TimeSpan.FromSeconds(2),
            Easing = Easing.Linear
        };
        
        var cts = new CancellationTokenSource();
        var callbackCount = 0;
        
        // 在50ms后取消（应该在第3-4次回调之间）
        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            Console.WriteLine("  触发取消信号");
            cts.Cancel();
        });
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await engine.Animate(
                from,
                to,
                spec,
                onProgress: (visual) =>
                {
                    callbackCount++;
                },
                cancellationToken: cts.Token
            );
            
            Console.WriteLine("  ❌ 失败：动画应该被取消");
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"  ✅ 成功：动画在 {elapsed}ms 后被取消");
            Console.WriteLine($"  回调被调用了 {callbackCount} 次");
            
            // 验证响应时间（应该在50-70ms之间，允许一帧的延迟）
            if (elapsed >= 50 && elapsed < 70)
            {
                Console.WriteLine($"  ✅ 响应时间合格（50-70ms）");
            }
            else
            {
                Console.WriteLine($"  ⚠️ 警告：响应时间异常（{elapsed}ms，预期50-70ms）");
            }
        }
        finally
        {
            cts.Dispose();
        }
    }
    
    /// <summary>
    /// 测试3：验证回调抛出异常时的处理
    /// </summary>
    public static async Task TestCallbackException()
    {
        Console.WriteLine("\n=== 测试3：回调异常处理 ===");
        
        var engine = new AnimationEngine();
        var from = new WindowVisualState
        {
            Bounds = new Rect(0, 0, 400, 600),
            Opacity = 0.0
        };
        
        var to = new WindowVisualState
        {
            Bounds = new Rect(1000, 500, 400, 600),
            Opacity = 1.0
        };
        
        var spec = new AnimationSpec
        {
            Duration = TimeSpan.FromSeconds(1),
            Easing = Easing.Linear
        };
        
        var cts = new CancellationTokenSource();
        var callbackCount = 0;
        
        try
        {
            await engine.Animate(
                from,
                to,
                spec,
                onProgress: (visual) =>
                {
                    callbackCount++;
                    
                    // 在第5次回调时抛出异常
                    if (callbackCount == 5)
                    {
                        Console.WriteLine($"  第{callbackCount}次回调：抛出异常");
                        throw new InvalidOperationException("模拟回调失败");
                    }
                },
                cancellationToken: cts.Token
            );
            
            Console.WriteLine("  ❌ 失败：应该抛出异常");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  ✅ 成功：捕获到异常 - {ex.Message}");
            Console.WriteLine($"  回调被调用了 {callbackCount} 次");
            
            // 验证异常消息包含预期内容
            if (ex.Message.Contains("Animation progress callback failed"))
            {
                Console.WriteLine($"  ✅ 异常消息正确");
            }
            else
            {
                Console.WriteLine($"  ⚠️ 警告：异常消息不符合预期");
            }
            
            // 验证内部异常被保留
            if (ex.InnerException != null && ex.InnerException.Message.Contains("模拟回调失败"))
            {
                Console.WriteLine($"  ✅ 内部异常被正确保留");
            }
            else
            {
                Console.WriteLine($"  ⚠️ 警告：内部异常未被保留");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 失败：抛出了意外的异常类型 - {ex.GetType().Name}");
        }
        finally
        {
            cts.Dispose();
        }
    }
    
    /// <summary>
    /// 测试4：验证Spring动画的取消响应
    /// </summary>
    public static async Task TestSpringAnimationCancellation()
    {
        Console.WriteLine("\n=== 测试4：Spring动画取消 ===");
        
        var engine = new AnimationEngine();
        var from = new WindowVisualState
        {
            Bounds = new Rect(0, 0, 400, 600),
            Opacity = 0.0
        };
        
        var to = new WindowVisualState
        {
            Bounds = new Rect(1000, 500, 400, 600),
            Opacity = 1.0
        };
        
        var spec = new AnimationSpec
        {
            Duration = TimeSpan.FromSeconds(2), // Spring动画会忽略Duration
            Spring = new SpringConfig
            {
                Stiffness = 200,
                Damping = 20
            }
        };
        
        var cts = new CancellationTokenSource();
        var callbackCount = 0;
        
        // 在100ms后取消
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            Console.WriteLine("  触发取消信号");
            cts.Cancel();
        });
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await engine.Animate(
                from,
                to,
                spec,
                onProgress: (visual) =>
                {
                    callbackCount++;
                },
                cancellationToken: cts.Token
            );
            
            Console.WriteLine("  ❌ 失败：动画应该被取消");
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"  ✅ 成功：Spring动画在 {elapsed}ms 后被取消");
            Console.WriteLine($"  回调被调用了 {callbackCount} 次");
            
            // 验证响应时间（应该在100-120ms之间）
            if (elapsed >= 100 && elapsed < 120)
            {
                Console.WriteLine($"  ✅ 响应时间合格（100-120ms）");
            }
            else
            {
                Console.WriteLine($"  ⚠️ 警告：响应时间异常（{elapsed}ms，预期100-120ms）");
            }
        }
        finally
        {
            cts.Dispose();
        }
    }
    
    /// <summary>
    /// 运行所有测试
    /// </summary>
    public static async Task RunAllTests()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("动画打断修复验证测试");
        Console.WriteLine("========================================\n");
        
        try
        {
            await TestCancellationDuringCallback();
            await TestCancellationDuringComputation();
            await TestCallbackException();
            await TestSpringAnimationCancellation();
            
            Console.WriteLine("\n========================================");
            Console.WriteLine("所有测试完成");
            Console.WriteLine("========================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 测试运行失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
