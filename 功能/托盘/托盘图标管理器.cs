// 引入系统基础类型和异常处理
using System;
// 引入文件路径操作
using System.IO;
// 引入 DevWinUI 库，提供系统托盘图标功能
using DevWinUI;
// 引入 WinUI 窗口类型
using Microsoft.UI.Xaml;
// 引入 WinUI 控件（菜单、图标等）
using Microsoft.UI.Xaml.Controls;
// 引入本地化辅助类，用于多语言支持
using Docked_AI.Features.Localization;
// 引入全局快捷键管理器
using Docked_AI.Features.Hotkey;

namespace Docked_AI.Features.Tray
{
    /// <summary>
    /// 托盘图标管理器类
    /// 负责管理系统托盘图标、右键菜单和主窗口的显示/隐藏
    /// 实现 IDisposable 接口以正确释放资源
    /// </summary>
    public class TrayIconManager : IDisposable
    {
        // 系统托盘图标对象，可为空
        private SystemTrayIcon? _trayIcon;
        // 主窗口引用，可为空
        private Window? _mainWindow;
        // 退出应用程序时的回调函数，可为空
        private readonly Action? _exitAction;
        // 全局快捷键管理器，负责处理快捷键注册和监听
        private readonly GlobalHotkeyManager? _hotkeyManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="initialMainWindow">初始主窗口引用</param>
        /// <param name="exitAction">退出应用程序时的回调函数</param>
        public TrayIconManager(Window? initialMainWindow, Action? exitAction = null)
        {
            // 保存主窗口引用
            _mainWindow = initialMainWindow;
            // 保存退出回调函数
            _exitAction = exitAction;

            // 创建全局快捷键管理器，传入显示主窗口的回调函数
            _hotkeyManager = new GlobalHotkeyManager(ShowMainWindow);
        }

        /// <summary>
        /// 初始化托盘图标和全局快捷键
        /// </summary>
        public void Initialize()
        {
            // 设置托盘图标的唯一标识符
            uint iconId = 123;
            // 构建图标文件的完整路径（应用程序目录/Assets/Sparkles.ico）
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Sparkles.ico");
            // 创建系统托盘图标对象，参数：图标ID、图标路径、鼠标悬停提示文本
            _trayIcon = new SystemTrayIcon(iconId, iconPath, "Docked AI");

            // 订阅托盘图标的左键点击事件
            _trayIcon.LeftClick += TrayIcon_LeftClick;
            // 订阅托盘图标的右键点击事件
            _trayIcon.RightClick += TrayIcon_RightClick;
            // 设置托盘图标为可见状态
            _trayIcon.IsVisible = true;

            // 初始化全局快捷键
            _hotkeyManager?.Initialize();
        }

        /// <summary>
        /// 托盘图标左键点击事件处理函数
        /// </summary>
        /// <param name="sender">托盘图标对象</param>
        /// <param name="args">事件参数</param>
        private void TrayIcon_LeftClick(SystemTrayIcon sender, SystemTrayIconEventArgs args)
        {
            // 显示主窗口
            ShowMainWindow();
        }

        /// <summary>
        /// 托盘图标右键点击事件处理函数
        /// </summary>
        /// <param name="sender">托盘图标对象</param>
        /// <param name="args">事件参数</param>
        private void TrayIcon_RightClick(SystemTrayIcon sender, SystemTrayIconEventArgs args)
        {
            // 创建弹出菜单
            var flyout = new MenuFlyout();

            // 创建"打开主窗口"菜单项
            var openItem = new MenuFlyoutItem
            {
                // 从本地化资源获取菜单文本
                Text = LocalizationHelper.GetString("TrayMenu_OpenWindow"),
                // 设置菜单图标（开始符号）
                Icon = new SymbolIcon(Symbol.GoToStart)
            };
            // 绑定点击事件，点击时显示主窗口
            openItem.Click += (s, e) => ShowMainWindow();
            // 将菜单项添加到弹出菜单
            flyout.Items.Add(openItem);

            // 添加分隔线
            flyout.Items.Add(new MenuFlyoutSeparator());

            // 创建"退出"菜单项
            var exitItem = new MenuFlyoutItem
            {
                // 从本地化资源获取菜单文本
                Text = LocalizationHelper.GetString("TrayMenu_Exit"),
                // 设置菜单图标（关闭符号，Unicode 字符）
                Icon = new FontIcon { Glyph = "\uF3B1" }
            };
            // 绑定点击事件，点击时退出应用程序
            exitItem.Click += (s, e) => ExitApplication();
            // 将菜单项添加到弹出菜单
            flyout.Items.Add(exitItem);

            // 将弹出菜单赋值给事件参数，系统会自动显示菜单
            args.Flyout = flyout;
        }

        /// <summary>
        /// 显示主窗口
        /// 如果窗口不存在或已关闭，则创建新窗口
        /// 如果窗口已存在，则切换显示/隐藏状态
        /// </summary>
        public void ShowMainWindow()
        {
            try
            {
                // 如果主窗口引用不为空
                if (_mainWindow != null)
                {
                    // 获取窗口的原生句柄
                    var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
                    // 如果句柄无效（窗口已关闭），清空引用
                    if (windowHandle == IntPtr.Zero)
                    {
                        _mainWindow = null;
                    }
                }

                // 如果主窗口不存在或内容为空（窗口已关闭）
                if (_mainWindow == null || _mainWindow.Content == null)
                {
                    // 创建新的主窗口实例
                    _mainWindow = new global::Docked_AI.MainWindow();
                    // 激活窗口（显示并获得焦点）
                    _mainWindow.Activate();
                    // 将窗口置于前台
                    WindowHelper.SetForegroundWindow(_mainWindow);
                }
                else
                {
                    // 如果窗口已存在，尝试转换为 MainWindow 类型
                    if (_mainWindow is global::Docked_AI.MainWindow mainWindow)
                    {
                        // 切换窗口的显示/隐藏状态
                        mainWindow.ToggleWindow();
                        // 如果窗口现在是可见状态
                        if (mainWindow.IsWindowVisible)
                        {
                            // 将窗口置于前台
                            WindowHelper.SetForegroundWindow(_mainWindow);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果发生异常，输出错误信息
                System.Diagnostics.Debug.WriteLine($"Error showing main window: {ex.Message}");
                // 清空窗口引用
                _mainWindow = null;
                // 创建新的主窗口实例
                _mainWindow = new global::Docked_AI.MainWindow();
                // 激活窗口
                _mainWindow.Activate();
                // 将窗口置于前台
                WindowHelper.SetForegroundWindow(_mainWindow);
            }
        }

        /// <summary>
        /// 退出应用程序
        /// 清理所有资源并调用退出回调
        /// </summary>
        public void ExitApplication()
        {
            // 释放快捷键管理器资源
            _hotkeyManager?.Dispose();

            // 如果托盘图标存在
            if (_trayIcon != null)
            {
                // 取消订阅左键点击事件
                _trayIcon.LeftClick -= TrayIcon_LeftClick;
                // 取消订阅右键点击事件
                _trayIcon.RightClick -= TrayIcon_RightClick;
                // 隐藏托盘图标
                _trayIcon.IsVisible = false;
                // 释放托盘图标资源
                _trayIcon.Dispose();
                // 清空托盘图标引用
                _trayIcon = null;
            }

            // 如果退出回调函数存在，则调用它
            _exitAction?.Invoke();
        }

        /// <summary>
        /// 释放资源（实现 IDisposable 接口）
        /// 在对象被垃圾回收前调用，确保资源正确释放
        /// </summary>
        public void Dispose()
        {
            // 释放快捷键管理器资源
            _hotkeyManager?.Dispose();

            // 如果托盘图标存在
            if (_trayIcon != null)
            {
                // 取消订阅左键点击事件
                _trayIcon.LeftClick -= TrayIcon_LeftClick;
                // 取消订阅右键点击事件
                _trayIcon.RightClick -= TrayIcon_RightClick;
                // 隐藏托盘图标
                _trayIcon.IsVisible = false;
                // 释放托盘图标资源
                _trayIcon.Dispose();
                // 清空托盘图标引用
                _trayIcon = null;
            }
        }
    }
}
