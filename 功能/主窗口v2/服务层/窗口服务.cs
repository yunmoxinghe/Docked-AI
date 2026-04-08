using System;
using System.Runtime.InteropServices;
using Docked_AI.功能.主窗口v2.服务层.Win32互操作;

namespace Docked_AI.功能.主窗口v2.服务层;

/// <summary>
/// Win32 API 静态抽象层，无状态，纯函数集
/// 提供清晰的 Win32 API 抽象，供上层模块调用
/// </summary>
public static class WindowService
{
    // ===== 窗口样式和外观方法 =====

    /// <summary>
    /// 移除窗口标题栏
    /// 通过移除 WS_CAPTION 和 WS_SYSMENU 样式实现
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    public static void RemoveTitleBar(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        int style = 窗口样式互操作.GetWindowLong(hwnd, 窗口样式互操作.GWL_STYLE);
        style &= ~窗口样式互操作.WS_CAPTION;
        style &= ~窗口样式互操作.WS_SYSMENU;
        窗口样式互操作.SetWindowLong(hwnd, 窗口样式互操作.GWL_STYLE, style);
    }

    /// <summary>
    /// 设置透明背景
    /// 通过添加 WS_EX_LAYERED 扩展样式实现
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    public static void SetTransparentBackground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        int exStyle = 窗口样式互操作.GetWindowLong(hwnd, 窗口样式互操作.GWL_EXSTYLE);
        exStyle |= 窗口样式互操作.WS_EX_LAYERED;
        窗口样式互操作.SetWindowLong(hwnd, 窗口样式互操作.GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// 设置扩展窗口样式
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="exStyle">扩展样式标志</param>
    public static void SetExtendedStyle(IntPtr hwnd, int exStyle)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        窗口样式互操作.SetWindowLong(hwnd, 窗口样式互操作.GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// 设置窗口置顶状态
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="topmost">true 表示置顶，false 表示取消置顶</param>
    public static void SetTopmost(IntPtr hwnd, bool topmost)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        if (topmost)
        {
            int exStyle = 窗口样式互操作.GetWindowLong(hwnd, 窗口样式互操作.GWL_EXSTYLE);
            exStyle |= 窗口样式互操作.WS_EX_TOPMOST;
            窗口样式互操作.SetWindowLong(hwnd, 窗口样式互操作.GWL_EXSTYLE, exStyle);
        }
        else
        {
            int exStyle = 窗口样式互操作.GetWindowLong(hwnd, 窗口样式互操作.GWL_EXSTYLE);
            exStyle &= ~窗口样式互操作.WS_EX_TOPMOST;
            窗口样式互操作.SetWindowLong(hwnd, 窗口样式互操作.GWL_EXSTYLE, exStyle);
        }
    }

    /// <summary>
    /// 显示窗口
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    public static void ShowWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        窗口样式互操作.ShowWindow(hwnd, 窗口样式互操作.SW_SHOW);
    }

    /// <summary>
    /// 隐藏窗口
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    public static void HideWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        窗口样式互操作.ShowWindow(hwnd, 窗口样式互操作.SW_HIDE);
    }

    /// <summary>
    /// 设置 DWM 属性
    /// 用于设置窗口圆角、亚克力效果等
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="attribute">DWM 属性类型</param>
    /// <param name="value">属性值</param>
    public static void SetDwmAttribute(IntPtr hwnd, DWM互操作.DWMWINDOWATTRIBUTE attribute, int value)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        int result = DWM互操作.DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to set DWM attribute {attribute}. Error code: {result}");
        }
    }

    /// <summary>
    /// 设置窗口圆角
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="preference">圆角偏好</param>
    public static void SetCornerPreference(IntPtr hwnd, DWM互操作.DWM_WINDOW_CORNER_PREFERENCE preference)
    {
        SetDwmAttribute(hwnd, DWM互操作.DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, (int)preference);
    }

    /// <summary>
    /// 设置系统背景类型（亚克力效果）
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="backdropType">背景类型</param>
    public static void SetSystemBackdrop(IntPtr hwnd, DWM互操作.DWM_SYSTEMBACKDROP_TYPE backdropType)
    {
        SetDwmAttribute(hwnd, DWM互操作.DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, (int)backdropType);
    }

    // ===== 窗口定位和大小调整方法 =====

    /// <summary>
    /// 移动窗口到指定坐标
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="x">新的 X 坐标</param>
    /// <param name="y">新的 Y 坐标</param>
    public static void MoveWindow(IntPtr hwnd, int x, int y)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        // 获取当前窗口尺寸
        if (!窗口位置互操作.GetWindowRect(hwnd, out var rect))
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to get window rect. Error code: {error}");
        }

        int width = rect.Width;
        int height = rect.Height;

        // 使用 SetWindowPos 移动窗口，保持尺寸不变
        if (!窗口位置互操作.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            x,
            y,
            width,
            height,
            窗口位置互操作.SWP_NOZORDER | 窗口位置互操作.SWP_NOACTIVATE))
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to move window. Error code: {error}");
        }
    }

    /// <summary>
    /// 调整窗口大小
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="width">新的宽度</param>
    /// <param name="height">新的高度</param>
    public static void ResizeWindow(IntPtr hwnd, int width, int height)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        if (width <= 0)
            throw new ArgumentException("Width must be positive", nameof(width));

        if (height <= 0)
            throw new ArgumentException("Height must be positive", nameof(height));

        // 获取当前窗口位置
        if (!窗口位置互操作.GetWindowRect(hwnd, out var rect))
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to get window rect. Error code: {error}");
        }

        // 使用 SetWindowPos 调整窗口大小，保持位置不变
        if (!窗口位置互操作.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            rect.Left,
            rect.Top,
            width,
            height,
            窗口位置互操作.SWP_NOZORDER | 窗口位置互操作.SWP_NOACTIVATE))
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to resize window. Error code: {error}");
        }
    }

    /// <summary>
    /// 启用窗口大小调整
    /// 通过添加 WS_THICKFRAME 样式实现
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    public static void EnableResize(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        int style = 窗口样式互操作.GetWindowLong(hwnd, 窗口样式互操作.GWL_STYLE);
        style |= 窗口样式互操作.WS_THICKFRAME;
        窗口样式互操作.SetWindowLong(hwnd, 窗口样式互操作.GWL_STYLE, style);

        // 通知窗口样式已更改，需要重新计算非客户区
        窗口位置互操作.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            窗口位置互操作.SWP_NOMOVE | 窗口位置互操作.SWP_NOSIZE | 窗口位置互操作.SWP_NOZORDER | 窗口位置互操作.SWP_FRAMECHANGED);
    }

    /// <summary>
    /// 禁用窗口大小调整
    /// 通过移除 WS_THICKFRAME 样式实现
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    public static void DisableResize(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        int style = 窗口样式互操作.GetWindowLong(hwnd, 窗口样式互操作.GWL_STYLE);
        style &= ~窗口样式互操作.WS_THICKFRAME;
        窗口样式互操作.SetWindowLong(hwnd, 窗口样式互操作.GWL_STYLE, style);

        // 通知窗口样式已更改，需要重新计算非客户区
        窗口位置互操作.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            窗口位置互操作.SWP_NOMOVE | 窗口位置互操作.SWP_NOSIZE | 窗口位置互操作.SWP_NOZORDER | 窗口位置互操作.SWP_FRAMECHANGED);
    }

    /// <summary>
    /// 获取窗口边界（屏幕坐标）
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>窗口矩形（Left, Top, Right, Bottom）</returns>
    public static (int Left, int Top, int Right, int Bottom) GetWindowBounds(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        if (!窗口位置互操作.GetWindowRect(hwnd, out var rect))
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to get window rect. Error code: {error}");
        }

        return (rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    /// <summary>
    /// 获取当前窗口所在的显示器信息
    /// 如果获取失败，返回主显示器信息作为回退
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>显示器信息（显示器矩形和工作区矩形）</returns>
    public static (显示器管理器.RECT MonitorBounds, 显示器管理器.RECT WorkArea) GetCurrentScreen(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        try
        {
            // 获取窗口所在的显示器句柄
            IntPtr hMonitor = 显示器管理器.MonitorFromWindow(hwnd, 显示器管理器.MONITOR_DEFAULTTONEAREST);
            
            if (hMonitor == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Warning: Failed to get monitor handle, falling back to primary monitor");
                return GetPrimaryMonitorInfo();
            }

            // 获取显示器信息
            var monitorInfo = 显示器管理器.MONITORINFO.Default;
            if (!显示器管理器.GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                int error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"Warning: Failed to get monitor info (error {error}), falling back to primary monitor");
                return GetPrimaryMonitorInfo();
            }

            return (monitorInfo.rcMonitor, monitorInfo.rcWork);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Exception getting monitor info: {ex.Message}, falling back to primary monitor");
            return GetPrimaryMonitorInfo();
        }
    }
    
    /// <summary>
    /// 获取主显示器信息（回退方案）
    /// </summary>
    private static (显示器管理器.RECT MonitorBounds, 显示器管理器.RECT WorkArea) GetPrimaryMonitorInfo()
    {
        try
        {
            // 获取主显示器句柄
            IntPtr hMonitor = 显示器管理器.MonitorFromPoint(new 显示器管理器.POINT { X = 0, Y = 0 }, 显示器管理器.MONITOR_DEFAULTTOPRIMARY);
            
            if (hMonitor != IntPtr.Zero)
            {
                var monitorInfo = 显示器管理器.MONITORINFO.Default;
                if (显示器管理器.GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    return (monitorInfo.rcMonitor, monitorInfo.rcWork);
                }
            }
        }
        catch
        {
            // 忽略异常，使用硬编码的默认值
        }
        
        // 最后的回退：使用硬编码的默认值（1920x1080 屏幕）
        System.Diagnostics.Debug.WriteLine("Warning: Using hardcoded default monitor bounds (1920x1080)");
        var defaultMonitor = new 显示器管理器.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
        var defaultWorkArea = new 显示器管理器.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 }; // 减去任务栏高度
        return (defaultMonitor, defaultWorkArea);
    }

    // ===== 焦点管理方法 =====

    /// <summary>
    /// 将窗口设置为前台窗口并尝试获得输入焦点
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>成功返回 true，失败返回 false（受系统限制）</returns>
    public static bool SetForegroundWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        return 焦点管理互操作.SetForegroundWindow(hwnd);
    }

    /// <summary>
    /// 将窗口提升到 Z 轴顶部
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    public static bool BringWindowToTop(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        return 焦点管理互操作.BringWindowToTop(hwnd);
    }

    /// <summary>
    /// 闪烁窗口以吸引用户注意（作为焦点获取失败时的降级方案）
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="flags">闪烁标志（FLASHW_* 常量组合）</param>
    /// <param name="count">闪烁次数（0 表示持续闪烁）</param>
    /// <param name="timeout">闪烁间隔（毫秒，0 表示使用默认值）</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    public static bool FlashWindowEx(IntPtr hwnd, uint flags, uint count, uint timeout)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        var flashInfo = 焦点管理互操作.FLASHWINFO.Create(hwnd, flags, count, timeout);
        return 焦点管理互操作.FlashWindowEx(ref flashInfo);
    }

    /// <summary>
    /// 获取当前前台窗口
    /// </summary>
    /// <returns>前台窗口句柄</returns>
    public static IntPtr GetForegroundWindow()
    {
        return 焦点管理互操作.GetForegroundWindow();
    }

    /// <summary>
    /// 获取窗口所属线程 ID 和进程 ID
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="processId">接收进程 ID 的变量</param>
    /// <returns>线程 ID</returns>
    public static uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        return 焦点管理互操作.GetWindowThreadProcessId(hwnd, out processId);
    }

    /// <summary>
    /// 尝试将窗口带到前台，使用多层策略（Activate → SetForegroundWindow → FlashWindowEx）
    /// 这是推荐的焦点管理方法，符合 Windows UX 规范
    /// </summary>
    /// <param name="window">WinUI3 窗口实例</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    public static bool TryBringToFront(Microsoft.UI.Xaml.Window window)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        // 获取窗口句柄
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get window handle from WinUI3 window");
        }

        // 策略 1: 调用 WinUI3 的 Activate() 方法
        try
        {
            window.Activate();
            
            // 检查是否成功获得焦点
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == hwnd)
            {
                return true;
            }
        }
        catch
        {
            // Activate() 失败，继续尝试其他策略
        }

        // 策略 2: 使用 SetForegroundWindow
        if (SetForegroundWindow(hwnd))
        {
            // 再次检查是否成功
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == hwnd)
            {
                return true;
            }
        }

        // 策略 3: 使用 BringWindowToTop（提升 Z 轴顺序）
        BringWindowToTop(hwnd);

        // 最后检查是否成功
        IntPtr finalForegroundWindow = GetForegroundWindow();
        if (finalForegroundWindow == hwnd)
        {
            return true;
        }

        // 策略 4: 降级方案 - 闪烁窗口以吸引用户注意
        // 使用 FLASHW_ALL | FLASHW_TIMERNOFG 持续闪烁直到用户点击
        FlashWindowEx(hwnd, 焦点管理互操作.FLASHW_ALL | 焦点管理互操作.FLASHW_TIMERNOFG, 0, 0);
        
        return false;
    }

    // ===== AppBar 管理方法 =====

    /// <summary>
    /// 注册窗口为 AppBar（占用屏幕工作区）
    /// 使用 SHAppBarMessage(ABM_NEW, ...) 和 SHAppBarMessage(ABM_SETPOS, ...)
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="edge">AppBar 停靠边缘</param>
    /// <param name="size">AppBar 占用的尺寸（宽度或高度，取决于边缘）</param>
    /// <param name="callbackMessage">AppBar 回调消息 ID（可选，默认为 0）</param>
    /// <returns>AppBar 实际占用的矩形区域</returns>
    public static (int Left, int Top, int Right, int Bottom) RegisterAppBar(
        IntPtr hwnd, 
        AppBarEdge edge, 
        int size, 
        uint callbackMessage = 0)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        if (size <= 0)
            throw new ArgumentException("Size must be positive", nameof(size));

        // 如果没有提供回调消息 ID，注册一个唯一的消息 ID
        if (callbackMessage == 0)
        {
            callbackMessage = AppBar互操作.RegisterWindowMessage("DockedAI_AppBarMessage");
        }

        // 步骤 1: 注册 AppBar
        var appBarData = AppBar互操作.APPBARDATA.Create(hwnd, callbackMessage, edge);
        uint result = AppBar互操作.SHAppBarMessage(AppBar互操作.ABM_NEW, ref appBarData);
        
        if (result == 0)
        {
            throw new InvalidOperationException("Failed to register AppBar with ABM_NEW");
        }

        // 步骤 2: 获取当前屏幕的工作区
        var (monitorBounds, workArea) = GetCurrentScreen(hwnd);

        // 步骤 3: 根据边缘和尺寸设置 AppBar 的初始矩形
        switch (edge)
        {
            case AppBarEdge.Left:
                appBarData.rc.Left = workArea.Left;
                appBarData.rc.Top = workArea.Top;
                appBarData.rc.Right = workArea.Left + size;
                appBarData.rc.Bottom = workArea.Bottom;
                break;

            case AppBarEdge.Top:
                appBarData.rc.Left = workArea.Left;
                appBarData.rc.Top = workArea.Top;
                appBarData.rc.Right = workArea.Right;
                appBarData.rc.Bottom = workArea.Top + size;
                break;

            case AppBarEdge.Right:
                appBarData.rc.Left = workArea.Right - size;
                appBarData.rc.Top = workArea.Top;
                appBarData.rc.Right = workArea.Right;
                appBarData.rc.Bottom = workArea.Bottom;
                break;

            case AppBarEdge.Bottom:
                appBarData.rc.Left = workArea.Left;
                appBarData.rc.Top = workArea.Bottom - size;
                appBarData.rc.Right = workArea.Right;
                appBarData.rc.Bottom = workArea.Bottom;
                break;

            default:
                throw new ArgumentException($"Invalid AppBar edge: {edge}", nameof(edge));
        }

        // 步骤 4: 查询系统建议的位置（ABM_QUERYPOS）
        AppBar互操作.SHAppBarMessage(AppBar互操作.ABM_QUERYPOS, ref appBarData);

        // 步骤 5: 根据边缘重新调整矩形（系统可能修改了某些边）
        switch (edge)
        {
            case AppBarEdge.Left:
                appBarData.rc.Right = appBarData.rc.Left + size;
                break;

            case AppBarEdge.Top:
                appBarData.rc.Bottom = appBarData.rc.Top + size;
                break;

            case AppBarEdge.Right:
                appBarData.rc.Left = appBarData.rc.Right - size;
                break;

            case AppBarEdge.Bottom:
                appBarData.rc.Top = appBarData.rc.Bottom - size;
                break;
        }

        // 步骤 6: 正式设置 AppBar 位置（ABM_SETPOS）
        AppBar互操作.SHAppBarMessage(AppBar互操作.ABM_SETPOS, ref appBarData);

        // 返回最终的矩形区域
        return (appBarData.rc.Left, appBarData.rc.Top, appBarData.rc.Right, appBarData.rc.Bottom);
    }

    /// <summary>
    /// 取消 AppBar 注册
    /// 使用 SHAppBarMessage(ABM_REMOVE, ...)
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    public static void UnregisterAppBar(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        // 创建 APPBARDATA 结构
        var appBarData = new AppBar互操作.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<AppBar互操作.APPBARDATA>(),
            hWnd = hwnd
        };

        // 调用 ABM_REMOVE 取消注册
        AppBar互操作.SHAppBarMessage(AppBar互操作.ABM_REMOVE, ref appBarData);
    }

    // ===== 动画系统支持方法 =====

    /// <summary>
    /// 设置窗口位置和尺寸（一次性设置）
    /// 用于动画系统实时更新窗口边界
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="x">X 坐标</param>
    /// <param name="y">Y 坐标</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    public static void SetWindowBounds(IntPtr hwnd, int x, int y, int width, int height)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        if (width <= 0)
            throw new ArgumentException("Width must be positive", nameof(width));

        if (height <= 0)
            throw new ArgumentException("Height must be positive", nameof(height));

        // 使用 SetWindowPos 同时设置位置和尺寸
        if (!窗口位置互操作.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            x,
            y,
            width,
            height,
            窗口位置互操作.SWP_NOZORDER | 窗口位置互操作.SWP_NOACTIVATE))
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to set window bounds. Error code: {error}");
        }
    }

    /// <summary>
    /// 设置窗口圆角半径
    /// 注意：DWM API 不支持设置精确的圆角半径，只能设置圆角偏好
    /// 这里根据半径值选择合适的圆角偏好
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="radius">圆角半径（像素）</param>
    public static void SetCornerRadius(IntPtr hwnd, int radius)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        // 根据半径值选择圆角偏好
        DWM互操作.DWM_WINDOW_CORNER_PREFERENCE preference;
        
        if (radius <= 0)
        {
            // 无圆角
            preference = DWM互操作.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
        }
        else if (radius < 8)
        {
            // 小圆角
            preference = DWM互操作.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUNDSMALL;
        }
        else
        {
            // 默认圆角
            preference = DWM互操作.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
        }

        SetCornerPreference(hwnd, preference);
    }

    /// <summary>
    /// 设置窗口不透明度
    /// 使用 SetLayeredWindowAttributes 实现
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="opacity">不透明度（0.0 - 1.0）</param>
    public static void SetOpacity(IntPtr hwnd, double opacity)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        if (opacity < 0.0 || opacity > 1.0)
            throw new ArgumentException("Opacity must be between 0.0 and 1.0", nameof(opacity));

        // 确保窗口有 WS_EX_LAYERED 样式
        int exStyle = 窗口样式互操作.GetWindowLong(hwnd, 窗口样式互操作.GWL_EXSTYLE);
        if ((exStyle & 窗口样式互操作.WS_EX_LAYERED) == 0)
        {
            exStyle |= 窗口样式互操作.WS_EX_LAYERED;
            窗口样式互操作.SetWindowLong(hwnd, 窗口样式互操作.GWL_EXSTYLE, exStyle);
        }

        // 将 0.0-1.0 转换为 0-255
        byte alpha = (byte)(opacity * 255);

        // 调用 SetLayeredWindowAttributes
        if (!窗口样式互操作.SetLayeredWindowAttributes(hwnd, 0, alpha, 窗口样式互操作.LWA_ALPHA))
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to set window opacity. Error code: {error}");
        }
    }
}
