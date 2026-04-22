using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Docked_AI.Features.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Docked_AI.功能.统一调用.应用内弹窗;

/// <summary>
/// 应用内弹窗统一调用服务。
/// 负责解析可用的 XamlRoot，并串行显示 ContentDialog，避免同一时间重复弹窗。
/// </summary>
public static class 应用内弹窗服务
{
    private static readonly SemaphoreSlim DialogLock = new(1, 1);

    /// <summary>
    /// 显示一个基础消息弹窗。
    /// </summary>
    public static Task<ContentDialogResult> ShowMessageAsync(
        string title,
        object content,
        string closeButtonText,
        string? primaryButtonText = null,
        ContentDialogButton defaultButton = ContentDialogButton.Close,
        XamlRoot? xamlRoot = null)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = closeButtonText,
            PrimaryButtonText = primaryButtonText,
            DefaultButton = defaultButton
        };

        return ShowAsync(dialog, xamlRoot);
    }

    /// <summary>
    /// 显示外部链接打开确认弹窗。
    /// </summary>
    public static Task<ContentDialogResult> ShowExternalLinkConfirmAsync(XamlRoot? xamlRoot = null)
    {
        var dialog = new ContentDialog
        {
            Title = LocalizationHelper.GetString("InAppDialog_ExternalLink_Title"),
            Content = LocalizationHelper.GetString("InAppDialog_ExternalLink_Content"),
            CloseButtonText = LocalizationHelper.GetString("InAppDialog_ExternalLink_CancelButton"),
            PrimaryButtonText = LocalizationHelper.GetString("InAppDialog_ExternalLink_OpenButton"),
            DefaultButton = ContentDialogButton.Primary
        };

        return ShowAsync(dialog, xamlRoot);
    }

    /// <summary>
    /// 确认后在系统默认浏览器中打开外部链接。
    /// </summary>
    public static async Task<bool> OpenExternalLinkAsync(Uri uri, XamlRoot? xamlRoot = null)
    {
        var result = await ShowExternalLinkConfirmAsync(xamlRoot);
        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        try
        {
            return await Launcher.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[InAppDialog] Failed to open external link: {ex}");

            await ShowMessageAsync(
                LocalizationHelper.GetString("InAppDialog_ErrorTitle"),
                LocalizationHelper.GetString("InAppDialog_OpenExternalFailed"),
                LocalizationHelper.GetString("InAppDialog_ConfirmButton"),
                xamlRoot: xamlRoot);

            return false;
        }
    }

    /// <summary>
    /// 使用统一入口显示指定弹窗。
    /// </summary>
    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog, XamlRoot? xamlRoot = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var resolvedXamlRoot = ResolveXamlRoot(xamlRoot);
        if (resolvedXamlRoot == null)
        {
            Debug.WriteLine("[InAppDialog] No available XamlRoot was found.");
            return ContentDialogResult.None;
        }

        await DialogLock.WaitAsync();
        try
        {
            dialog.XamlRoot = resolvedXamlRoot;
            return await dialog.ShowAsync();
        }
        finally
        {
            DialogLock.Release();
        }
    }

    private static XamlRoot? ResolveXamlRoot(XamlRoot? preferredXamlRoot)
    {
        if (preferredXamlRoot != null)
        {
            return preferredXamlRoot;
        }

        if (Application.Current is App app && app.MainWindow?.Content is FrameworkElement rootElement)
        {
            return rootElement.XamlRoot;
        }

        return null;
    }
}
