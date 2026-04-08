using System;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;
using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.服务层;

namespace Docked_AI.功能.主窗口v2.窗口形态;

/// <summary>
/// 初始化窗口形态（占位符）
/// 代表 WinUI3 内部的初始化阶段，不做任何改变
/// 这是唯一的起点状态，不可从其他状态转入
/// </summary>
public class InitializingWindow : IWindowState
{
    private readonly WindowContext _context;

    /// <summary>
    /// 创建初始化窗口形态实例
    /// </summary>
    /// <param name="context">窗口上下文</param>
    public InitializingWindow(WindowContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// 获取初始化窗口的目标视觉状态
    /// 返回当前视觉状态，不做任何改变
    /// </summary>
    public WindowVisualState GetTargetVisual()
    {
        // 返回当前视觉状态的副本，不做任何改变
        return _context.GetCurrentVisual().Clone();
    }

    /// <summary>
    /// 获取动画规格
    /// 返回即时动画（Duration=0），无过渡效果
    /// </summary>
    public AnimationSpec GetAnimationSpec(WindowVisualState from, WindowVisualState to)
    {
        return new AnimationSpec
        {
            Duration = TimeSpan.Zero,
            Easing = Easing.Linear
        };
    }

    /// <summary>
    /// 进入初始化状态时的生命周期钩子
    /// 保持为空，无需任何操作
    /// 
    /// 幂等性保证：
    /// - 本方法为空实现，天然幂等
    /// 
    /// 注意：当动画被打断时，OnEnter 可能被多次调用而 OnExit 未被调用。
    /// 由于本方法为空，不会产生任何副作用。
    /// </summary>
    public void OnEnter()
    {
        // 保持为空 - 初始化状态不需要任何操作
    }

    /// <summary>
    /// 离开初始化状态时的生命周期钩子
    /// 保持为空，无需任何操作
    /// 
    /// 注意：当动画被打断时，OnExit 不会被调用。
    /// 由于本方法为空，不会产生任何影响。
    /// </summary>
    public void OnExit()
    {
        // 保持为空 - 初始化状态不需要任何操作
    }
}
