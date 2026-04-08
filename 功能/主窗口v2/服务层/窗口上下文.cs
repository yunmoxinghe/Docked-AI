using System;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;

namespace Docked_AI.功能.主窗口v2.服务层;

/// <summary>
/// 窗口上下文，集中管理窗口实例、HWND 和当前视觉状态
/// 解决多个模块需要访问相同窗口信息的问题，避免重复传递参数和循环依赖
/// </summary>
public class WindowContext
{
    private readonly Window _window;
    private IntPtr _hwnd;
    private WindowVisualState _currentVisual;
    
    /// <summary>
    /// AppBar 注册状态（由 SidebarWindow 使用）
    /// 使用实例字段而非静态字段，因为每个窗口有独立的 AppBar 状态
    /// </summary>
    private bool _isAppBarRegistered = false;
    
    /// <summary>
    /// 浮窗是否启用了大小调整（由 FloatingWindow 使用）
    /// </summary>
    private bool _isResizeEnabled = false;

    /// <summary>
    /// 创建窗口上下文
    /// </summary>
    /// <param name="window">WinUI3 窗口实例</param>
    /// <exception cref="ArgumentNullException">当 window 为 null 时抛出</exception>
    public WindowContext(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _hwnd = IntPtr.Zero;
        _currentVisual = new WindowVisualState();
    }
    
    /// <summary>
    /// 获取 AppBar 注册状态
    /// </summary>
    public bool IsAppBarRegistered => _isAppBarRegistered;
    
    /// <summary>
    /// 设置 AppBar 注册状态
    /// </summary>
    public void SetAppBarRegistered(bool registered)
    {
        _isAppBarRegistered = registered;
    }
    
    /// <summary>
    /// 获取大小调整启用状态
    /// </summary>
    public bool IsResizeEnabled => _isResizeEnabled;
    
    /// <summary>
    /// 设置大小调整启用状态
    /// </summary>
    public void SetResizeEnabled(bool enabled)
    {
        _isResizeEnabled = enabled;
    }

    /// <summary>
    /// 获取窗口实例
    /// </summary>
    /// <returns>WinUI3 窗口实例</returns>
    public Window GetWindow() => _window;

    /// <summary>
    /// 获取窗口句柄（HWND）
    /// 如果尚未获取，则自动获取并缓存
    /// </summary>
    /// <returns>窗口句柄</returns>
    /// <exception cref="InvalidOperationException">当无法获取窗口句柄时抛出</exception>
    public IntPtr GetHwnd()
    {
        if (_hwnd == IntPtr.Zero)
        {
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            
            if (_hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to get window handle (HWND) from WinUI3 window");
            }
        }
        return _hwnd;
    }

    /// <summary>
    /// 获取当前窗口的实时视觉状态
    /// 从窗口读取当前的实际视觉状态并更新缓存
    /// </summary>
    /// <returns>当前窗口视觉状态快照</returns>
    public WindowVisualState GetCurrentVisual()
    {
        try
        {
            var hwnd = GetHwnd();
            
            // 读取窗口边界（位置和尺寸）
            var (left, top, right, bottom) = WindowService.GetWindowBounds(hwnd);
            var bounds = new Rect(left, top, right - left, bottom - top);
            
            // 读取窗口不透明度
            // 注意：WinUI3 的 Window 类没有 Opacity 属性
            // 不透明度通过 Win32 API 的 SetLayeredWindowAttributes 管理
            // 这里返回缓存的值，因为读取需要额外的 Win32 API 调用
            var opacity = _currentVisual.Opacity;
            
            // 读取窗口置顶状态
            var isTopmost = GetCurrentTopmostState(hwnd);
            
            // 读取扩展样式
            var extendedStyle = GetCurrentExtendedStyle(hwnd);
            
            // 读取圆角半径（从 DWM 属性推断）
            var cornerRadius = GetCurrentCornerRadius(hwnd);
            
            // 更新缓存
            _currentVisual = new WindowVisualState
            {
                Bounds = bounds,
                CornerRadius = cornerRadius,
                Opacity = opacity,
                IsTopmost = isTopmost,
                ExtendedStyle = extendedStyle
            };
            
            return _currentVisual;
        }
        catch (Exception ex)
        {
            // 如果读取失败，返回缓存的视觉状态
            // 这样可以避免在动画过程中因为读取失败而中断
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to read current visual state: {ex.Message}");
            return _currentVisual;
        }
    }

    /// <summary>
    /// 更新当前视觉状态缓存
    /// 由 AnimationEngine 在动画过程中调用，避免重复读取窗口属性
    /// </summary>
    /// <param name="visual">新的视觉状态</param>
    /// <exception cref="ArgumentNullException">当 visual 为 null 时抛出</exception>
    public void UpdateCurrentVisual(WindowVisualState visual)
    {
        _currentVisual = visual ?? throw new ArgumentNullException(nameof(visual));
    }

    /// <summary>
    /// 获取当前窗口的置顶状态
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>true 表示窗口置顶，false 表示不置顶</returns>
    private bool GetCurrentTopmostState(IntPtr hwnd)
    {
        try
        {
            int exStyle = 服务层.Win32互操作.窗口样式互操作.GetWindowLong(hwnd, 服务层.Win32互操作.窗口样式互操作.GWL_EXSTYLE);
            return (exStyle & 服务层.Win32互操作.窗口样式互操作.WS_EX_TOPMOST) != 0;
        }
        catch
        {
            // 读取失败，返回默认值
            return false;
        }
    }

    /// <summary>
    /// 获取当前窗口的扩展样式
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>扩展样式标志</returns>
    private int GetCurrentExtendedStyle(IntPtr hwnd)
    {
        try
        {
            return 服务层.Win32互操作.窗口样式互操作.GetWindowLong(hwnd, 服务层.Win32互操作.窗口样式互操作.GWL_EXSTYLE);
        }
        catch
        {
            // 读取失败，返回默认值
            return 0;
        }
    }

    /// <summary>
    /// 获取当前窗口的圆角半径
    /// 注意：DWM API 不提供直接读取圆角半径的方法，这里返回基于圆角偏好的估计值
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>圆角半径（像素）</returns>
    private double GetCurrentCornerRadius(IntPtr hwnd)
    {
        // DWM API 不提供读取圆角半径的方法
        // 我们只能根据设置的圆角偏好返回估计值
        // 实际的圆角半径由系统决定
        
        // 这里返回缓存的值，因为我们无法从系统读取
        // 动画引擎会在设置圆角时更新这个缓存
        return _currentVisual.CornerRadius;
    }
}
