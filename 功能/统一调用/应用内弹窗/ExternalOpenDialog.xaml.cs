using System;
using Docked_AI.Features.Localization;
using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.UnifiedCalls.InAppDialog;

public sealed partial class ExternalOpenDialog : ContentDialog
{
    public ExternalOpenDialog()
    {
        InitializeComponent();
        Title = LocalizationHelper.GetString("InAppDialog_OpenExternal_Title");
        PrimaryButtonText = LocalizationHelper.GetString("InAppDialog_OpenExternal_OpenButton");
        CloseButtonText = LocalizationHelper.GetString("InAppDialog_OpenExternal_CancelButton");
    }

    public void SetUri(Uri uri)
    {
        MessageTextBlock.Text = LocalizationHelper.GetString("InAppDialog_OpenExternal_Content");
        UrlTextBlock.Text = uri.AbsoluteUri;
    }
}
