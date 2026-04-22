using Docked_AI.Features.Localization;

namespace Docked_AI.Features.UnifiedCalls.InAppDialog;

public static class LanguageRestartConfirmDialogFactory
{
    public static UnifiedInAppDialog Create()
    {
        var dialog = new UnifiedInAppDialog();
        dialog.ConfigureMessage(
            LocalizationHelper.GetString("SettingsPage_RestartTitle"),
            LocalizationHelper.GetString("SettingsPage_RestartContent"),
            LocalizationHelper.GetString("SettingsPage_RestartButton"),
            LocalizationHelper.GetString("SettingsPage_LaterButton"));
        return dialog;
    }
}
