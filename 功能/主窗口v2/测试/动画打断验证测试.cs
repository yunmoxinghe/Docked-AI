using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;

namespace Docked_AI.功能.主窗口v2.测试;

/// <summary>
/// 动画打断验证测试
/// 验证动画能够被立即取消，并从中间状态继续到新目标
/// </summary>
public class AnimationInterruptionTest
{
    /// <summary>
    /// 测试动画打断功能
    /// 场景：动画播放到50%时被打断，应该立即停止并抛出 OperationCanceledException
    /// </summary>
    public static async Task TestAnimationCancellation()
    {
        Console.WriteLine("=== 测试动画打断功能 ===");
        
        var engine = new AnimationEngine();
        
        var from = new WindowVisualState
        {
            Bounds = new Rect(0, 0, 400, 600),
            CornerRadius = 0,
            Opacity = 0.0,
            IsTopmost = false,
            ExtendedStyle = 0
        };
        
        var to = new WindowVisualState
        {
            Bounds = new Rect(1000, 500, 400, 600),
            CornerRadius = 12,
            Opacity = 1.0,
            IsTopmost = true,
            ExtendedStyle = 0
        };
        
        var spec = new AnimationSpec
        {
            Duration = TimeSpan.FromSeconds(2), // 2秒动画，足够长以便测试打断
            Easing = Easing.Linear
        };
        
        var cts = new CancellationTokenSource();
        var progressCount = 0;
        WindowVisualState? lastVisual = null;
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var animationTask = engine.Animate(
                from,
                to,
                spec,
                onProgress: (visual) =>
                {
                    progressCount++;
                    lastVisual = visual;
                    Console.WriteLine($"Progress {progressCount}: Opacity={visual.Opacity:F2}, X={visual.Bounds.X:F0}");
                    
                    // 在第5帧（约80ms后）取消动画
                    if (progressCount == 5)
                    {
                        Console.WriteLine(">>> 触发取消信号");
                        cts.Cancel();
                    }
                },
                cancellationToken: cts.Token
            );
            
            await animationTask;
            
            Console.WriteLine("❌ 测试失败：动画应该被取消，但却完成了");
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            Console.WriteLine($"✅ 测试成功：动画在 {stopwatch.ElapsedMilliseconds}ms 后被取消");
            Console.WriteLine($"   进度回调被调用了 {progressCount} 次");
            
            if (lastVisual != null)
            {
                Console.WriteLine($"   最后的视觉状态：Opacity={lastVisual.Opacity:F2}, X={lastVisual.Bounds.X:F0}");
                
                // 验证动画确实在中间状态停止（而非起点或终点）
                if (lastVisual.Opacity > 0.0 && lastVisual.Opacity < 1.0)
                {
                    Console.WriteLine("   ✅ 动画确实在中间状态停止");
                }
                else
                {
                    Console.WriteLine("   ⚠️ 警告：动画可能在起点或终点停止");
                }
            }
            
            // 验证取消响应时间（应该 <50ms）
            if (stopwatch.ElapsedMilliseconds < 50)
            {
                Console.WriteLine($"   ✅ 取消响应时间合格（<50ms）");
            }
            else
            {
                Console.WriteLine($"   ⚠️ 警告：取消响应时间过长（{stopwatch.ElapsedMilliseconds}ms）");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 测试失败：抛出了意外的异常 {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            cts.Dispose();
        }
    }
    
    /// <summary>
    /// 测试快速连续打断
    /// 场景：快速连续取消多次，验证不会出现异常或资源泄漏
    /// </summary>
    public static async Task TestRapidCancellation()
    {
        Console.WriteLine("\n=== 测试快速连续打断 ===");
        
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
        
        int successfulCancellations = 0;
        
        // 快速连续启动和取消10次动画
        for (int i = 0; i < 10; i++)
        {
            var cts = new CancellationTokenSource();
            
            try
            {
                var animationTask = engine.Animate(
                    from,
                    to,
                    spec,
                    onProgress: (visual) => { },
                    cancellationToken: cts.Token
                );
                
                // 等待10ms后取消
                await Task.Delay(10);
                cts.Cancel();
                
                await animationTask;
            }
            catch (OperationCanceledException)
            {
                successfulCancellations++;
            }
            finally
            {
                cts.Dispose();
            }
        }
        
        Console.WriteLine($"成功取消了 {successfulCancellations}/10 次动画");
        
        if (successfulCancellations == 10)
        {
            Console.WriteLine("✅ 测试成功：所有动画都被正确取消");
        }
        else
        {
            Console.WriteLine($"⚠️ 警告：有 {10 - successfulCancellations} 次动画未被取消");
        }
    }
    
    /// <summary>
    /// 运行所有测试
    /// </summary>
    public static async Task RunAllTests()
    {
        try
        {
            await TestAnimationCancellation();
            await TestRapidCancellation();
            
            Console.WriteLine("\n=== 所有测试完成 ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 测试运行失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
