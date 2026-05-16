using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Windowing;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;

namespace Docked_AI.Features.Tray
{
    public class SystemTrayIcon : IDisposable
    {
        private const uint WM_APP = 0x8000;
        private const uint TRAY_CALLBACK = WM_APP + 100;
        private const uint WM_CONTEXTMENU = 0x007B;  // 🛠️ 添加 WM_CONTEXTMENU

        private readonly Window _hiddenWindow;
        private readonly uint _iconId;
        private IntPtr _hWnd;
        private bool _isVisible;
        private string _tooltip = "";
        private bool _disposed;
        private IntPtr _hIcon;
        private GCHandle _gcHandle;
        private SUBCLASSPROC _subclassDelegate;

        public SystemTrayIcon(uint trayIconId, string iconPath, string tooltip)
        {
            _iconId = trayIconId;
            _tooltip = tooltip ?? "";

            _hiddenWindow = new Window();
            _hiddenWindow.Content = new Microsoft.UI.Xaml.Controls.Grid();
            _hiddenWindow.AppWindow.IsShownInSwitchers = false;
            if (_hiddenWindow.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter overlappedPresenter)
                overlappedPresenter.IsAlwaysOnTop = true;
            _hWnd = WindowNative.GetWindowHandle(_hiddenWindow);

            SetWindowLongPtr(_hWnd, GWL_STYLE, WS_POPUP);

            _gcHandle = GCHandle.Alloc(this, GCHandleType.Weak);
            _subclassDelegate = WndProc;
            var fnPtr = Marshal.GetFunctionPointerForDelegate(_subclassDelegate);
            SetWindowSubclass(_hWnd, fnPtr, 102, (nuint)GCHandle.ToIntPtr(_gcHandle));

            SetWindowPos(_hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOZORDER);
            LoadIcon(iconPath);
        }

        private void LoadIcon(string iconPath)
        {
            var p = Marshal.StringToHGlobalUni(iconPath);
            try
            {
                var dpi = GetDpiForWindow(_hWnd);
                int size = (int)(dpi / 6d);
                _hIcon = LoadImage(IntPtr.Zero, p, 1, size, size, 0x0010);
                if (_hIcon == IntPtr.Zero)
                    throw new ArgumentException($"Failed to load icon from {iconPath}");
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }

        ~SystemTrayIcon() { Dispose(false); }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;
            if (disposing)
            {
                _hiddenWindow.Close();
            }
            RemoveFromTray();
            if (_hIcon != IntPtr.Zero)
                DestroyIcon(_hIcon);
        }

        public uint TrayIconId => _iconId;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_disposed) throw new ObjectDisposedException(nameof(SystemTrayIcon));
                _isVisible = value;
                if (_isVisible) AddToTray();
                else RemoveFromTray();
            }
        }

        public string Tooltip
        {
            get => _tooltip;
            set
            {
                if (_disposed) throw new ObjectDisposedException(nameof(SystemTrayIcon));
                _tooltip = value ?? "";
                if (_isVisible) UpdateTray(NIM_MODIFY);
            }
        }

        public event TypedEventHandler<SystemTrayIcon, SystemTrayIconEventArgs>? LeftClick;
        public event TypedEventHandler<SystemTrayIcon, SystemTrayIconEventArgs>? RightClick;

        private void AddToTray()
        {
            var data = CreateNotifyIconData(NIM_ADD);
            Shell_NotifyIconW(NIM_ADD, ref data);
            Shell_NotifyIconW(NIM_SETVERSION, ref data);
        }

        private void RemoveFromTray()
        {
            var data = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = _hWnd,
                uID = _iconId
            };
            Shell_NotifyIconW(NIM_DELETE, ref data);
        }

        private void UpdateTray(uint message)
        {
            var data = CreateNotifyIconData(message);
            Shell_NotifyIconW(message, ref data);
        }

        private NOTIFYICONDATAW CreateNotifyIconData(uint message)
        {
            var data = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = _hWnd,
                uID = _iconId,
                uFlags = NIF_ICON,
                hIcon = _hIcon,
                szTip = new ushort[128],
                szInfo = new ushort[256],
                szInfoTitle = new ushort[64],
            };

            var tip = _tooltip;
            if (!string.IsNullOrEmpty(tip))
            {
                data.uFlags |= NIF_TIP | NIF_SHOWTIP;
                for (int i = 0; i < tip.Length && i < 128; i++)
                    data.szTip[i] = (ushort)tip[i];
            }

            if (message == NIM_ADD || message == NIM_SETVERSION)
            {
                data.uFlags |= NIF_MESSAGE;
                data.uCallbackMessage = TRAY_CALLBACK;
            }

            if (message == NIM_ADD || message == NIM_SETVERSION)
                data.VersionOrTimeout = 4;

            return data;
        }

        private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData)
        {
            if (uMsg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                mmi.ptMinTrackSize = new POINT(0, 0);
                Marshal.StructureToPtr(mmi, lParam, false);
                return IntPtr.Zero;
            }

            if (uMsg == TRAY_CALLBACK)
            {
                var msg = (uint)(lParam.ToInt32() & 0xffff);
                var args = new SystemTrayIconEventArgs();

                // 🔍 检测输入设备类型
                args.InputDevice = DetectInputDeviceType();

                switch (msg)
                {
                    case WM_LBUTTONUP:  // 🛠️ 改为 UP，在释放时触发
                        LeftClick?.Invoke(this, args);
                        break;
                    case WM_CONTEXTMENU:  // 🛠️ 使用 WM_CONTEXTMENU 而不是 WM_RBUTTONDOWN
                        RightClick?.Invoke(this, args);
                        break;
                }

                if (args.Flyout != null)
                    ShowFlyout(args.Flyout);
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        /// <summary>
        /// 检测当前输入设备类型
        /// </summary>
        /// <returns>输入设备类型</returns>
        private InputDeviceType DetectInputDeviceType()
        {
            // 🔍 检查 GetMessageExtraInfo 的触摸标志
            var extraInfo = GetMessageExtraInfo();
            const long MOUSEEVENTF_FROMTOUCH = 0xFF515700;
            const long SIGNATURE_MASK = 0xFFFFFF00;
            var isFromTouch = (extraInfo.ToInt64() & SIGNATURE_MASK) == MOUSEEVENTF_FROMTOUCH;
            
            // 🔍 检查系统是否有鼠标
            var mousePresent = GetSystemMetrics(SM_MOUSEPRESENT);
            
            System.Diagnostics.Debug.WriteLine($"[SystemTrayIcon] Input detection: MousePresent={mousePresent}, FromTouch={isFromTouch}, ExtraInfo=0x{extraInfo.ToInt64():X}");
            
            // 🎯 简化决策逻辑：默认鼠标，只有明确检测到触摸才用触摸模式
            if (isFromTouch)
            {
                System.Diagnostics.Debug.WriteLine("[SystemTrayIcon] ✅ Detected: Touch (from message extra info)");
                return InputDeviceType.Touch;
            }
            
            // 默认返回鼠标类型（即使是触摸设备，如果有鼠标也优先使用鼠标模式）
            System.Diagnostics.Debug.WriteLine("[SystemTrayIcon] ✅ Detected: Mouse (default)");
            return InputDeviceType.Mouse;
        }

        private void ShowFlyout(FlyoutBase flyout)
        {
            flyout.ShouldConstrainToRootBounds = false;
            var grid = (Microsoft.UI.Xaml.Controls.Grid)_hiddenWindow.Content;
            grid.ContextFlyout = flyout;

            flyout.Closed += OnFlyoutClosed;
            
            // 🔧 关键修复：在显示菜单前模拟鼠标移动，强制 WinUI 切换到鼠标模式
            // 获取当前鼠标位置
            if (GetCursorPos(out var cursorPos))
            {
                // 模拟一个微小的鼠标移动（移动1像素再移回）
                // 这会触发 WinUI 的输入模式检测，切换到鼠标布局
                SetCursorPos(cursorPos.X + 1, cursorPos.Y);
                SetCursorPos(cursorPos.X, cursorPos.Y);
            }
            
            // 🛠️ 关键修复：调整顺序，先显示窗口再设置前台
            // 删除 _hiddenWindow.Activate()，避免触发 WinUI 的 touch 输入模式初始化
            ShowWindow(_hWnd, SW_SHOW);
            SetForegroundWindow(_hWnd);

            var iconId = new NOTIFYICONIDENTIFIER
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
                hWnd = _hWnd,
                uID = _iconId
            };

            if (Shell_NotifyIconGetRect(ref iconId, out var iconRect) == 0)
            {
                int cx = (iconRect.left + iconRect.right) / 2;
                
                // 给窗口一个最小尺寸 1x1，避免尺寸为 0
                _hiddenWindow.AppWindow.MoveAndResize(
                    new RectInt32(cx, iconRect.top, 1, 1),
                    DisplayArea.GetFromPoint(new PointInt32(cx, iconRect.top), DisplayAreaFallback.Primary));

                // 不设置 Position，让 Flyout 自动居中对齐到窗口
                flyout.ShowAt(grid, new FlyoutShowOptions
                {
                    Placement = FlyoutPlacementMode.Bottom,
                    ShowMode = FlyoutShowMode.Standard  // 🔧 强制使用标准模式
                });
            }
            else
            {
                flyout.ShowAt(grid, new FlyoutShowOptions
                {
                    Placement = FlyoutPlacementMode.Bottom,
                    ShowMode = FlyoutShowMode.Standard  // 🔧 强制使用标准模式
                });
            }
        }

        private void OnFlyoutClosed(object? sender, object e)
        {
            if (sender is FlyoutBase flyout)
            {
                flyout.Closed -= OnFlyoutClosed;
                ShowWindow(_hWnd, 0);
                
                // 🛠️ 经典托盘菜单修复：菜单关闭后发送 WM_NULL 消息
                // 确保焦点正确释放，避免菜单不消失或焦点异常
                PostMessage(_hWnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private const int GWL_STYLE = -16;
        private const uint WS_POPUP = 0x80000000;
        private const uint NIM_ADD = 0;
        private const uint NIM_MODIFY = 1;
        private const uint NIM_DELETE = 2;
        private const uint NIM_SETVERSION = 4;
        private const uint NIF_MESSAGE = 0x0001;
        private const uint NIF_ICON = 0x0002;
        private const uint NIF_TIP = 0x0004;
        private const uint NIF_SHOWTIP = 0x0080;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;  // 🛠️ 添加 WM_LBUTTONUP
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;  // 🛠️ 添加 WM_RBUTTONUP
        private const uint WM_NULL = 0x0000;  // 🛠️ 添加 WM_NULL
        private const int SW_SHOW = 5;
        private const uint WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
            public POINT(int x, int y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATAW
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public ushort[] szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] szInfo;
            public uint VersionOrTimeout;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public ushort[] szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

        [DllImport("shell32.dll")]
        private static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);

        [StructLayout(LayoutKind.Sequential)]
        private struct NOTIFYICONIDENTIFIER
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public Guid guidItem;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SetWindowLongPtr(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("comctl32.dll")]
        private static extern bool SetWindowSubclass(IntPtr hWnd, IntPtr pfnSubclass, nuint uIdSubclass, nuint dwRefData);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImage(IntPtr hInst, IntPtr name, uint type, int cx, int cy, uint fuLoad);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        private const int SM_DIGITIZER = 94;
        private const int SM_MAXIMUMTOUCHES = 95;
        private const int SM_MOUSEPRESENT = 19;
        
        // Digitizer 标志位
        private const int NID_INTEGRATED_TOUCH = 0x01;
        private const int NID_EXTERNAL_TOUCH = 0x02;
        private const int NID_INTEGRATED_PEN = 0x04;
        private const int NID_EXTERNAL_PEN = 0x08;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;

        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData);
    }

    public delegate void TypedEventHandler<TSender, TArgs>(TSender sender, TArgs args);
}
