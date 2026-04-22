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
    /// 创建完整的托盘菜单
    /// </summary>
    /// <param name="onOpenWindow">打开窗口回调</param>
    /// <param name="onExit">退出应用回调</param>
    /// <returns>托盘菜单对象</returns>
    public static MenuFlyout CreateTrayMenu(Action onOpenWindow, Action onExit)
    {
        var flyout = new MenuFlyout();

        // 打开主窗口
        var openItem = new MenuFlyoutItem
        {
            Text = LocalizationHelper.GetString("TrayMenu_OpenWindow"),
            Icon = new FontIcon { Glyph = "\uE78B" } // 窗口图标
        };
        openItem.Click += (s, e) => onOpenWindow?.Invoke();
        flyout.Items.Add(openItem);

        // 分隔线
        flyout.Items.Add(new MenuFlyoutSeparator());

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

        return flyout;
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
