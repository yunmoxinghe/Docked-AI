using System;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;
using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.服务层;

namespace Docked_AI.功能.主窗口v2.窗口形态;

/// <summary>
/// 隐藏窗口形态
/// 通过设置 Opacity=0.0 实现淡出效果，动画完成后从视觉树中移除
/// </summary>
public class HiddenWindow : IWindowState
{
    private readonly WindowContext _context;

    /// <summary>
    /// 创建隐藏窗口形态实例
    /// </summary>
    /// <param name="context">窗口上下文</param>
    public HiddenWindow(WindowContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// 获取隐藏窗口的目标视觉状态
    /// 保持当前的 Bounds 和 CornerRadius，只改变 Opacity 为 0.0
    /// </summary>
    public WindowVisualState GetTargetVisual()
    {
        // 获取当前视觉状态
        var currentVisual = _context.GetCurrentVisual();
        
        // 保持当前位置、尺寸和圆角，只改变透明度
        return new WindowVisualState
        {
            Bounds = currentVisual.Bounds,
            CornerRadius = currentVisual.CornerRadius,
            Opacity = 0.0,
            IsTopmost = currentVisual.IsTopmost,
            ExtendedStyle = currentVisual.ExtendedStyle
        };
    }

    /// <summary>
    /// 获取动画规格
    /// 使用 200ms EaseOutCubic 缓动，提供平滑的淡出效果
    /// </summary>
    public AnimationSpec GetAnimationSpec(WindowVisualState from, WindowVisualState to)
    {
        return new AnimationSpec
        {
            Duration = TimeSpan.FromMilliseconds(200),
            Easing = Easing.EaseOutCubic
        };
    }

    /// <summary>
    /// 进入隐藏状态时的生命周期钩子
    /// 保持为空，允许淡出动画播放
    /// 注意：不在这里设置 IsVisible=false，以便动画能够正常播放
    /// 
    /// 幂等性保证：
    /// - 本方法为空实现，天然幂等
    /// 
    /// 注意：当动画被打断时，OnEnter 可能被多次调用而 OnExit 未被调用。
    /// 由于本方法为空，不会产生任何副作用。
    /// </summary>
    public void OnEnter()
    {
        // 保持为空 - 允许淡出动画播放
        // IsVisible 保持为 true，直到动画完成后在 OnExit 中设置为 false
    }

    /// <summary>
    /// 离开隐藏状态时的生命周期钩子
    /// 在动画完成后设置 IsVisible=false 和 IsHitTestVisible=false
    /// 
    /// 注意：当动画被打断时，OnExit 不会被调用。
    /// 这意味着窗口可能保持在半透明状态（Opacity 在 0 和 1 之间）。
    /// 下一次 OnEnter（无论是哪个状态）会重新设置窗口的可见性和透明度。
    /// </summary>
    public void OnExit()
    {
        // 动画完成后，从视觉树中移除窗口
        var hwnd = _context.GetHwnd();
        WindowService.HideWindow(hwnd);
    }
}
