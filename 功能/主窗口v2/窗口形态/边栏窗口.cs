using System;
using Windows.Foundation;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;
using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.服务层;
using Docked_AI.功能.主窗口v2.服务层.Win32互操作;

namespace Docked_AI.功能.主窗口v2.窗口形态;

/// <summary>
/// 边栏窗口形态
/// 吸附在屏幕右边缘的固定侧边栏（400 x 屏幕高度），无圆角
/// 通过 AppBar 注册占用屏幕工作区
/// </summary>
public class SidebarWindow : IWindowState
{
    private readonly WindowContext _context;

    /// <summary>
    /// 创建边栏窗口形态实例
    /// </summary>
    /// <param name="context">窗口上下文</param>
    public SidebarWindow(WindowContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// 获取边栏窗口的目标视觉状态
    /// 定位在屏幕右边缘，宽度 400，高度为屏幕高度，无圆角，完全不透明
    /// </summary>
    public WindowVisualState GetTargetVisual()
    {
        var hwnd = _context.GetHwnd();
        var (monitorBounds, workArea) = WindowService.GetCurrentScreen(hwnd);
        
        return new WindowVisualState
        {
            Bounds = new Rect(
                monitorBounds.Right - 400, 
                monitorBounds.Top, 
                400, 
                monitorBounds.Bottom - monitorBounds.Top),
            CornerRadius = 0,
            Opacity = 1.0,
            IsTopmost = false,
            ExtendedStyle = 0
        };
    }

    /// <summary>
    /// 获取动画规格
    /// 使用 250ms EaseOutCubic 缓动，提供流畅的滑入效果
    /// </summary>
    public AnimationSpec GetAnimationSpec(WindowVisualState from, WindowVisualState to)
    {
        return new AnimationSpec
        {
            Duration = TimeSpan.FromMilliseconds(250),
            Easing = Easing.EaseOutCubic
        };
    }

    /// <summary>
    /// 进入边栏状态时的生命周期钩子
    /// 设置窗口可见、可交互、禁用大小调整、注册为 AppBar 占用屏幕工作区
    /// 
    /// 幂等性保证：
    /// - ShowWindow 可以安全地多次调用（Win32 API 本身是幂等的）
    /// - DisableResize 可以安全地多次调用（只是设置窗口样式标志）
    /// - RegisterAppBar 使用 WindowContext 中的标志防止重复注册，确保幂等性
    /// 
    /// 注意：当动画被打断时，OnEnter 可能被多次调用而 OnExit 未被调用。
    /// 本实现使用 _context.IsAppBarRegistered 标志（在 WindowContext 中持久化）防止重复注册。
    /// </summary>
    public void OnEnter()
    {
        var hwnd = _context.GetHwnd();
        
        // ✅ 幂等操作：ShowWindow 可以安全地多次调用
        WindowService.ShowWindow(hwnd);
        
        // ✅ 幂等操作：DisableResize 只是设置窗口样式标志，可以多次调用
        WindowService.DisableResize(hwnd);
        
        // ⚠️ 使用 WindowContext 中的标志防止重复注册
        // 这确保即使窗口形态实例被重新创建，状态也不会丢失
        if (!_context.IsAppBarRegistered)
        {
            // 通过 SHAppBarMessage 注册为 AppBar，占用屏幕工作区
            // 停靠在右边缘，宽度为 400 像素
            WindowService.RegisterAppBar(hwnd, AppBarEdge.Right, 400);
            _context.SetAppBarRegistered(true);
        }
    }

    /// <summary>
    /// 离开边栏状态时的生命周期钩子
    /// 取消 AppBar 注册，释放屏幕工作区
    /// 
    /// 注意：当动画被打断时，OnExit 不会被调用。
    /// 因此，下一次 OnEnter 必须能够处理 AppBar 已经注册的情况。
    /// 由于状态存储在 WindowContext 中，即使实例被重新创建也能正确处理。
    /// </summary>
    public void OnExit()
    {
        // 取消 AppBar 注册
        var hwnd = _context.GetHwnd();
        
        // 只有在已注册的情况下才取消注册
        if (_context.IsAppBarRegistered)
        {
            WindowService.UnregisterAppBar(hwnd);
            _context.SetAppBarRegistered(false);
        }
    }
}
