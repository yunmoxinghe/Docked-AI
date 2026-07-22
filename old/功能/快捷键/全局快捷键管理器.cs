// 引入系统基础类型和异常处理
using System;
// 引入 NHotkey 库，用于注册全局快捷键
using NHotkey.WinUI;
// 引入虚拟键修饰符枚举（Ctrl、Alt、Shift、Win）
using Windows.System;
// 引入设置页面类，用于监听快捷键设置变化
using Docked_AI.Features.Pages.Settings;

namespace Docked_AI.Features.Hotkey
{
    /// <summary>
    /// 全局快捷键管理器
    /// 负责注册、更新和管理系统级全局快捷键
    /// 使用 NHotkey.WinUI 库实现跨应用程序的快捷键监听
    /// </summary>
    public class GlobalHotkeyManager : IDisposable
    {
        // 快捷键被按下时的回调函数
        private readonly Action? _hotkeyCallback;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="hotkeyCallback">快捷键被按下时的回调函数</param>
        public GlobalHotkeyManager(Action? hotkeyCallback = null)
        {
            // 保存回调函数
            _hotkeyCallback = hotkeyCallback;

            // 订阅快捷键设置变化事件，当用户修改快捷键设置时会触发
            SettingsPage.HotkeySettingsChanged += OnHotkeySettingsChanged;
        }

        /// <summary>
        /// 初始化全局快捷键
        /// 在应用程序启动时调用
        /// </summary>
        public void Initialize()
        {
            try
            {
                // 注册快捷键
                RegisterHotkey();
            }
            catch (Exception ex)
            {
                // 如果注册失败，输出调试信息
                System.Diagnostics.Debug.WriteLine($"[GlobalHotkeyManager] Failed to initialize global hotkey: {ex}");
            }
        }

        /// <summary>
        /// 注册或更新全局快捷键
        /// 根据用户设置注册快捷键，如果已存在则更新
        /// </summary>
        private void RegisterHotkey()
        {
            try
            {
                // 创建快捷键设置对象，从本地存储加载用户配置
                var settings = new HotkeySettings();

                // 如果快捷键功能未启用
                if (!settings.IsEnabled)
                {
                    // 尝试移除已注册的快捷键
                    try
                    {
                        HotkeyManager.Current.Remove("ShowHideWindow");
                    }
                    catch { } // 忽略移除失败的异常
                    return; // 直接返回，不注册快捷键
                }

                // 构建修饰键组合（初始为无修饰键）
                VirtualKeyModifiers modifiers = VirtualKeyModifiers.None;
                // 如果启用了 Ctrl 键，添加 Control 修饰符
                if (settings.Ctrl) modifiers |= VirtualKeyModifiers.Control;
                // 如果启用了 Alt 键，添加 Menu 修饰符
                if (settings.Alt) modifiers |= VirtualKeyModifiers.Menu;
                // 如果启用了 Shift 键，添加 Shift 修饰符
                if (settings.Shift) modifiers |= VirtualKeyModifiers.Shift;
                // 如果启用了 Win 键，添加 Windows 修饰符
                if (settings.Win) modifiers |= VirtualKeyModifiers.Windows;

                // 使用 NHotkey 注册全局快捷键
                // 参数：快捷键名称、按键、修饰键组合、触发时的回调函数
                HotkeyManager.Current.AddOrReplace(
                    "ShowHideWindow",        // 快捷键的唯一标识符
                    settings.Key,            // 主按键（如 VirtualKey.Space）
                    modifiers,               // 修饰键组合（Ctrl、Alt、Shift、Win）
                    OnGlobalHotkeyPressed);  // 快捷键被按下时的回调函数

                // 输出调试信息，显示已注册的快捷键组合
                System.Diagnostics.Debug.WriteLine($"[GlobalHotkeyManager] Global hotkey registered: {settings.GetDisplayText()}");
            }
            catch (Exception ex)
            {
                // 如果注册失败，输出错误信息
                System.Diagnostics.Debug.WriteLine($"[GlobalHotkeyManager] Error registering hotkey: {ex}");
            }
        }

        /// <summary>
        /// 快捷键设置变化时的事件处理函数
        /// 当用户在设置页面修改快捷键配置时触发
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnHotkeySettingsChanged(object? sender, EventArgs e)
        {
            // 重新注册快捷键，应用新的设置
            RegisterHotkey();
        }

        /// <summary>
        /// 全局快捷键被按下时的回调函数
        /// 由 NHotkey 库在检测到快捷键按下时调用
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">快捷键事件参数</param>
        private void OnGlobalHotkeyPressed(object? sender, NHotkey.HotkeyEventArgs e)
        {
            // 输出调试信息
            System.Diagnostics.Debug.WriteLine("[GlobalHotkeyManager] Global hotkey pressed!");
            // 标记事件已处理，防止其他程序接收此快捷键
            e.Handled = true;
            // 调用回调函数（通常是显示/隐藏主窗口）
            _hotkeyCallback?.Invoke();
        }

        /// <summary>
        /// 释放资源（实现 IDisposable 接口）
        /// 在对象被垃圾回收前调用，确保资源正确释放
        /// </summary>
        public void Dispose()
        {
            // 取消订阅快捷键设置变化事件
            SettingsPage.HotkeySettingsChanged -= OnHotkeySettingsChanged;

            // 移除全局快捷键
            try
            {
                HotkeyManager.Current.Remove("ShowHideWindow");
            }
            catch { } // 忽略移除失败的异常
        }
    }
}

