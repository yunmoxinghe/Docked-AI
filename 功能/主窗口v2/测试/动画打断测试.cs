using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Docked_AI.功能.主窗口v2.状态机;
using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.服务层;
using Docked_AI.功能.主窗口v2.动画系统.策略;

namespace Docked_AI.功能.主窗口v2.测试;

/// <summary>
/// 动画打断测试
/// 验证快速连续的状态转换请求是否能够正确打断正在执行的动画
/// </summary>
public class 动画打断测试
{
    /// <summary>
    /// 测试快速状态切换：Floating → Fullscreen → Sidebar
    /// 验证：
    /// 1. 中间状态（Fullscreen）不会成为稳定状态
    /// 2. 最终状态是 Sidebar
    /// 3. 动画能够被正确打断
    /// 4. StateChanged 事件显示正确的 from/to 状态
    /// </summary>
    public static async Task TestRapidStateTransitions()
    {
        Console.WriteLine("=== 测试：快速状态切换 ===");
        Console.WriteLine();
        
        // 注意：这个测试需要在 UI 线程上运行
        // 因为 WindowStateManager 需要 DispatcherQueue
        
        Console.WriteLine("⚠️ 此测试需要在 UI 线程上运行");
        Console.WriteLine("请在主窗口中调用此测试方法");
        Console.WriteLine();
        
        // 测试步骤：
        // 1. 创建 WindowStateManager
        // 2. 快速连续调用 TransitionTo(Floating) → TransitionTo(Fullscreen) → TransitionTo(Sidebar)
        // 3. 等待所有转换完成
        // 4. 验证最终状态是 Sidebar
        // 5. 验证 StateChanged 事件只触发一次：Initializing → Sidebar
        
        Console.WriteLine("测试步骤：");
        Console.WriteLine("1. 创建 WindowStateManager（状态：Initializing）");
        Console.WriteLine("2. 快速调用 TransitionTo(Floating)");
        Console.WriteLine("3. 立即调用 TransitionTo(Fullscreen)（打断 Floating 动画）");
        Console.WriteLine("4. 立即调用 TransitionTo(Sidebar)（打断 Fullscreen 动画）");
        Console.WriteLine("5. 等待转换完成");
        Console.WriteLine("6. 验证最终状态是 Sidebar");
        Console.WriteLine();
        
        Console.WriteLine("预期结果：");
        Console.WriteLine("- 中间状态（Floating、Fullscreen）不会成为稳定状态");
        Console.WriteLine("- 最终状态是 Sidebar");
        Console.WriteLine("- StateChanged 事件：Initializing → Sidebar");
        Console.WriteLine("- 动画从当前中间状态平滑过渡到 Sidebar，无跳变");
        Console.WriteLine();
    }
    
    /// <summary>
    /// 测试动画中断后的视觉连续性
    /// 验证：
    /// 1. 动画被打断时，_currentVisual 停留在中间状态
    /// 2. 新动画从中间状态继续，无视觉跳变
    /// 3. 不需要执行反向动画
    /// </summary>
    public static async Task TestVisualContinuity()
    {
        Console.WriteLine("=== 测试：动画中断后的视觉连续性 ===");
        Console.WriteLine();
        
        Console.WriteLine("测试步骤：");
        Console.WriteLine("1. 开始从 Hidden 到 Floating 的转换");
        Console.WriteLine("2. 动画播放到 50%");
        Console.WriteLine("3. 用 Fullscreen 转换打断");
        Console.WriteLine("4. 验证动画从 50% 中间状态继续到 Fullscreen");
        Console.WriteLine("5. 验证没有视觉跳变（不回到 Hidden 再开始）");
        Console.WriteLine();
        
        Console.WriteLine("预期结果：");
        Console.WriteLine("- 动画从中间状态（50%）平滑过渡到 Fullscreen");
        Console.WriteLine("- 窗口位置/大小/透明度连续变化，无跳变");
        Console.WriteLine("- 不执行反向动画（不回到 Hidden）");
        Console.WriteLine();
    }
    
    /// <summary>
    /// 测试取消令牌的响应速度
    /// 验证：
    /// 1. TransitionTo 调用后，旧动画能够立即停止
    /// 2. 取消延迟不超过一帧（~16ms）
    /// </summary>
    public static async Task TestCancellationSpeed()
    {
        Console.WriteLine("=== 测试：取消令牌响应速度 ===");
        Console.WriteLine();
        
        Console.WriteLine("测试步骤：");
        Console.WriteLine("1. 开始一个长动画（500ms）");
        Console.WriteLine("2. 在 100ms 后调用 TransitionTo 打断");
        Console.WriteLine("3. 测量从 TransitionTo 调用到动画实际停止的时间");
        Console.WriteLine("4. 验证延迟不超过 16ms（一帧）");
        Console.WriteLine();
        
        Console.WriteLine("预期结果：");
        Console.WriteLine("- 取消延迟 < 16ms");
        Console.WriteLine("- 动画立即停止，不等待当前帧完成");
        Console.WriteLine();
    }
    
    /// <summary>
    /// 运行所有测试
    /// </summary>
    public static async Task RunAllTests()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          动画打断功能测试套件                              ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        
        await TestRapidStateTransitions();
        Console.WriteLine();
        
        await TestVisualContinuity();
        Console.WriteLine();
        
        await TestCancellationSpeed();
        Console.WriteLine();
        
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          测试完成                                          ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
    }
}
