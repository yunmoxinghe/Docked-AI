using Microsoft.UI.Dispatching;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Docked_AI.Features.MainWindow.Status
{
    /// <summary>
    /// 窗口最大化监听服务 - 监听其他应用是否最大化
    /// 
    /// 【文件职责】
    /// 1. 监听系统中前台窗口的变化
    /// 2. 检测其他应用窗口是否处于最大化状态
    /// 3. 通过事件通知订阅者窗口最大化状态的变化
    /// 4. 排除当前应用自身的窗口，只关注其他应用
    /// 
    /// 【核心设计】
    /// 
    /// 为什么需要监听其他应用最大化？
    /// - 当用户最大化其他应用时，边栏助手可能需要调整自身显示效果
    /// - 例如：其他应用最大化时，增加亚克力背景的不透明度，避免内容透过来干扰
    /// - 提升用户体验：边栏助手能感知用户当前关注的应用状态
    /// 
    /// 为什么使用 WinEvent Hook？
    /// - SetWinEventHook 是 Windows API，专门用于监听窗口事件
    /// - EVENT_OBJECT_LOCATIONCHANGE: 监听窗口位置/大小/状态变化（包括最大化/还原）
    /// - 优势：持续后台监听，不依赖焦点变化，任何窗口状态改变都能捕获
    /// - 轻量级、高效、系统级监听机制
    /// - 参考：https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook
    /// 
    /// 为什么需要 DispatcherQueue？
    /// - WinEvent 回调发生在非 UI 线程
    /// - 事件订阅者可能需要更新 UI，必须调度到 UI 线程
    /// - 避免跨线程访问 UI 导致崩溃
    /// 
    /// 【核心逻辑流程】
    /// 
    /// 启动监听流程：
    ///   1. 检查是否已经启动，避免重复启动
    ///   2. 使用 SetWinEventHook 注册 EVENT_OBJECT_LOCATIONCHANGE 事件
    ///   3. 保存 hook 句柄和委托引用（防止被 GC 回收）
    ///   4. 延迟检测当前前台窗口状态（等待焦点稳定）
    ///   5. 输出调试信息确认启动成功
    /// 
    /// 窗口事件处理流程：
    ///   1. 收到 EVENT_OBJECT_LOCATIONCHANGE 事件，获取窗口句柄
    ///   2. 过滤：只处理窗口对象（idObject=0），排除子控件和光标
    ///   3. 检查窗口是否有效（IsWindow）
    ///   4. 排除当前应用窗口（通过进程 ID 判断）
    ///   5. 获取窗口放置信息（GetWindowPlacement）
    ///   6. 判断窗口状态是否为 SW_SHOWMAXIMIZED
    ///   7. 对比上一次状态，如果发生变化则触发事件
    ///   8. 在 UI 线程上触发事件通知订阅者
    /// 
    /// 停止监听流程：
    ///   1. 检查 hook 是否存在
    ///   2. 调用 UnhookWinEvent 取消注册
    ///   3. 清理句柄和委托引用
    ///   4. 输出调试信息确认停止成功
    /// 
    /// 【关键依赖关系】
    /// - SetWinEventHook: 注册窗口事件监听
    /// - UnhookWinEvent: 取消窗口事件监听
    /// - GetWindowPlacement: 获取窗口状态（最大化、最小化、正常）
    /// - GetWindowThreadProcessId: 获取窗口所属进程 ID
    /// - DispatcherQueue: UI 线程调度器
    /// 
    /// 【潜在副作用】
    /// 1. 注册全局 WinEvent Hook（系统级监听）
    /// 2. 在 UI 线程上触发事件（可能阻塞 UI）
    /// 3. 委托必须持有强引用，否则会被 GC 回收导致崩溃
    /// 4. EVENT_OBJECT_LOCATIONCHANGE 触发频率高（包括鼠标移动），需要过滤
    /// 
    /// 【重构风险点】
    /// 1. 委托生命周期管理：
    ///    - _hookDelegate 必须持有强引用
    ///    - 如果被 GC 回收，SetWinEventHook 回调会访问无效内存导致崩溃
    /// 2. 线程安全：
    ///    - _isMaximized 可能在多个线程访问
    ///    - 目前依赖 DispatcherQueue 串行化事件，但仍有风险
    /// 3. 资源泄漏：
    ///    - 必须在 Dispose 中调用 UnhookWinEvent
    ///    - 否则 hook 会一直存在，导致资源泄漏
    /// 4. 性能影响：
    ///    - EVENT_OBJECT_LOCATIONCHANGE 触发非常频繁（窗口移动、调整大小、光标移动等）
    ///    - 需要高效过滤，只处理窗口对象（idObject=0, idChild=0）
    ///    - 频繁事件可能影响性能，但通过过滤可以减轻
    /// 5. Native AOT 兼容性：
    ///    - 不能使用反射
    ///    - 委托必须使用托管方式，AOT 编译器自动生成适配代码
    /// </summary>
    public sealed class WindowMaximizedMonitorService : IDisposable
    {
        // Win32 API 声明
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern long GetWindowLongPtr(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_NOACTIVATE = 0x08000000L;

        // 委托定义
        private delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

        // 结构定义
        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public uint length;
            public uint flags;
            public uint showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        // 常量定义
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B; // 窗口位置/大小/状态变化事件
        private const uint EVENT_OBJECT_DESTROY = 0x8001; // 窗口销毁事件
        private const uint EVENT_OBJECT_SHOW = 0x8002; // 窗口显示事件
        private const uint EVENT_OBJECT_HIDE = 0x8003; // 窗口隐藏事件
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
        private const uint SW_SHOWMAXIMIZED = 3;
        private const int OBJID_WINDOW = 0; // 顶级窗口对象

        // 事件：其他应用最大化状态变化
        public event EventHandler<bool>? OtherAppMaximizedChanged;

        private IntPtr _hookHandle;
        private WinEventDelegate? _hookDelegate; // 必须持有强引用，否则会被 GC 回收
        private bool _isRunning;
        private bool _isMaximized; // 当前是否有最大化窗口
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly int _currentProcessId;
        private System.Threading.Timer? _debounceTimer; // 防抖定时器
        private readonly object _debounceLock = new object();

        /// <summary>
        /// 获取当前是否有任何应用处于最大化状态
        /// </summary>
        public bool IsCurrentlyMaximized => _isMaximized;

        public WindowMaximizedMonitorService(DispatcherQueue dispatcherQueue)
        {
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
            _currentProcessId = Environment.ProcessId;
        }

        /// <summary>
        /// 启动监听
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                System.Diagnostics.Debug.WriteLine("[WindowMaximizedMonitor] Already running");
                return;
            }

            try
            {
                // 创建委托并持有强引用（防止 GC 回收）
                _hookDelegate = new WinEventDelegate(WinEventCallback);

                // 注册 WinEvent Hook：监听多个事件
                _hookHandle = SetWinEventHook(
                    EVENT_OBJECT_SHOW,          // 最小事件
                    EVENT_OBJECT_LOCATIONCHANGE, // 最大事件（包含 SHOW, HIDE, DESTROY, LOCATIONCHANGE）
                    IntPtr.Zero,
                    _hookDelegate,
                    0,
                    0,
                    WINEVENT_OUTOFCONTEXT
                );

                if (_hookHandle == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("[WindowMaximizedMonitor] Failed to set hook");
                    return;
                }

                _isRunning = true;
                System.Diagnostics.Debug.WriteLine("[WindowMaximizedMonitor] Started successfully");

                // 启动后立即扫描一次
                RecalculateMaximizedState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowMaximizedMonitor] Start failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            try
            {
                // 停止防抖定时器
                _debounceTimer?.Dispose();
                _debounceTimer = null;
                
                if (_hookHandle != IntPtr.Zero)
                {
                    UnhookWinEvent(_hookHandle);
                    _hookHandle = IntPtr.Zero;
                }

                _isRunning = false;
                _hookDelegate = null;
                
                System.Diagnostics.Debug.WriteLine("[WindowMaximizedMonitor] Stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowMaximizedMonitor] Stop failed: {ex.Message}");
            }
        }

        /// <summary>
        /// WinEvent 回调函数 - 触发防抖重新扫描
        /// 注意：此函数在非 UI 线程执行
        /// </summary>
        private void WinEventCallback(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint idEventThread,
            uint dwmsEventTime)
        {
            try
            {
                // ⭐ 只处理顶级窗口对象
                if (idObject != OBJID_WINDOW)
                {
                    return;
                }

                // 检查窗口是否有效
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                // 排除当前应用的窗口
                GetWindowThreadProcessId(hwnd, out uint processId);
                if (processId == _currentProcessId)
                {
                    return;
                }

                // 过滤系统 UI 窗口
                if (IsSystemUIWindow(hwnd))
                {
                    return;
                }

                // ⭐ 触发防抖：100ms 后重新扫描
                TriggerDebounceRecalculate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowMaximizedMonitor] Callback exception: {ex.Message}");
            }
        }

        /// <summary>
        /// 触发防抖重新计算（100ms 防抖）
        /// </summary>
        private void TriggerDebounceRecalculate()
        {
            lock (_debounceLock)
            {
                // 重置定时器：如果100ms内又有新事件，重新计时
                _debounceTimer?.Dispose();
                _debounceTimer = new System.Threading.Timer(
                    _ => RecalculateMaximizedState(),
                    null,
                    100, // 100ms 后执行
                    System.Threading.Timeout.Infinite // 只执行一次
                );
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// 判断窗口是否为系统 UI 窗口（需要被排除）
        /// 系统 UI 包括：开始菜单、通知中心、任务视图、输入法弹窗、搜索窗口等
        /// </summary>
        private bool IsSystemUIWindow(IntPtr hwnd)
        {
            try
            {
                // 1. 获取窗口类名
                var className = new System.Text.StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                string classNameStr = className.ToString();

                // 2. 过滤已知的系统 UI 窗口类名
                string[] systemUIClasses = new[]
                {
                    "Windows.UI.Core.CoreWindow",     // Windows 10/11 开始菜单、通知中心
                    // "ApplicationFrameWindow",      // ⭐ 不能过滤！所有 UWP 应用都用这个类名，包括第三方应用
                    "Shell_TrayWnd",                  // 任务栏
                    "Shell_SecondaryTrayWnd",         // 多显示器任务栏
                    "NotifyIconOverflowWindow",       // 托盘溢出窗口
                    "TopLevelWindowForOverflowXamlIsland", // 系统托盘弹窗
                    "Windows.Internal.Shell",         // 系统内部 Shell 窗口
                    "ImmersiveLauncher",              // 开始菜单启动器
                    "ImmersiveSwitchList",            // 任务视图
                    "MultitaskingViewFrame",          // 多任务视图
                    "XamlExplorerHostIslandWindow",   // 文件资源管理器弹窗
                    "Progman",                        // 桌面窗口
                    "WorkerW",                        // 桌面工作区
                    "Windows.UI.Composition.DesktopWindowContentBridge", // 系统 UI 桥接
                    "TextInputHost",                  // 输入法弹窗（Windows 10/11）
                    "IPTip_Main_Window",              // 触摸键盘
                    "MSCTFIME UI",                    // 输入法编辑器
                    "CicMarshalWnd",                  // 输入法消息窗口
                    "IME",                            // 输入法窗口
                    "MSCTFIME",                       // 微软输入法
                    "CiceroUIWndFrame",               // 输入法候选窗口
                    "CicLoaderWndClass",              // 输入法加载器
                    "TrayNotifyWnd",                  // 托盘通知窗口
                    "ApplicationManager_",            // 应用管理器
                    "EdgeUiInputTopWndClass",         // Edge UI 输入窗口
                    "NativeHWNDHost",                 // 原生窗口宿主
                    "XamlIsland",                     // XAML 岛
                    "SearchHost",                     // Windows 搜索
                    "Windows.UI.Input.InputSite.WindowClass", // 输入站点
                };

                foreach (var sysClass in systemUIClasses)
                {
                    if (classNameStr.Contains(sysClass, StringComparison.OrdinalIgnoreCase))
                    {
                        System.Diagnostics.Debug.WriteLine($"[WindowMaximizedMonitor] Filtered system UI: {classNameStr}");
                        return true;
                    }
                }

                // 3. 检查窗口扩展样式，过滤工具窗口和无激活窗口
                // ⭐ 但不过滤可见的顶级窗口（即使有 TOOLWINDOW 样式）
                long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
                
                // ⭐ 只过滤 NOACTIVATE 窗口，不过滤 TOOLWINDOW
                // 因为某些应用（如 Chrome）的主窗口也可能有 TOOLWINDOW 样式
                if ((exStyle & WS_EX_NOACTIVATE) != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[WindowMaximizedMonitor] Filtered no-activate window: {classNameStr}");
                    return true;
                }

                // 4. 检查特殊进程名（Shell 进程、系统进程等）
                // 这里可以根据需要添加更多过滤条件

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowMaximizedMonitor] IsSystemUIWindow exception: {ex.Message}");
                // 出错时保守处理，假设不是系统窗口
                return false;
            }
        }

        /// <summary>
        /// 重新计算最大化状态（通过枚举所有窗口）
        /// ⭐ 不维护 HashSet，每次重新扫描 - 避免脏数据
        /// ⭐ 在后台线程执行，避免阻塞 UI 线程
        /// </summary>
        private void RecalculateMaximizedState()
        {
            // ✅ 在后台线程执行，避免阻塞调用者
            _ = Task.Run(() =>
            {
                try
                {
                    bool hasMaximized = false;

                    // 枚举所有窗口
                    EnumWindows((hwnd, lParam) =>
                    {
                        try
                        {
                            // 检查窗口是否有效且可见
                            if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !IsWindowVisible(hwnd))
                            {
                                return true;
                            }

                            // 排除当前应用的窗口
                            GetWindowThreadProcessId(hwnd, out uint processId);
                            if (processId == _currentProcessId)
                            {
                                return true;
                            }

                            // 过滤系统 UI 窗口
                            if (IsSystemUIWindow(hwnd))
                            {
                                return true;
                            }

                            // 获取窗口状态
                            WINDOWPLACEMENT placement = new()
                            {
                                length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>()
                            };

                            if (!GetWindowPlacement(hwnd, ref placement))
                            {
                                return true;
                            }

                            // 判断是否最大化
                            if (placement.showCmd == SW_SHOWMAXIMIZED)
                            {
                                hasMaximized = true;
                                return false; // 找到一个就够了，停止枚举
                            }
                        }
                        catch
                        {
                            // 忽略单个窗口的错误
                        }

                        return true; // 继续枚举
                    }, IntPtr.Zero);

                    // 检查状态是否变化
                    if (_isMaximized != hasMaximized)
                    {
                        _isMaximized = hasMaximized;
                        System.Diagnostics.Debug.WriteLine($"[WindowMaximizedMonitor] State changed: {(hasMaximized ? "MAXIMIZED" : "NOT MAXIMIZED")}");

                        // ✅ 使用 Low 优先级，不阻塞 UI 线程的关键操作
                        _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                        {
                            try
                            {
                                OtherAppMaximizedChanged?.Invoke(this, hasMaximized);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[WindowMaximizedMonitor] Event handler exception: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WindowMaximizedMonitor] RecalculateMaximizedState failed: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 手动触发检测（供外部调用，例如窗口进入固定模式时）
        /// </summary>
        public void RefreshCurrentState()
        {
            if (!_isRunning)
            {
                return;
            }

            RecalculateMaximizedState();
        }
    }
}
