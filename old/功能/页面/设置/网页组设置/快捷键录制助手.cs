using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using Docked_AI.Features.UnifiedCalls.InAppDialog;
using Docked_AI.Features.Localization;

namespace Docked_AI.Features.Pages.Settings.WebSettings
{
    /// <summary>
    /// 快捷键录制助手 - 提供快捷键录制对话框
    /// </summary>
    public static class HotkeyRecorderHelper
    {
        /// <summary>
        /// 显示快捷键录制对话框
        /// </summary>
        /// <param name="page">页面实例（用于显示对话框）</param>
        /// <param name="currentKey">当前快捷键</param>
        /// <param name="currentCtrl">当前 Ctrl 状态</param>
        /// <param name="currentShift">当前 Shift 状态</param>
        /// <param name="currentAlt">当前 Alt 状态</param>
        /// <returns>录制结果（null 表示取消）</returns>
        public static async Task<HotkeyRecordResult?> ShowRecorderAsync(
            Page page,
            VirtualKey currentKey = VirtualKey.None,
            bool currentCtrl = false,
            bool currentShift = false,
            bool currentAlt = false)
        {
            VirtualKey tempKey = currentKey;
            bool tempCtrl = currentCtrl;
            bool tempAlt = currentAlt;
            bool tempShift = currentShift;
            bool isCapturingHotkey = false;

            var displayText = new TextBlock
            {
                Text = tempKey == VirtualKey.None ? LocalizationHelper.GetString("HotkeyRecorder_PleasePress") : GetHotkeyDisplayText(tempKey, tempCtrl, tempAlt, tempShift),
                FontSize = 16,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            var recordButton = new ToggleButton
            {
                MinHeight = 80,
                Padding = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = displayText
            };

            recordButton.Checked += (_, _) =>
            {
                isCapturingHotkey = true;
                tempKey = VirtualKey.None;
                tempCtrl = tempAlt = tempShift = false;
                displayText.Text = LocalizationHelper.GetString("HotkeyRecorder_Recording");
            };

            recordButton.Unchecked += (_, _) => isCapturingHotkey = false;

            recordButton.PreviewKeyDown += (_, args) =>
            {
                if (!isCapturingHotkey || recordButton.IsChecked != true)
                {
                    return;
                }

                args.Handled = true;
                var key = args.Key;

                var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
                var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
                var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);

                bool ctrl = (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
                bool alt = (altState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
                bool shift = (shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

                // 忽略修饰键本身
                if (key == VirtualKey.Control || key == VirtualKey.Menu || key == VirtualKey.Shift)
                {
                    return;
                }

                tempKey = key;
                tempCtrl = ctrl;
                tempAlt = alt;
                tempShift = shift;
                displayText.Text = GetHotkeyDisplayText(key, ctrl, alt, shift);
            };

            recordButton.PreviewKeyUp += (_, args) =>
            {
                if (!isCapturingHotkey || recordButton.IsChecked != true)
                {
                    return;
                }

                args.Handled = true;

                var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
                var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
                var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);

                bool anyModifierPressed =
                    (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                    (altState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                    (shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

                if (tempKey != VirtualKey.None && !anyModifierPressed)
                {
                    recordButton.IsChecked = false;
                }
            };

            var content = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = LocalizationHelper.GetString("HotkeyRecorder_Instruction"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    recordButton,
                    new TextBlock
                    {
                        Text = LocalizationHelper.GetString("HotkeyRecorder_Hint"),
                        FontSize = 12,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            var dialog = new UnifiedInAppDialog();
            dialog.Configure(
                LocalizationHelper.GetString("HotkeyRecorder_DialogTitle"),
                content,
                LocalizationHelper.GetString("HotkeyRecorder_PrimaryButton"),
                LocalizationHelper.GetString("HotkeyRecorder_CloseButton"),
                defaultButton: ContentDialogButton.Primary);

            var result = await InAppDialogService.ShowAsync(dialog, page);
            isCapturingHotkey = false;

            if (result != ContentDialogResult.Primary || tempKey == VirtualKey.None)
            {
                return null;
            }

            return new HotkeyRecordResult(tempKey, tempCtrl, tempShift, tempAlt);
        }

        private static string GetHotkeyDisplayText(VirtualKey key, bool ctrl, bool alt, bool shift)
        {
            var parts = new List<string>();
            if (ctrl) parts.Add("Ctrl");
            if (shift) parts.Add("Shift");
            if (alt) parts.Add("Alt");
            if (key != VirtualKey.None) parts.Add(GetKeyDisplayName(key));
            return parts.Count > 0 ? string.Join(" + ", parts) : LocalizationHelper.GetString("HotkeyRecorder_NotSet");
        }

        private static string GetKeyDisplayName(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.Space => "Space",
                VirtualKey.Enter => "Enter",
                VirtualKey.Tab => "Tab",
                VirtualKey.Escape => "Esc",
                VirtualKey.Back => "Backspace",
                VirtualKey.Delete => "Delete",
                VirtualKey.Home => "Home",
                VirtualKey.End => "End",
                VirtualKey.PageUp => "PageUp",
                VirtualKey.PageDown => "PageDown",
                VirtualKey.Left => "←",
                VirtualKey.Right => "→",
                VirtualKey.Up => "↑",
                VirtualKey.Down => "↓",
                VirtualKey.F1 => "F1",
                VirtualKey.F2 => "F2",
                VirtualKey.F3 => "F3",
                VirtualKey.F4 => "F4",
                VirtualKey.F5 => "F5",
                VirtualKey.F6 => "F6",
                VirtualKey.F7 => "F7",
                VirtualKey.F8 => "F8",
                VirtualKey.F9 => "F9",
                VirtualKey.F10 => "F10",
                VirtualKey.F11 => "F11",
                VirtualKey.F12 => "F12",
                _ => key.ToString()
            };
        }
    }

    /// <summary>
    /// 快捷键录制结果
    /// </summary>
    public sealed record HotkeyRecordResult(
        VirtualKey Key,
        bool Ctrl,
        bool Shift,
        bool Alt);
}
