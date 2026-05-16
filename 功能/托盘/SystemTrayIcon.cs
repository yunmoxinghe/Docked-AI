using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
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

                switch (msg)
                {
                    case WM_LBUTTONDOWN:
                        LeftClick?.Invoke(this, args);
                        break;
                    case WM_RBUTTONDOWN:
                        RightClick?.Invoke(this, args);
                        break;
                }

                if (args.Flyout != null)
                    ShowFlyout(args.Flyout);
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private void ShowFlyout(FlyoutBase flyout)
        {
            flyout.ShouldConstrainToRootBounds = false;
            var grid = (Microsoft.UI.Xaml.Controls.Grid)_hiddenWindow.Content;
            grid.ContextFlyout = flyout;

            flyout.Closed += OnFlyoutClosed;
            _hiddenWindow.Activate();
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
                    Placement = FlyoutPlacementMode.Bottom
                });
            }
            else
            {
                flyout.ShowAt(grid, new FlyoutShowOptions
                {
                    Placement = FlyoutPlacementMode.Bottom
                });
            }
        }

        private void OnFlyoutClosed(object? sender, object e)
        {
            if (sender is FlyoutBase flyout)
            {
                flyout.Closed -= OnFlyoutClosed;
                ShowWindow(_hWnd, 0);
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
        private const uint WM_RBUTTONDOWN = 0x0204;
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

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;

        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData);
    }

    public delegate void TypedEventHandler<TSender, TArgs>(TSender sender, TArgs args);
}
