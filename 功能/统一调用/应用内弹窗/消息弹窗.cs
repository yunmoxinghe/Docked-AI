using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.UnifiedCalls.InAppDialog;

public static class MessageDialogFactory
{
    public static UnifiedInAppDialog Create(
        string title,
        string message,
        string primaryButtonText,
        string closeButtonText)
    {
        var dialog = new UnifiedInAppDialog();
        dialog.ConfigureMessage(title, message, primaryButtonText, closeButtonText);
        return dialog;
    }
}
