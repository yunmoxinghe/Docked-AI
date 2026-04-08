using System;
using System.Threading.Tasks;

namespace Docked_AI.功能.主窗口v2.测试;

/// <summary>
/// 测试运行器 - 提供统一的测试入口
/// </summary>
public static class TestRunner
{
    /// <summary>
    /// 运行所有独立测试（不需要 UI 线程）
    /// 可以在任何地方调用
    /// </summary>
    public static async Task RunStandaloneTests()
    {
        Console.WriteLine("\n");
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                                                          ║");
        Console.WriteLine("║          动画打断修复 - 独立验证测试                    ║");
        Console.WriteLine("║                                                          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine("\n");
        
        try
        {
            // 运行简单验证测试
            await SimpleVerificationTest.RunAllProofs();
            
            Console.WriteLine("\n");
            Console.WriteLine("════════════════════════════════════════════════════════════");
            Console.WriteLine("  测试总结");
            Console.WriteLine("════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("  这些测试证明了：");
            Console.WriteLine("  1. ✅ CancellationToken.None 无法被取消（问题根源）");
            Console.WriteLine("  2. ✅ 修复后的代码可以正常取消动画");
            Console.WriteLine("  3. ✅ 修复前后的行为差异明显");
            Console.WriteLine();
            Console.WriteLine("  下一步：");
            Console.WriteLine("  - 在实际应用中测试窗口状态转换");
            Console.WriteLine("  - 验证快速切换状态时的视觉效果");
            Console.WriteLine("  - 确认没有内存泄漏或资源问题");
            Console.WriteLine();
            Console.WriteLine("════════════════════════════════════════════════════════════");
            Console.WriteLine("\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n");
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  ❌ 测试运行失败                                         ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"错误: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("堆栈跟踪:");
            Console.WriteLine(ex.StackTrace);
            Console.WriteLine();
        }
    }
    
    /// <summary>
    /// 运行动画引擎测试
    /// </summary>
    public static async Task RunAnimationEngineTests()
    {
        Console.WriteLine("\n");
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          动画引擎测试                                    ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine("\n");
        
        try
        {
            await AnimationInterruptionFixVerification.RunAllTests();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 测试失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
    
    /// <summary>
    /// 运行取消令牌调试测试
    /// </summary>
    public static async Task RunCancellationTokenDebugTests()
    {
        Console.WriteLine("\n");
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          取消令牌调试测试                                ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine("\n");
        
        try
        {
            await CancellationTokenDebugTest.RunAllTests();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 测试失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
    
    /// <summary>
    /// 运行所有测试
    /// </summary>
    public static async Task RunAllTests()
    {
        Console.WriteLine("\n\n");
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                                                          ║");
        Console.WriteLine("║          动画打断修复 - 完整测试套件                    ║");
        Console.WriteLine("║                                                          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine("\n\n");
        
        // 1. 独立验证测试（最重要）
        await RunStandaloneTests();
        
        // 2. 取消令牌调试测试
        await RunCancellationTokenDebugTests();
        
        // 3. 动画引擎测试
        await RunAnimationEngineTests();
        
        Console.WriteLine("\n\n");
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                                                          ║");
        Console.WriteLine("║          所有测试完成                                    ║");
        Console.WriteLine("║                                                          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine("\n\n");
    }
}
