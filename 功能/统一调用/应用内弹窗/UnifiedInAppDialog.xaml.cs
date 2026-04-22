using System;
using Docked_AI.Features.Localization;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace Docked_AI.Features.UnifiedCalls.InAppDialog;

public sealed partial class UnifiedInAppDialog : ContentDialog
{
    public UnifiedInAppDialog()
    {
        InitializeComponent();
    }

    public void ConfigureExternalOpen(Uri uri)
    {
        Title = LocalizationHelper.GetString("InAppDialog_OpenExternal_Title");
        PrimaryButtonText = LocalizationHelper.GetString("InAppDialog_OpenExternal_OpenButton");
        CloseButtonText = LocalizationHelper.GetString("InAppDialog_OpenExternal_CancelButton");
        MessageTextBlock.Text = LocalizationHelper.GetString("InAppDialog_OpenExternal_Content");
        UrlTextBlock.Text = uri.AbsoluteUri;
        UrlTextBlock.Visibility = Visibility.Visible;
    }

    public void ConfigureMessage(
        string title,
        string message,
        string primaryButtonText,
        string closeButtonText)
    {
        Title = title;
        PrimaryButtonText = primaryButtonText;
        CloseButtonText = closeButtonText;
        MessageTextBlock.Text = message;
        UrlTextBlock.Text = string.Empty;
        UrlTextBlock.Visibility = Visibility.Collapsed;
    }
}
