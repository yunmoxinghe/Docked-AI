using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Docked_AI.Features.Localization;
using Docked_AI.功能.统一调用;

namespace Docked_AI.功能.统一调用.托盘右键菜单;

/// <summary>
/// 托盘右键菜单服务
/// 统一管理托盘图标的右键菜单项
/// </summary>
public static class TrayContextMenuService
{
    /// <summary>
    /// 创建完整的托盘菜单（自动检测输入类型）
    /// </summary>
    /// <param name="onOpenWindow">打开窗口回调</param>
    /// <param name="onCloseWindow">关闭窗口（释放内存）回调</param>
    /// <param name="onExit">退出应用回调</param>
    /// <returns>托盘菜单对象</returns>
    [Obsolete("请使用 CreateMouseTrayMenu 或 CreateTouchTrayMenu 以获得正确的菜单密度")]
    public static MenuFlyout CreateTrayMenu(Action onOpenWindow, Action onCloseWindow, Action onExit)
    {
        // 默认使用鼠标模式（紧凑）
        return CreateMouseTrayMenu(onOpenWindow, onCloseWindow, onExit);
    }

    /// <summary>
    /// 创建鼠标模式的托盘菜单（紧凑间距，28px 高度）
    /// </summary>
    /// <param name="onOpenWindow">打开窗口回调</param>
    /// <param name="onCloseWindow">关闭窗口（释放内存）回调</param>
    /// <param name="onExit">退出应用回调</param>
    /// <returns>鼠标模式托盘菜单对象</returns>
    public static MenuFlyout CreateMouseTrayMenu(Action onOpenWindow, Action onCloseWindow, Action onExit)
    {
        // 🖱️ 从 XAML 加载鼠标模式菜单
        var resources = new ResourceDictionary
        {
            Source = new Uri("ms-appx:///功能/托盘/TrayMenus.xaml")
        };

        if (resources["MouseTrayMenu"] is not MenuFlyout flyout)
        {
            System.Diagnostics.Debug.WriteLine("[TrayMenu] ❌ Failed to load MouseTrayMenu from XAML");
            // 降级：使用动态创建
            return CreateMouseTrayMenuFallback(onOpenWindow, onCloseWindow, onExit);
        }

        // 绑定事件处理器
        BindMenuEvents(flyout, "Mouse", onOpenWindow, onCloseWindow, onExit);
        
        // 🔑 关键：通过 Padding 来控制紧凑间距（不裁剪内容）
        flyout.Opening += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine("[TrayMenu] Mouse menu Opening - applying compact padding");
            foreach (var item in flyout.Items)
            {
                if (item is MenuFlyoutItem menuItem)
                {
                    // 紧凑模式：减少上下 Padding（左右保持 12）
                    menuItem.Padding = new Thickness(12, 4, 12, 4);
                    System.Diagnostics.Debug.WriteLine($"[TrayMenu] Set {menuItem.Name}: Padding=12,4,12,4 (compact)");
                }
            }
        };
        
        System.Diagnostics.Debug.WriteLine("[TrayMenu] ✅ Loaded Mouse menu from XAML (compact padding)");
        return flyout;
    }

    /// <summary>
    /// 创建触摸模式的托盘菜单（大间距，44px 高度）
    /// </summary>
    /// <param name="onOpenWindow">打开窗口回调</param>
    /// <param name="onCloseWindow">关闭窗口（释放内存）回调</param>
    /// <param name="onExit">退出应用回调</param>
    /// <returns>触摸模式托盘菜单对象</returns>
    public static MenuFlyout CreateTouchTrayMenu(Action onOpenWindow, Action onCloseWindow, Action onExit)
    {
        // 🖐️ 从 XAML 加载触摸模式菜单
        var resources = new ResourceDictionary
        {
            Source = new Uri("ms-appx:///功能/托盘/TrayMenus.xaml")
        };

        if (resources["TouchTrayMenu"] is not MenuFlyout flyout)
        {
            System.Diagnostics.Debug.WriteLine("[TrayMenu] ❌ Failed to load TouchTrayMenu from XAML");
            // 降级：使用动态创建
            return CreateTouchTrayMenuFallback(onOpenWindow, onCloseWindow, onExit);
        }

        // 绑定事件处理器
        BindMenuEvents(flyout, "Touch", onOpenWindow, onCloseWindow, onExit);
        
        // 🔑 关键：通过 Padding 来控制触摸间距（不裁剪内容）
        flyout.Opening += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine("[TrayMenu] Touch menu Opening - applying spacious padding");
            foreach (var item in flyout.Items)
            {
                if (item is MenuFlyoutItem menuItem)
                {
                    // 触摸模式：增加上下 Padding（左右保持 12）
                    menuItem.Padding = new Thickness(12, 12, 12, 12);
                    System.Diagnostics.Debug.WriteLine($"[TrayMenu] Set {menuItem.Name}: Padding=12,12,12,12 (touch)");
                }
            }
        };
        
        System.Diagnostics.Debug.WriteLine("[TrayMenu] ✅ Loaded Touch menu from XAML (spacious padding)");
        return flyout;
    }

    /// <summary>
    /// 绑定菜单事件处理器
    /// </summary>
    private static void BindMenuEvents(MenuFlyout flyout, string prefix, Action onOpenWindow, Action onCloseWindow, Action onExit)
    {
        System.Diagnostics.Debug.WriteLine($"[TrayMenu] Binding events for {prefix} menu, Items count: {flyout.Items.Count}");
        
        foreach (var item in flyout.Items)
        {
            if (item is MenuFlyoutItem menuItem)
            {
                // 输出每个菜单项的高度信息
                System.Diagnostics.Debug.WriteLine($"[TrayMenu] Item '{menuItem.Name}': MinHeight={menuItem.MinHeight}, MaxHeight={menuItem.MaxHeight}, Height={menuItem.Height}, ActualHeight={menuItem.ActualHeight}");
                
                // 根据 x:Name 绑定事件
                if (menuItem.Name == $"{prefix}OpenWindow")
                {
                    menuItem.Click += (s, e) => onOpenWindow?.Invoke();
                    // 应用本地化文本
                    menuItem.Text = LocalizationHelper.GetString("TrayMenu_OpenWindow");
                }
                else if (menuItem.Name == $"{prefix}CloseWindow")
                {
                    menuItem.Click += (s, e) => onCloseWindow?.Invoke();
                    menuItem.Text = LocalizationHelper.GetString("TrayMenu_CloseWindow");
                }
                else if (menuItem.Name == $"{prefix}Restart")
                {
                    menuItem.Click += OnRestart;
                    menuItem.Text = LocalizationHelper.GetString("TrayMenu_Restart");
                }
                else if (menuItem.Name == $"{prefix}Exit")
                {
                    menuItem.Click += (s, e) => onExit?.Invoke();
                    menuItem.Text = LocalizationHelper.GetString("TrayMenu_Exit");
                }
            }
        }
    }

    /// <summary>
    /// 降级方案：动态创建鼠标模式菜单
    /// </summary>
    private static MenuFlyout CreateMouseTrayMenuFallback(Action onOpenWindow, Action onCloseWindow, Action onExit)
    {
        var flyout = new MenuFlyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom
        };

        AddMenuItems(flyout, onOpenWindow, onCloseWindow, onExit);
        
        // 设置紧凑样式
        foreach (var item in flyout.Items)
        {
            if (item is MenuFlyoutItem menuItem)
            {
                menuItem.MinHeight = 28.0;
                menuItem.Padding = new Thickness(12, 6, 12, 6);
            }
        }
        
        System.Diagnostics.Debug.WriteLine("[TrayMenu] ✅ Created Mouse menu (fallback, 28px)");
        return flyout;
    }

    /// <summary>
    /// 降级方案：动态创建触摸模式菜单
    /// </summary>
    private static MenuFlyout CreateTouchTrayMenuFallback(Action onOpenWindow, Action onCloseWindow, Action onExit)
    {
        var flyout = new MenuFlyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom
        };

        AddMenuItems(flyout, onOpenWindow, onCloseWindow, onExit);
        
        // 设置触摸样式
        foreach (var item in flyout.Items)
        {
            if (item is MenuFlyoutItem menuItem)
            {
                menuItem.MinHeight = 44.0;
                menuItem.Padding = new Thickness(12, 10, 12, 10);
            }
        }
        
        System.Diagnostics.Debug.WriteLine("[TrayMenu] ✅ Created Touch menu (fallback, 44px)");
        return flyout;
    }

    /// <summary>
    /// 添加菜单项到 MenuFlyout（共享逻辑）
    /// </summary>
    private static void AddMenuItems(MenuFlyout flyout, Action onOpenWindow, Action onCloseWindow, Action onExit)
    {
        // 打开主窗口
        var openItem = new MenuFlyoutItem
        {
            Text = LocalizationHelper.GetString("TrayMenu_OpenWindow"),
            Icon = new FontIcon { Glyph = "\uE78B" } // 窗口图标
        };
        openItem.Click += (s, e) => onOpenWindow?.Invoke();
        flyout.Items.Add(openItem);

        // 清理窗口（关闭窗口释放内存，保留托盘）
        var closeWindowItem = new MenuFlyoutItem
        {
            Text = LocalizationHelper.GetString("TrayMenu_CloseWindow"),
            Icon = new FontIcon { Glyph = "\uEA99" }
        };
        closeWindowItem.Click += (s, e) => onCloseWindow?.Invoke();
        flyout.Items.Add(closeWindowItem);

        // 重启应用
        var restartItem = new MenuFlyoutItem
        {
            Text = LocalizationHelper.GetString("TrayMenu_Restart"),
            Icon = new FontIcon { Glyph = "\uE72C" } // 刷新图标
        };
        restartItem.Click += OnRestart;
        flyout.Items.Add(restartItem);

        // 分隔线
        flyout.Items.Add(new MenuFlyoutSeparator());

        // 退出
        var exitItem = new MenuFlyoutItem
        {
            Text = LocalizationHelper.GetString("TrayMenu_Exit"),
            Icon = new FontIcon { Glyph = "\uF3B1" } // 关闭图标
        };
        exitItem.Click += (s, e) => onExit?.Invoke();
        flyout.Items.Add(exitItem);
    }

    /// <summary>
    /// 重启应用
    /// </summary>
    private static void OnRestart(object sender, RoutedEventArgs e)
    {
        try
        {
            AppRestartService.Restart();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TrayContextMenu] Restart failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 添加自定义菜单项
    /// </summary>
    /// <param name="flyout">目标菜单</param>
    /// <param name="text">菜单文本</param>
    /// <param name="icon">图标字形</param>
    /// <param name="onClick">点击回调</param>
    public static void AddCustomMenuItem(MenuFlyout flyout, string text, string icon, Action onClick)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Icon = new FontIcon { Glyph = icon }
        };
        item.Click += (s, e) => onClick?.Invoke();
        
        // 插入到退出按钮之前
        int exitIndex = flyout.Items.Count - 1;
        if (exitIndex > 0)
        {
            flyout.Items.Insert(exitIndex, item);
        }
        else
        {
            flyout.Items.Add(item);
        }
    }
}
