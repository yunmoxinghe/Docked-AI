using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace NHotkey.WinUI
{
    public partial class HotkeyManager : HotkeyManagerBase
    {
        public static HotkeyManager Current { get; } = new HotkeyManager();

        private readonly Window _hiddenWindow;
        private readonly SUBCLASSPROC _subclassDelegate;
        private GCHandle _gcHandle;

        private HotkeyManager()
        {
            _hiddenWindow = new Window();
            _hiddenWindow.Content = new Microsoft.UI.Xaml.Controls.Grid();
            _hiddenWindow.AppWindow.IsShownInSwitchers = false;

            var hwnd = WindowNative.GetWindowHandle(_hiddenWindow);
            SetWindowLongPtr(hwnd, GWL_STYLE, unchecked((nint)WS_POPUP));
            SetHwnd(hwnd);

            _gcHandle = GCHandle.Alloc(this, GCHandleType.Weak);
            _subclassDelegate = WindowProc;
            var funcPointer = Marshal.GetFunctionPointerForDelegate(_subclassDelegate);
            SetWindowSubclass(hwnd, funcPointer, 101, (nuint)GCHandle.ToIntPtr(_gcHandle));
        }

        public void AddOrReplace(string name, Windows.System.VirtualKey key, Windows.System.VirtualKeyModifiers modifiers, EventHandler<HotkeyEventArgs> handler)
        {
            var flags = ConvertModifiers(modifiers);
            var vk = (uint)key;
            AddOrReplace(name, vk, flags, handler);
        }

        private static HotkeyFlags ConvertModifiers(Windows.System.VirtualKeyModifiers modifiers)
        {
            var flags = HotkeyFlags.None;
            if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control))
                flags |= HotkeyFlags.Control;
            if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Menu))
                flags |= HotkeyFlags.Alt;
            if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift))
                flags |= HotkeyFlags.Shift;
            if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Windows))
                flags |= HotkeyFlags.Windows;
            return flags;
        }

        private IntPtr WindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData)
        {
            var handle = GCHandle.FromIntPtr((IntPtr)(nint)dwRefData);
            if (handle.IsAllocated && handle.Target is HotkeyManager manager)
            {
                bool handled = false;
                manager.HandleHotkeyMessage(hWnd, (int)uMsg, wParam, lParam, ref handled, out _);
                if (handled)
                    return (IntPtr)0;
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private const int GWL_STYLE = -16;
        private const uint WS_POPUP = 0x80000000;

        // 🔧 x64 平台使用 SetWindowLongPtrW（Unicode 版本）
        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

        [LibraryImport("comctl32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowSubclass(IntPtr hWnd, IntPtr pfnSubclass, nuint uIdSubclass, nuint dwRefData);

        [LibraryImport("comctl32.dll", SetLastError = true)]
        private static partial IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData);
    }
}
