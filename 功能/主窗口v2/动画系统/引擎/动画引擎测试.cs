using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;

namespace Docked_AI.功能.主窗口v2.动画系统.引擎;

/// <summary>
/// AnimationEngine 手动测试工具
/// 用于验证基于缓动的动画功能
/// </summary>
public class AnimationEngineManualTest
{
    private readonly AnimationEngine _engine;

    public AnimationEngineManualTest()
    {
        _engine = new AnimationEngine();
    }

    /// <summary>
    /// 测试 AnimateWithEasing 方法
    /// 验证：
    /// 1. 支持 linear、EaseIn、EaseOut、EaseInOut 缓动函数
    /// 2. 基于经过时间（非帧数）计算进度
    /// 3. 使用插值后的视觉状态调用 onProgress 回调
    /// </summary>
    public async Task TestAnimateWithEasing()
    {
        Console.WriteLine("=== 测试 AnimateWithEasing ===\n");

        // 定义起始和目标视觉状态
        var from = new WindowVisualState
        {
            Bounds = new Rect(100, 100, 400, 600),
            CornerRadius = 0,
            Opacity = 0.0,
            IsTopmost = false,
            ExtendedStyle = 0
        };

        var to = new WindowVisualState
        {
            Bounds = new Rect(500, 200, 800, 800),
            CornerRadius = 12,
            Opacity = 1.0,
            IsTopmost = true,
            ExtendedStyle = 1
        };

        // 测试不同的缓动函数
        var easingTypes = new[] 
        { 
            Easing.Linear, 
            Easing.EaseIn, 
            Easing.EaseOut, 
            Easing.EaseInOut 
        };

        foreach (var easingType in easingTypes)
        {
            Console.WriteLine($"测试缓动函数: {easingType}");
            
            var spec = new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(500),
                Easing = easingType
            };

            var progressCallCount = 0;
            var stopwatch = Stopwatch.StartNew();
            var firstProgress = 0.0;
            var lastProgress = 0.0;

            try
            {
                await _engine.Animate(
                    from,
                    to,
                    spec,
                    (current) =>
                    {
                        progressCallCount++;
                        
                        // 记录第一次和最后一次的进度
                        if (progressCallCount == 1)
                        {
                            firstProgress = current.Opacity;
                        }
                        lastProgress = current.Opacity;

                        // 验证插值结果在起始和目标之间
                        if (current.Opacity < from.Opacity || current.Opacity > to.Opacity)
                        {
                            Console.WriteLine($"  ❌ 错误: Opacity {current.Opacity} 超出范围 [{from.Opacity}, {to.Opacity}]");
                        }

                        if (current.Bounds.Width < from.Bounds.Width || current.Bounds.Width > to.Bounds.Width)
                        {
                            Console.WriteLine($"  ❌ 错误: Width {current.Bounds.Width} 超出范围 [{from.Bounds.Width}, {to.Bounds.Width}]");
                        }
                    },
                    CancellationToken.None
                );

                stopwatch.Stop();
                var actualDuration = stopwatch.ElapsedMilliseconds;
                var durationError = Math.Abs(actualDuration - spec.Duration.TotalMilliseconds) / spec.Duration.TotalMilliseconds;

                Console.WriteLine($"  ✓ 动画完成");
                Console.WriteLine($"  - 回调次数: {progressCallCount}");
                Console.WriteLine($"  - 预期时长: {spec.Duration.TotalMilliseconds}ms");
                Console.WriteLine($"  - 实际时长: {actualDuration}ms");
                Console.WriteLine($"  - 时长误差: {durationError * 100:F2}%");
                Console.WriteLine($"  - 首次进度: Opacity={firstProgress:F3}");
                Console.WriteLine($"  - 最终进度: Opacity={lastProgress:F3}");

                // 验证时长精度（误差应小于 5%）
                if (durationError > 0.05)
                {
                    Console.WriteLine($"  ⚠️ 警告: 时长误差超过 5%");
                }

                // 验证回调次数（至少 2 次）
                if (progressCallCount < 2)
                {
                    Console.WriteLine($"  ❌ 错误: 回调次数少于 2 次");
                }

                // 验证最终状态接近目标
                if (Math.Abs(lastProgress - to.Opacity) > 0.01)
                {
                    Console.WriteLine($"  ⚠️ 警告: 最终 Opacity {lastProgress} 与目标 {to.Opacity} 差距较大");
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 异常: {ex.Message}");
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// 测试动画取消功能
    /// 验证：动画可以通过 CancellationToken 立即停止
    /// </summary>
    public async Task TestAnimationCancellation()
    {
        Console.WriteLine("=== 测试动画取消 ===\n");

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
            Bounds = new Rect(1000, 1000, 800, 800),
            CornerRadius = 12,
            Opacity = 1.0,
            IsTopmost = true,
            ExtendedStyle = 1
        };

        var spec = new AnimationSpec
        {
            Duration = TimeSpan.FromSeconds(2), // 长时间动画
            Easing = Easing.Linear
        };

        var cts = new CancellationTokenSource();
        var progressCallCount = 0;
        var lastOpacity = 0.0;

        // 100ms 后取消动画
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            cts.Cancel();
        });

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _engine.Animate(
                from,
                to,
                spec,
                (current) =>
                {
                    progressCallCount++;
                    lastOpacity = current.Opacity;
                },
                cts.Token
            );

            Console.WriteLine("  ❌ 错误: 动画未被取消");
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            Console.WriteLine("  ✓ 动画成功取消");
            Console.WriteLine($"  - 取消前回调次数: {progressCallCount}");
            Console.WriteLine($"  - 取消时 Opacity: {lastOpacity:F3}");
            Console.WriteLine($"  - 取消时间: {stopwatch.ElapsedMilliseconds}ms");

            // 验证取消响应时间（应小于 50ms）
            if (stopwatch.ElapsedMilliseconds > 150)
            {
                Console.WriteLine($"  ⚠️ 警告: 取消响应时间超过 50ms");
            }

            // 验证动画在中间状态停止
            if (lastOpacity <= 0.0 || lastOpacity >= 1.0)
            {
                Console.WriteLine($"  ⚠️ 警告: 动画未在中间状态停止");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// 运行所有测试
    /// </summary>
    public async Task RunAllTests()
    {
        Console.WriteLine("开始测试 AnimationEngine\n");
        Console.WriteLine("========================================\n");

        await TestAnimateWithEasing();
        await TestAnimationCancellation();

        Console.WriteLine("========================================");
        Console.WriteLine("测试完成");
    }
}
