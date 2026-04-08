using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;
using Docked_AI.功能.主窗口v2.状态机;

namespace Docked_AI.功能.主窗口v2.动画系统.策略;

/// <summary>
/// 全局动画策略接口，统一管理动画参数
/// 提供全局动画一致性，根据状态转换类型返回适当的 AnimationSpec
/// </summary>
/// <remarks>
/// **设计理念：**
/// - 返回 null 表示使用状态自己的 GetAnimationSpec
/// - 支持防抖和上下文感知的动画参数调整
/// - 确保相同的状态转换每次都使用相同的动画参数
/// 
/// **验证需求: 10.1, 10.2, 10.3, 10.4, 28.1, 28.2, 28.3, 28.5**
/// </remarks>
public interface IAnimationPolicy
{
    /// <summary>
    /// 根据状态转换和当前视觉状态，解析出最终的动画规格
    /// </summary>
    /// <param name="fromState">起始稳定状态（非中间状态）
    /// 注意：fromState 可能是 Initializing，实现需要能处理这种情况</param>
    /// <param name="toState">目标状态</param>
    /// <param name="currentVisual">当前实际视觉状态（可能是中间状态）</param>
    /// <returns>
    /// 动画规格，如果返回 null 则回退到使用目标状态的 GetAnimationSpec 方法
    /// </returns>
    AnimationSpec? Resolve(WindowState fromState, WindowState toState, WindowVisualState currentVisual);
}
