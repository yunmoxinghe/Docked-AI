using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Docked_AI.功能.主窗口v2.状态机;
using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.服务层;
using Docked_AI.功能.主窗口v2.动画系统.策略;

namespace Docked_AI.功能.主窗口v2.测试;

/// <summary>
/// 动画打断集成测试
/// 实际运行状态管理器并验证动画打断功能
/// </summary>
public class 动画打断集成测试
{
    private WindowStateManager? _stateManager;
    private int _stateChangedCount = 0;
    private int _transitionStartedCount = 0;
    private WindowState? _finalState = null;
    private WindowState? _fromState = null;
    
    /// <summary>
    /// 初始化测试环境
    /// 必须在 UI 线程上调用
    /// </summary>
    public void Initialize(Window window)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));
        
        // 创建依赖
        var context = new WindowContext(window);
        var animationEngine = new AnimationEngine();
        var animationPolicy = new DefaultAnimationPolicy();
        
        // 创建状态管理器
        _stateManager = new WindowStateManager(
            dispatcher: Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(),
            animationEngine: animationEngine,
            animationPolicy: animationPolicy,
            context: context
        );
        
        // 订阅事件
        _stateManager.StateChanged += OnStateChanged;
        _stateManager.TransitionStarted += OnTransitionStarted;
        
        Debug.WriteLine("[Test] WindowStateManager initialized");
    }
    
    private void OnStateChanged(WindowState from, WindowState to)
    {
        _stateChangedCount++;
        _fromState = from;
        _finalState = to;
        Debug.WriteLine($"[Test] StateChanged #{_stateChangedCount}: {from} → {to}");
    }
    
    private void OnTransitionStarted(WindowState from, WindowState to)
    {
        _transitionStartedCount++;
        Debug.WriteLine($"[Test] TransitionStarted #{_transitionStartedCount}: {from} → {to}");
    }
    
    /// <summary>
    /// 测试 1：快速连续状态切换
    /// 验证请求压缩和动画打断
    /// </summary>
    public async Task<bool> Test1_RapidStateTransitions()
    {
        if (_stateManager == null)
            throw new InvalidOperationException("Must call Initialize() first");
        
        Debug.WriteLine("");
        Debug.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Debug.WriteLine("║  测试 1：快速连续状态切换                                  ║");
        Debug.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Debug.WriteLine("");
        
        // 重置计数器
        _stateChangedCount = 0;
        _transitionStartedCount = 0;
        _finalState = null;
        _fromState = null;
        
        // 快速连续调用 TransitionTo
        Debug.WriteLine("[Test] Calling TransitionTo(Floating)...");
        _stateManager.TransitionTo(WindowState.Floating);
        
        Debug.WriteLine("[Test] Calling TransitionTo(Fullscreen) immediately...");
        _stateManager.TransitionTo(WindowState.Fullscreen);
        
        Debug.WriteLine("[Test] Calling TransitionTo(Sidebar) immediately...");
        _stateManager.TransitionTo(WindowState.Sidebar);
        
        // 等待转换完成（最多 5 秒）
        Debug.WriteLine("[Test] Waiting for transitions to complete...");
        var timeout = TimeSpan.FromSeconds(5);
        var stopwatch = Stopwatch.StartNew();
        
        while (_stateManager.TransitioningTo != null && stopwatch.Elapsed < timeout)
        {
            await Task.Delay(50);
        }
        
        stopwatch.Stop();
        Debug.WriteLine($"[Test] Transitions completed in {stopwatch.ElapsedMilliseconds}ms");
        Debug.WriteLine("");
        
        // 验证结果
        Debug.WriteLine("验证结果：");
        Debug.WriteLine($"- StateChanged 触发次数: {_stateChangedCount}");
        Debug.WriteLine($"- TransitionStarted 触发次数: {_transitionStartedCount}");
        Debug.WriteLine($"- 最终状态: {_stateManager.CurrentState}");
        Debug.WriteLine($"- StateChanged from: {_fromState}");
        Debug.WriteLine($"- StateChanged to: {_finalState}");
        Debug.WriteLine("");
        
        // 断言
        bool success = true;
        
        if (_finalState != WindowState.Sidebar)
        {
            Debug.WriteLine($"❌ 失败：最终状态应该是 Sidebar，实际是 {_finalState}");
            success = false;
        }
        else
        {
            Debug.WriteLine("✅ 通过：最终状态是 Sidebar");
        }
        
        if (_fromState != WindowState.Initializing)
        {
            Debug.WriteLine($"❌ 失败：StateChanged from 应该是 Initializing，实际是 {_fromState}");
            success = false;
        }
        else
        {
            Debug.WriteLine("✅ 通过：StateChanged from 是 Initializing");
        }
        
        if (_stateChangedCount != 1)
        {
            Debug.WriteLine($"❌ 失败：StateChanged 应该只触发 1 次，实际触发 {_stateChangedCount} 次");
            success = false;
        }
        else
        {
            Debug.WriteLine("✅ 通过：StateChanged 只触发 1 次");
        }
        
        if (_transitionStartedCount < 1)
        {
            Debug.WriteLine($"❌ 失败：TransitionStarted 应该至少触发 1 次，实际触发 {_transitionStartedCount} 次");
            success = false;
        }
        else
        {
            Debug.WriteLine($"✅ 通过：TransitionStarted 触发 {_transitionStartedCount} 次");
        }
        
        Debug.WriteLine("");
        Debug.WriteLine(success ? "✅ 测试 1 通过" : "❌ 测试 1 失败");
        Debug.WriteLine("");
        
        return success;
    }
    
    /// <summary>
    /// 测试 2：动画打断后的状态一致性
    /// 验证打断后 CurrentState 和 TransitioningTo 的正确性
    /// </summary>
    public async Task<bool> Test2_StateConsistency()
    {
        if (_stateManager == null)
            throw new InvalidOperationException("Must call Initialize() first");
        
        Debug.WriteLine("");
        Debug.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Debug.WriteLine("║  测试 2：动画打断后的状态一致性                            ║");
        Debug.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Debug.WriteLine("");
        
        // 开始转换到 Floating
        Debug.WriteLine("[Test] Calling TransitionTo(Floating)...");
        _stateManager.TransitionTo(WindowState.Floating);
        
        // 等待一小段时间（让动画开始）
        await Task.Delay(50);
        
        // 检查 TransitioningTo
        var transitioningTo1 = _stateManager.TransitioningTo;
        Debug.WriteLine($"[Test] TransitioningTo after 50ms: {transitioningTo1}");
        
        // 打断：转换到 Fullscreen
        Debug.WriteLine("[Test] Calling TransitionTo(Fullscreen) to interrupt...");
        _stateManager.TransitionTo(WindowState.Fullscreen);
        
        // 立即检查 TransitioningTo
        var transitioningTo2 = _stateManager.TransitioningTo;
        Debug.WriteLine($"[Test] TransitioningTo immediately after interrupt: {transitioningTo2}");
        
        // 等待转换完成
        Debug.WriteLine("[Test] Waiting for transition to complete...");
        var timeout = TimeSpan.FromSeconds(5);
        var stopwatch = Stopwatch.StartNew();
        
        while (_stateManager.TransitioningTo != null && stopwatch.Elapsed < timeout)
        {
            await Task.Delay(50);
        }
        
        stopwatch.Stop();
        Debug.WriteLine($"[Test] Transition completed in {stopwatch.ElapsedMilliseconds}ms");
        Debug.WriteLine("");
        
        // 验证结果
        Debug.WriteLine("验证结果：");
        Debug.WriteLine($"- CurrentState: {_stateManager.CurrentState}");
        Debug.WriteLine($"- TransitioningTo: {_stateManager.TransitioningTo}");
        Debug.WriteLine("");
        
        // 断言
        bool success = true;
        
        if (_stateManager.CurrentState != WindowState.Fullscreen)
        {
            Debug.WriteLine($"❌ 失败：CurrentState 应该是 Fullscreen，实际是 {_stateManager.CurrentState}");
            success = false;
        }
        else
        {
            Debug.WriteLine("✅ 通过：CurrentState 是 Fullscreen");
        }
        
        if (_stateManager.TransitioningTo != null)
        {
            Debug.WriteLine($"❌ 失败：TransitioningTo 应该是 null，实际是 {_stateManager.TransitioningTo}");
            success = false;
        }
        else
        {
            Debug.WriteLine("✅ 通过：TransitioningTo 是 null");
        }
        
        Debug.WriteLine("");
        Debug.WriteLine(success ? "✅ 测试 2 通过" : "❌ 测试 2 失败");
        Debug.WriteLine("");
        
        return success;
    }
    
    /// <summary>
    /// 运行所有测试
    /// </summary>
    public async Task<bool> RunAllTests()
    {
        Debug.WriteLine("");
        Debug.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Debug.WriteLine("║          动画打断集成测试套件                              ║");
        Debug.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Debug.WriteLine("");
        
        bool test1 = await Test1_RapidStateTransitions();
        bool test2 = await Test2_StateConsistency();
        
        Debug.WriteLine("");
        Debug.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Debug.WriteLine("║          测试总结                                          ║");
        Debug.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Debug.WriteLine($"测试 1（快速连续状态切换）: {(test1 ? "✅ 通过" : "❌ 失败")}");
        Debug.WriteLine($"测试 2（状态一致性）: {(test2 ? "✅ 通过" : "❌ 失败")}");
        Debug.WriteLine("");
        
        bool allPassed = test1 && test2;
        Debug.WriteLine(allPassed ? "✅ 所有测试通过" : "❌ 部分测试失败");
        Debug.WriteLine("");
        
        return allPassed;
    }
}
