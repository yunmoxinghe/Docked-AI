using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Docked_AI.Features.MainWindow.State;

/// <summary>
/// 窗口状态枚举，表示窗口的五种状态
/// </summary>
public enum WindowState
{
    /// <summary>
    /// 窗口尚未创建
    /// </summary>
    NotCreated,

    /// <summary>
    /// 窗口已隐藏
    /// </summary>
    Hidden,

    /// <summary>
    /// 窗口化模式（标准停靠）
    /// </summary>
    Windowed,

    /// <summary>
    /// 最大化模式
    /// </summary>
    Maximized,

    /// <summary>
    /// 固定模式（AppBar）
    /// </summary>
    Pinned
}

/// <summary>
/// 状态转换记录，用于追踪状态转换历史
/// </summary>
public record StateTransition(
    WindowState FromState,
    WindowState ToState,
    DateTime Timestamp,
    string? Reason = null
);

/// <summary>
/// 状态变化事件参数
/// </summary>
public class StateChangedEventArgs : EventArgs
{
    public WindowState PreviousState { get; }
    public WindowState CurrentState { get; }
    public DateTime Timestamp { get; }
    public string? Reason { get; }
    public int TransitionId { get; } // 添加 TransitionId 用于提交/回滚

    public StateChangedEventArgs(
        WindowState previousState,
        WindowState currentState,
        DateTime timestamp,
        int transitionId,
        string? reason = null)
    {
        PreviousState = previousState;
        CurrentState = currentState;
        Timestamp = timestamp;
        TransitionId = transitionId;
        Reason = reason;
    }
}

/// <summary>
/// 状态转换执行计划（命令模式）
/// StateManager 返回计划，Controller 执行计划
/// 
/// CRITICAL: TransitionId 用于防止竞态条件
/// 场景：A→B (transitionId=1) 执行中，用户触发 B→C (transitionId=2)
/// 结果：旧的 commit(1) 延迟到达时会被拒绝，避免覆盖正确状态
/// </summary>
public record TransitionPlan(
    int TransitionId,
    WindowState From,
    WindowState To,
    Func<Task> Execute,
    Func<Task>? Compensate = null
);

/// <summary>
/// 组合转换记录，包含子转换列表
/// 用于记录 Pinned -> Windowed -> Hidden 等多步转换
/// </summary>
public record CompositeTransition(
    WindowState From,
    WindowState To,
    List<StateTransition> SubTransitions,
    DateTime Timestamp
);

/// <summary>
/// 抽象线程调度接口，便于测试
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// 尝试将回调加入调度队列
    /// </summary>
    /// <param name="callback">要执行的回调</param>
    /// <returns>如果成功加入队列返回 true，否则返回 false</returns>
    bool TryEnqueue(Action callback);
}

/// <summary>
/// WinUI 调度器实现（生产环境）
/// </summary>
internal sealed class WinUIDispatcher : IDispatcher
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _queue;

    public WinUIDispatcher(Microsoft.UI.Dispatching.DispatcherQueue queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    public bool TryEnqueue(Action callback)
    {
        return _queue.TryEnqueue(() => callback());
    }
}

/// <summary>
/// 同步调度器实现（测试环境）
/// 回调同步执行，避免测试中的时序差异
/// </summary>
internal sealed class SynchronousDispatcher : IDispatcher
{
    public bool TryEnqueue(Action callback)
    {
        callback();
        return true;
    }
}
