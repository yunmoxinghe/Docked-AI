using System;
using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;
using Docked_AI.功能.主窗口v2.状态机;

namespace Docked_AI.功能.主窗口v2.动画系统.策略;

/// <summary>
/// 默认动画策略实现
/// 提供全局一致的动画参数，支持防抖和上下文感知的动画调整
/// </summary>
/// <remarks>
/// **设计理念：**
/// - 防抖：快速切换时使用短动画（120ms linear）
/// - 语义化：根据转换类型（EnterFullscreen、ExitFullscreen 等）返回不同的动画参数
/// - 一致性：相同的状态转换每次都使用相同的动画参数
/// - 正确处理 Initializing 状态的转换
/// 
/// **验证需求: 10.6, 10.7, 10.8, 10.9, 10.10, 10.11, 28.6, 28.7**
/// </remarks>
public class DefaultAnimationPolicy : IAnimationPolicy
{
    private DateTime _lastTransitionTime = DateTime.MinValue;

    /// <summary>
    /// 根据状态转换和当前视觉状态，解析出最终的动画规格
    /// </summary>
    /// <param name="fromState">起始稳定状态（非中间状态）
    /// 注意：fromState 可能是 Initializing，实现需要能处理这种情况</param>
    /// <param name="toState">目标状态</param>
    /// <param name="currentVisual">当前实际视觉状态（可能是中间状态）</param>
    /// <returns>动画规格，如果返回 null 则回退到使用目标状态的 GetAnimationSpec 方法</returns>
    public AnimationSpec? Resolve(WindowState fromState, WindowState toState, WindowVisualState currentVisual)
    {
        // 1. 防抖：如果距离上次转换很近（<100ms），使用快速动画
        var timeSinceLastTransition = DateTime.Now - _lastTransitionTime;
        if (timeSinceLastTransition < TimeSpan.FromMilliseconds(100))
        {
            _lastTransitionTime = DateTime.Now;
            return new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(120),
                Easing = Easing.Linear
            };
        }

        // 更新上次转换时间
        _lastTransitionTime = DateTime.Now;

        // 2. 根据转换类型选择动画
        var transitionType = GetTransitionType(fromState, toState);

        return transitionType switch
        {
            TransitionType.EnterFullscreen => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(400),
                Spring = new SpringConfig { Stiffness = 300, Damping = 30 }
            },
            TransitionType.ExitFullscreen => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(300),
                Easing = Easing.EaseOutCubic
            },
            TransitionType.DockToSidebar => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(250),
                Easing = Easing.EaseOutCubic
            },
            TransitionType.Float => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = Easing.EaseInOutCubic
            },
            TransitionType.Hide => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = Easing.EaseOutCubic
            },
            TransitionType.Show => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = Easing.EaseInCubic
            },
            _ => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(250),
                Easing = Easing.EaseInOutCubic
            }
        };
    }

    /// <summary>
    /// 将（from、to）状态对映射到 TransitionType
    /// 正确处理 Initializing 状态的转换
    /// </summary>
    private TransitionType GetTransitionType(WindowState from, WindowState to)
    {
        return (from, to) switch
        {
            // 处理 Initializing 状态的转换
            (WindowState.Initializing, WindowState.Fullscreen) => TransitionType.EnterFullscreen,
            (WindowState.Initializing, WindowState.Sidebar) => TransitionType.DockToSidebar,
            (WindowState.Initializing, WindowState.Floating) => TransitionType.Float,
            (WindowState.Initializing, WindowState.Hidden) => TransitionType.Hide,

            // 处理其他状态的转换
            (_, WindowState.Fullscreen) => TransitionType.EnterFullscreen,
            (WindowState.Fullscreen, _) => TransitionType.ExitFullscreen,
            (_, WindowState.Sidebar) => TransitionType.DockToSidebar,
            (_, WindowState.Floating) => TransitionType.Float,
            (_, WindowState.Hidden) => TransitionType.Hide,
            (WindowState.Hidden, _) => TransitionType.Show,
            _ => TransitionType.Default
        };
    }
}

/// <summary>
/// 转换类型枚举
/// 定义不同的状态转换场景，用于选择合适的动画参数
/// </summary>
internal enum TransitionType
{
    /// <summary>
    /// 默认转换
    /// </summary>
    Default,

    /// <summary>
    /// 进入全屏模式
    /// </summary>
    EnterFullscreen,

    /// <summary>
    /// 退出全屏模式
    /// </summary>
    ExitFullscreen,

    /// <summary>
    /// 停靠到边栏
    /// </summary>
    DockToSidebar,

    /// <summary>
    /// 浮窗模式
    /// </summary>
    Float,

    /// <summary>
    /// 隐藏窗口
    /// </summary>
    Hide,

    /// <summary>
    /// 显示窗口
    /// </summary>
    Show
}
