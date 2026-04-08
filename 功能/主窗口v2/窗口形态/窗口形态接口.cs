using Docked_AI.功能.主窗口v2.动画系统.视觉状态;
using Docked_AI.功能.主窗口v2.动画系统.引擎;

namespace Docked_AI.功能.主窗口v2.窗口形态;

/// <summary>
/// 窗口形态接口，每个形态定义目标视觉和动画规格
/// FloatingWindow、FullscreenWindow、SidebarWindow、HiddenWindow 各自实现此接口
/// </summary>
public interface IWindowState
{
    /// <summary>
    /// 获取该状态的目标视觉效果
    /// </summary>
    /// <returns>目标视觉状态</returns>
    WindowVisualState GetTargetVisual();

    /// <summary>
    /// 获取动画规格，可根据起点和终点动态调整
    /// </summary>
    /// <param name="from">当前视觉状态</param>
    /// <param name="to">目标视觉状态（通常是 GetTargetVisual() 的返回值）</param>
    /// <returns>动画规格</returns>
    AnimationSpec GetAnimationSpec(WindowVisualState from, WindowVisualState to);

    /// <summary>
    /// 进入该状态时的生命周期钩子（在动画开始前调用）
    /// 用于控制离散状态（如设置 IsVisible = true, IsHitTestVisible = true）
    /// 注意：此方法应该是幂等的（可以被多次调用而不产生副作用）
    /// </summary>
    void OnEnter();

    /// <summary>
    /// 离开该状态时的生命周期钩子（在动画完成后调用）
    /// 用于清理资源或设置离散状态（如设置 IsVisible = false）
    /// </summary>
    void OnExit();
}
