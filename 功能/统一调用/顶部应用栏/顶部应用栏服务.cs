using System;
using Docked_AI.Features.MainWindowContent.ContentArea;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.UnifiedCalls.TopAppBar;

/// <summary>
/// 统一顶部应用栏服务，提供对内容区顶部栏的集中控制
/// </summary>
public static class TopAppBarService
{
    private static ContentArea? _contentArea;

    /// <summary>
    /// 注册内容区实例（由链接器在初始化时调用）
    /// </summary>
    public static void Register(ContentArea contentArea)
    {
        _contentArea = contentArea;
    }

    /// <summary>
    /// 显示或隐藏顶部应用栏
    /// </summary>
    public static bool IsVisible
    {
        get => _contentArea?.IsTopBarVisible ?? false;
        set
        {
            if (_contentArea is not null)
                _contentArea.IsTopBarVisible = value;
        }
    }

    /// <summary>
    /// 设置左侧面板内容
    /// </summary>
    public static void SetLeftContent(UIElement? element)
    {
        if (_contentArea?.TopBarLeft is not { } panel) return;
        panel.Children.Clear();
        if (element is not null)
            panel.Children.Add(element);
    }

    /// <summary>
    /// 设置右侧面板内容
    /// </summary>
    public static void SetRightContent(UIElement? element)
    {
        if (_contentArea?.TopBarRight is not { } panel) return;
        panel.Children.Clear();
        if (element is not null)
            panel.Children.Add(element);
    }

    /// <summary>
    /// 设置中间内容
    /// </summary>
    public static void SetCenterContent(object? content)
    {
        if (_contentArea?.TopBarCenter is not { } presenter) return;
        presenter.Content = content;
    }

    /// <summary>
    /// 清空所有区域内容
    /// </summary>
    public static void ClearAll()
    {
        SetLeftContent(null);
        SetCenterContent(null);
        SetRightContent(null);
    }

    /// <summary>
    /// 注册页面大标题元素，滚动时统一控制其淡入淡出
    /// </summary>
    public static void SetPageTitle(UIElement? element)
    {
        _contentArea?.SetPageTitle(element);
    }

    /// <summary>
    /// 设置页面大标题的显示状态（带动画）
    /// </summary>
    public static void SetPageTitleVisible(bool visible)
    {
        _contentArea?.SetPageTitleVisible(visible);
    }

    #region 顶栏按钮控制

    /// <summary>
    /// 设置返回按钮的可见性
    /// </summary>
    public static void SetBackButtonVisible(bool visible)
    {
        _contentArea?.SetBackButtonVisible(visible);
    }

    /// <summary>
    /// 设置菜单按钮的可见性
    /// </summary>
    public static void SetMenuButtonVisible(bool visible)
    {
        _contentArea?.SetMenuButtonVisible(visible);
    }

    /// <summary>
    /// 订阅返回按钮点击事件
    /// </summary>
    public static event EventHandler? BackButtonClicked
    {
        add
        {
            if (_contentArea is not null)
                _contentArea.BackButtonClicked += value;
        }
        remove
        {
            if (_contentArea is not null)
                _contentArea.BackButtonClicked -= value;
        }
    }

    /// <summary>
    /// 订阅菜单按钮点击事件
    /// </summary>
    public static event EventHandler? MenuButtonClicked
    {
        add
        {
            if (_contentArea is not null)
                _contentArea.MenuButtonClicked += value;
        }
        remove
        {
            if (_contentArea is not null)
                _contentArea.MenuButtonClicked -= value;
        }
    }

    /// <summary>
    /// 获取更多按钮的菜单，用于动态添加菜单项
    /// </summary>
    public static MenuFlyout? GetMoreMenu()
    {
        // 需要通过反射或添加公共属性来访问 MoreMenuFlyout
        // 暂时返回 null，后续可以扩展
        return null;
    }

    #endregion
}
