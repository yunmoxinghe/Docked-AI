using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.System;

namespace Docked_AI.Features.Hotkey
{
    /// <summary>
    /// 全局快捷键管理器，用于注册和管理系统级快捷键
    /// </summary>
    public class GlobalHotkeyManager : IDisposable
    {
        // Win32 API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // 修饰键常量
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        private const int HOTKEY_ID = 9000;
        private const int GWL_WNDPROC = -4;
        private const uint WM_HOTKEY = 0x0312;

        private IntPtr _windowHandle;
        private bool _isRegistered;
        private Action? _hotkeyCallback;
        private WndProcDelegate? _wndProcDelegate;
        private IntPtr _oldWndProc;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public GlobalHotkeyManager()
        {
        }

        /// <summary>
        /// 注册全局快捷键
        /// </summary>
        public bool RegisterHotkey(Window window, VirtualKey key, bool ctrl, bool alt, bool shift, bool win, Action callback)
        {
            try
            {
                _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                _hotkeyCallback = callback;

                // 卸载已存在的快捷键
                if (_isRegistered)
                {
                    UnregisterHotkey();
                }

                uint modifiers = 0;
                if (ctrl) modifiers |= MOD_CONTROL;
                if (alt) modifiers |= MOD_ALT;
                if (shift) modifiers |= MOD_SHIFT;
                if (win) modifiers |= MOD_WIN;

                _isRegistered = RegisterHotKey(_windowHandle, HOTKEY_ID, modifiers, (uint)key);

                if (_isRegistered)
                {
                    // 订阅窗口消息
                    SubscribeToWindowMessages();
                }

                return _isRegistered;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlobalHotkeyManager] Failed to register hotkey: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 卸载全局快捷键
        /// </summary>
        public void UnregisterHotkey()
        {
            if (_isRegistered && _windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
                _isRegistered = false;
            }
        }

        private void SubscribeToWindowMessages()
        {
            _wndProcDelegate = WndProc;
            IntPtr newWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            _oldWndProc = SetWindowLongPtr(_windowHandle, GWL_WNDPROC, newWndProc);
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _hotkeyCallback?.Invoke();
                return IntPtr.Zero;
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
            UnregisterHotkey();
        }
    }
}

