using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Docked_AI.Features.UnifiedCalls.InAppDialog;

public static class InAppDialogService
{
    public static async Task<ContentDialogResult?> ShowAsync(
        string title,
        object content,
        FrameworkElement? owner = null,
        string? primaryButtonText = null,
        string? closeButtonText = null,
        string? secondaryButtonText = null,
        ContentDialogButton defaultButton = ContentDialogButton.Close)
    {
        var xamlRoot = ResolveXamlRoot(owner);
        if (xamlRoot is null)
        {
            Debug.WriteLine("[InAppDialogService] XamlRoot is unavailable, cannot show dialog.");
            return null;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = content,
            DefaultButton = defaultButton
        };

        if (!string.IsNullOrWhiteSpace(primaryButtonText))
        {
            dialog.PrimaryButtonText = primaryButtonText;
        }

        if (!string.IsNullOrWhiteSpace(secondaryButtonText))
        {
            dialog.SecondaryButtonText = secondaryButtonText;
        }

        if (!string.IsNullOrWhiteSpace(closeButtonText))
        {
            dialog.CloseButtonText = closeButtonText;
        }

        return await dialog.ShowAsync();
    }

    public static async Task ShowMessageAsync(
        string title,
        object content,
        FrameworkElement? owner = null,
        string? closeButtonText = null)
    {
        await ShowAsync(
            title,
            content,
            owner,
            closeButtonText: closeButtonText ?? "OK");
    }

    public static async Task<bool> OpenExternalAsync(Uri uri, FrameworkElement? owner = null)
    {
        if (!await ConfirmOpenExternalAsync(uri, owner))
        {
            return false;
        }

        try
        {
            return await Launcher.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[InAppDialogService] Failed to launch uri '{uri}': {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> ConfirmOpenExternalAsync(Uri uri, FrameworkElement? owner = null)
    {
        var xamlRoot = ResolveXamlRoot(owner);
        if (xamlRoot is null)
        {
            Debug.WriteLine("[InAppDialogService] XamlRoot is unavailable, cannot show dialog.");
            return false;
        }

        var dialog = new ExternalOpenDialog
        {
            XamlRoot = xamlRoot
        };

        dialog.SetUri(uri);
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
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
