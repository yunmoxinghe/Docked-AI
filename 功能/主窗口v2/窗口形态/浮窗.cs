using System;
using Windows.Foundation;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;
using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.服务层;
using Docked_AI.功能.主窗口v2.服务层.Win32互操作;

namespace Docked_AI.功能.主窗口v2.窗口形态;

/// <summary>
/// 浮窗形态
/// 可自由移动和调整大小的窗口，置顶显示，带圆角
/// </summary>
public class FloatingWindow : IWindowState
{
    private readonly WindowContext _context;
    private readonly IWindowPositionService _positionService;

    /// <summary>
    /// 创建浮窗形态实例
    /// </summary>
    /// <param name="context">窗口上下文</param>
    /// <param name="positionService">窗口位置服务</param>
    public FloatingWindow(WindowContext context, IWindowPositionService positionService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _positionService = positionService ?? throw new ArgumentNullException(nameof(positionService));
    }

    /// <summary>
    /// 获取浮窗的目标视觉状态
    /// 恢复上次位置/大小或使用默认值（400x600 居中）
    /// </summary>
    public WindowVisualState GetTargetVisual()
    {
        // 获取上次停留位置和尺寸，如果没有则使用默认值
        var lastPosition = _positionService.GetLastFloatingPosition();
        
        if (lastPosition != null)
        {
            // 根据保存的边缘距离和当前屏幕尺寸计算位置
            var hwnd = _context.GetHwnd();
            var (monitorBounds, workArea) = WindowService.GetCurrentScreen(hwnd);
            
            var rightDistance = lastPosition.RightDistance;
            var bottomDistance = lastPosition.BottomDistance;
            var width = lastPosition.Width;
            var height = lastPosition.Height;
            
            // 基于保存的距屏幕边缘的右/下距离计算位置
            var x = monitorBounds.Right - rightDistance - width;
            var y = monitorBounds.Bottom - bottomDistance - height;
            
            // 确保窗口在屏幕范围内（至少部分可见）
            x = Math.Max(monitorBounds.Left, Math.Min(x, monitorBounds.Right - 100));
            y = Math.Max(monitorBounds.Top, Math.Min(y, monitorBounds.Bottom - 100));
            
            return new WindowVisualState
            {
                Bounds = new Rect(x, y, width, height),
                CornerRadius = 12,
                Opacity = 1.0,
                IsTopmost = true,
                ExtendedStyle = 0  // 不使用工具窗口样式，这是主窗口而不是工具窗口
            };
        }
        else
        {
            // 首次启动，停靠在屏幕右侧（与旧代码保持一致）
            var hwnd = _context.GetHwnd();
            var (monitorBounds, workArea) = WindowService.GetCurrentScreen(hwnd);
            
            const int defaultMargin = 10;
            const int minWindowWidth = 380;
            
            // 计算窗口尺寸（与旧代码逻辑一致）
            // 宽度：工作区宽度的 1/3
            int availableWidth = workArea.Width - (defaultMargin * 2);
            int windowWidth = availableWidth / 3;
            windowWidth = Math.Max(minWindowWidth, windowWidth);
            windowWidth = Math.Min(availableWidth, windowWidth);
            
            // 高度：工作区高度减去上下边距
            int windowHeight = workArea.Height - (defaultMargin * 2);
            
            // 定位：基于工作区（不是屏幕边界）
            // 停靠在工作区右侧，距离右边缘 10 像素
            var x = workArea.Right - windowWidth - defaultMargin;
            var y = workArea.Top + defaultMargin;
            
            return new WindowVisualState
            {
                Bounds = new Rect(x, y, windowWidth, windowHeight),
                CornerRadius = 12,
                Opacity = 1.0,
                IsTopmost = true,
                ExtendedStyle = 0  // 不使用工具窗口样式，这是主窗口而不是工具窗口
            };
        }
    }

    /// <summary>
    /// 获取动画规格
    /// 根据距离动态调整动画时长（小于100px则150ms，否则300ms）
    /// </summary>
    public AnimationSpec GetAnimationSpec(WindowVisualState from, WindowVisualState to)
    {
        // 根据距离动态调整动画时长
        var distance = CalculateDistance(from.Bounds, to.Bounds);
        var duration = distance < 100 
            ? TimeSpan.FromMilliseconds(150) 
            : TimeSpan.FromMilliseconds(300);

        return new AnimationSpec
        {
            Duration = duration,
            Easing = Easing.EaseInOutCubic
        };
    }

    /// <summary>
    /// 进入浮窗状态时的生命周期钩子
    /// 设置窗口可见、可交互、启用大小调整、尝试获得焦点
    /// 
    /// 幂等性保证：
    /// - ShowWindow 可以安全地多次调用（Win32 API 本身是幂等的）
    /// - EnableResize 可以安全地多次调用（只是设置窗口样式标志）
    /// - TryBringToFront 可以安全地多次调用（焦点管理是幂等的）
    /// - IsShownInSwitchers 可以安全地多次设置（属性赋值是幂等的）
    /// 
    /// 注意：当动画被打断时，OnEnter 可能被多次调用而 OnExit 未被调用。
    /// 本实现确保所有操作都是幂等的，不会产生副作用或资源泄漏。
    /// </summary>
    public void OnEnter()
    {
        // 进入浮窗模式前，确保窗口可见和可交互
        var window = _context.GetWindow();
        
        // 注意：WinUI3 的 Window 类没有 IsVisible 和 IsHitTestVisible 属性
        // 这些属性是 UIElement 的属性，Window 本身不继承自 UIElement
        // 我们通过 Win32 API 控制窗口可见性
        var hwnd = _context.GetHwnd();
        
        // ✅ 幂等操作：ShowWindow 可以安全地多次调用
        WindowService.ShowWindow(hwnd);
        
        // ✅ 幂等操作：EnableResize 只是设置窗口样式标志，可以多次调用
        WindowService.EnableResize(hwnd);
        
        // ✅ 幂等操作：TryBringToFront 可以安全地多次调用
        WindowService.TryBringToFront(window);
        
        // ✅ 幂等操作：设置不在任务切换器中显示（与旧代码保持一致）
        // 使用 IsShownInSwitchers 而不是 WS_EX_TOOLWINDOW，因为这是主窗口而不是工具窗口
        window.AppWindow.IsShownInSwitchers = false;
    }

    /// <summary>
    /// 离开浮窗状态时的生命周期钩子
    /// 保存当前位置、尺寸和边缘距离，禁用大小调整
    /// </summary>
    public void OnExit()
    {
        // 保存当前位置、尺寸和边缘距离以便下次恢复
        var currentVisual = _context.GetCurrentVisual();
        var hwnd = _context.GetHwnd();
        var (monitorBounds, workArea) = WindowService.GetCurrentScreen(hwnd);
        
        var rightDistance = monitorBounds.Right - currentVisual.Bounds.Right;
        var bottomDistance = monitorBounds.Bottom - currentVisual.Bounds.Bottom;
        
        _positionService.SaveFloatingPosition(
            currentVisual.Bounds.Width,
            currentVisual.Bounds.Height,
            rightDistance,
            bottomDistance
        );
        
        // 禁用窗口大小调整
        WindowService.DisableResize(hwnd);
    }

    /// <summary>
    /// 计算居中位置
    /// </summary>
    private Rect CalculateCenteredPosition(int width, int height)
    {
        var hwnd = _context.GetHwnd();
        var (monitorBounds, workArea) = WindowService.GetCurrentScreen(hwnd);
        
        var x = (monitorBounds.Width - width) / 2 + monitorBounds.Left;
        var y = (monitorBounds.Height - height) / 2 + monitorBounds.Top;
        
        return new Rect(x, y, width, height);
    }

    /// <summary>
    /// 计算两个矩形之间的距离（中心点距离）
    /// </summary>
    private double CalculateDistance(Rect from, Rect to)
    {
        var fromCenterX = from.X + from.Width / 2;
        var fromCenterY = from.Y + from.Height / 2;
        var toCenterX = to.X + to.Width / 2;
        var toCenterY = to.Y + to.Height / 2;
        
        var dx = toCenterX - fromCenterX;
        var dy = toCenterY - fromCenterY;
        
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
