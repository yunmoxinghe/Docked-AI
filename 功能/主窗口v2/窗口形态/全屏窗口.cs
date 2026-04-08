using System;
using Windows.Foundation;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;
using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.服务层;

namespace Docked_AI.功能.主窗口v2.窗口形态;

/// <summary>
/// 全屏窗口形态
/// 覆盖整个屏幕的展开视图，无圆角，不置顶
/// </summary>
public class FullscreenWindow : IWindowState
{
    private readonly WindowContext _context;

    /// <summary>
    /// 创建全屏窗口形态实例
    /// </summary>
    /// <param name="context">窗口上下文</param>
    public FullscreenWindow(WindowContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// 获取全屏窗口的目标视觉状态
    /// 覆盖当前屏幕的完整边界，无圆角，完全不透明，不置顶
    /// </summary>
    public WindowVisualState GetTargetVisual()
    {
        var hwnd = _context.GetHwnd();
        var (monitorBounds, workArea) = WindowService.GetCurrentScreen(hwnd);
        
        return new WindowVisualState
        {
            Bounds = new Rect(monitorBounds.Left, monitorBounds.Top, 
                            monitorBounds.Right - monitorBounds.Left, 
                            monitorBounds.Bottom - monitorBounds.Top),
            CornerRadius = 0,
            Opacity = 1.0,
            IsTopmost = false,
            ExtendedStyle = 0
        };
    }

    /// <summary>
    /// 获取动画规格
    /// 如果从很小的窗口放大（大小比率 > 2.0），使用 Spring 动画更自然
    /// 否则使用 EaseInOutCubic 缓动
    /// </summary>
    public AnimationSpec GetAnimationSpec(WindowVisualState from, WindowVisualState to)
    {
        // 如果从很小的窗口放大，使用 Spring 更自然
        var sizeRatio = to.Bounds.Width / from.Bounds.Width;
        
        if (sizeRatio > 2.0)
        {
            return new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(400),
                Spring = new SpringConfig
                {
                    Stiffness = 300,
                    Damping = 30
                }
            };
        }

        return new AnimationSpec
        {
            Duration = TimeSpan.FromMilliseconds(300),
            Easing = Easing.EaseInOutCubic
        };
    }

    /// <summary>
    /// 进入全屏状态时的生命周期钩子
    /// 设置窗口可见、可交互、禁用大小调整、尝试获得焦点
    /// 
    /// 幂等性保证：
    /// - ShowWindow 可以安全地多次调用（Win32 API 本身是幂等的）
    /// - DisableResize 可以安全地多次调用（只是设置窗口样式标志）
    /// - TryBringToFront 可以安全地多次调用（焦点管理是幂等的）
    /// 
    /// 注意：当动画被打断时，OnEnter 可能被多次调用而 OnExit 未被调用。
    /// 本实现确保所有操作都是幂等的，不会产生副作用或资源泄漏。
    /// </summary>
    public void OnEnter()
    {
        var hwnd = _context.GetHwnd();
        
        // ✅ 幂等操作：ShowWindow 可以安全地多次调用
        WindowService.ShowWindow(hwnd);
        
        // ✅ 幂等操作：DisableResize 只是设置窗口样式标志，可以多次调用
        WindowService.DisableResize(hwnd);
        
        // ✅ 幂等操作：TryBringToFront 可以安全地多次调用
        var window = _context.GetWindow();
        WindowService.TryBringToFront(window);
    }

    /// <summary>
    /// 离开全屏状态时的生命周期钩子
    /// 可选清理操作（当前为空实现）
    /// </summary>
    public void OnExit()
    {
        // 可选：恢复任务栏或其他清理操作
        // 当前无需特殊清理
    }
}
