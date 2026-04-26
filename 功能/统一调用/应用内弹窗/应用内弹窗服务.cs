using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.UnifiedCalls.InAppDialog;

public static class InAppDialogService
{
    private static readonly SemaphoreSlim DialogLock = new(1, 1);

    public static async Task<ContentDialogResult?> ShowAsync(ContentDialog dialog, FrameworkElement? owner = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var xamlRoot = ResolveXamlRoot(owner);
        if (xamlRoot is null)
        {
            Debug.WriteLine("[InAppDialogService] XamlRoot is unavailable, cannot show dialog.");
            return null;
        }

        await DialogLock.WaitAsync();
        try
        {
            dialog.XamlRoot = xamlRoot;
            // 同步主题，确保弹窗跟随应用当前主题（修复夜间主题不生效问题）
            if (xamlRoot.Content is FrameworkElement rootElement)
            {
                dialog.RequestedTheme = rootElement.ActualTheme;
            }
            return await dialog.ShowAsync();
        }
        finally
        {
            DialogLock.Release();
        }
    }

    private static XamlRoot? ResolveXamlRoot(FrameworkElement? owner)
    {
        if (owner?.XamlRoot is not null)
        {
            return owner.XamlRoot;
        }

        if (Application.Current is App app && app.MainWindow?.Content is FrameworkElement rootElement)
        {
            return rootElement.XamlRoot;
        }

        return null;
    }
}
