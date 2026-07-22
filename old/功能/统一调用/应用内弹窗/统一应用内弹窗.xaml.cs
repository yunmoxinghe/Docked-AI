using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.UnifiedCalls.InAppDialog;

public sealed partial class UnifiedInAppDialog : ContentDialog
{
    public UnifiedInAppDialog()
    {
        InitializeComponent();
    }

    public void Configure(
        string title,
        object content,
        string? primaryButtonText = null,
        string? closeButtonText = null,
        string? secondaryButtonText = null,
        ContentDialogButton defaultButton = ContentDialogButton.Close)
    {
        Title = title;
        PrimaryButtonText = primaryButtonText ?? string.Empty;
        CloseButtonText = closeButtonText ?? string.Empty;
        SecondaryButtonText = secondaryButtonText ?? string.Empty;
        DefaultButton = defaultButton;
        DialogContentPresenter.Content = content;
    }
}
