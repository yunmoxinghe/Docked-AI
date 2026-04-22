using System;

namespace Docked_AI.Features.UnifiedCalls.InAppDialog;

public static class ExternalOpenConfirmDialogFactory
{
    public static UnifiedInAppDialog Create(Uri uri)
    {
        var dialog = new UnifiedInAppDialog();
        dialog.ConfigureExternalOpen(uri);
        return dialog;
    }
}
