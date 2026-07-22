using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Docked_AI.Features.UnifiedCalls.InAppDialog;
using Docked_AI.Features.Localization;

namespace Docked_AI.Features.Pages.Settings.WebSettings
{
    /// <summary>
    /// 图标选择助手 - 提供图标选择对话框（使用虚拟化控件优化性能）
    /// </summary>
    public static class IconPickerHelper
    {
        /// <summary>
        /// 显示图标选择对话框
        /// </summary>
        /// <param name="page">页面实例（用于显示对话框）</param>
        /// <param name="currentCode">当前选中的图标十六进制 Code（例如：E8FB）</param>
        /// <returns>选中的图标十六进制 Code（null 表示取消）</returns>
        public static async Task<string?> ShowPickerAsync(Page page, string? currentCode = null)
        {
            // 创建图标选择器控件
            var pickerControl = new DockedAI.功能.页面.设置.网页组设置.IconPickerControl();
            pickerControl.SetInitialSelection(currentCode);

            var dialog = new UnifiedInAppDialog();
            dialog.Configure(
                LocalizationHelper.GetString("IconPicker_DialogTitle"),
                pickerControl,
                LocalizationHelper.GetString("IconPicker_PrimaryButton"),
                LocalizationHelper.GetString("IconPicker_CloseButton"),
                defaultButton: ContentDialogButton.Primary);

            var result = await InAppDialogService.ShowAsync(dialog, page);

            if (result != ContentDialogResult.Primary)
            {
                return null;
            }

            return pickerControl.SelectedIconCode;
        }
    }
}
