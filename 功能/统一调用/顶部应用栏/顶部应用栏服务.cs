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
}
