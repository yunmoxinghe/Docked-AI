using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Docked_AI.Features.UnifiedCalls.InAppDialog;

public sealed partial class HotkeyRecordingContent : UserControl
{
    private VirtualKey _tempKey = VirtualKey.None;
    private bool _tempCtrl;
    private bool _tempAlt;
    private bool _tempShift;
    private bool _tempWin;
    private bool _isCapturingHotkey;

    public HotkeyCaptureResult? Result { get; private set; }

    public HotkeyRecordingContent()
    {
        InitializeComponent();
        ResetCapture();
    }

    public void ResetCapture()
    {
        _isCapturingHotkey = false;
        _tempKey = VirtualKey.None;
        _tempCtrl = _tempAlt = _tempShift = _tempWin = false;
        HotkeyToggleButton.IsChecked = false;
        HotkeyDisplayText.Text = "点击开始录制";
        Result = null;
    }

    public void Confirm()
    {
        _isCapturingHotkey = false;
        Result = _tempKey == VirtualKey.None
            ? null
            : new HotkeyCaptureResult(_tempKey, _tempCtrl, _tempAlt, _tempShift, _tempWin);
    }

    public void Cancel()
    {
        _isCapturingHotkey = false;
        Result = null;
    }

    private void HotkeyToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        _isCapturingHotkey = true;
        _tempKey = VirtualKey.None;
        _tempCtrl = _tempAlt = _tempShift = _tempWin = false;
        HotkeyDisplayText.Text = "按下快捷键...";
    }

    private void HotkeyToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _isCapturingHotkey = false;
    }

    private void HotkeyToggleButton_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isCapturingHotkey || HotkeyToggleButton.IsChecked != true)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key;

        var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
        var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        var winLeftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows);
        var winRightState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows);

        bool ctrl = (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        bool alt = (altState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        bool shift = (shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        bool win = (winLeftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
                   (winRightState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

        if (key == VirtualKey.Control || key == VirtualKey.Menu ||
            key == VirtualKey.Shift || key == VirtualKey.LeftWindows || key == VirtualKey.RightWindows)
        {
            return;
        }

        if (!ctrl && !alt && !shift && !win)
        {
            HotkeyDisplayText.Text = "需要至少一个修饰键";
            return;
        }

        _tempKey = key;
        _tempCtrl = ctrl;
        _tempAlt = alt;
        _tempShift = shift;
        _tempWin = win;
        HotkeyDisplayText.Text = GetHotkeyDisplayText(key, ctrl, alt, shift, win);
    }

    private void HotkeyToggleButton_PreviewKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (!_isCapturingHotkey || HotkeyToggleButton.IsChecked != true)
        {
            return;
        }

        e.Handled = true;

        var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
        var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        var winLeftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows);
        var winRightState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows);

        bool anyModifierPressed =
            (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
            (altState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
            (shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
            (winLeftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
            (winRightState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

        if (_tempKey != VirtualKey.None && !anyModifierPressed)
        {
            HotkeyToggleButton.IsChecked = false;
        }
    }

    private string GetHotkeyDisplayText(VirtualKey key, bool ctrl, bool alt, bool shift, bool win)
    {
        var parts = new System.Collections.Generic.List<string>();

        if (ctrl) parts.Add("Ctrl");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");
        if (win) parts.Add("Win");

        if (key != VirtualKey.None)
        {
            parts.Add(GetKeyDisplayName(key));
        }

        return parts.Count > 0 ? string.Join(" + ", parts) : "未设置";
    }

    private string GetKeyDisplayName(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.Space => "Space",
            VirtualKey.Enter => "Enter",
            VirtualKey.Escape => "Esc",
            VirtualKey.Tab => "Tab",
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
            _ when key >= VirtualKey.F1 && key <= VirtualKey.F24 => $"F{(int)key - (int)VirtualKey.F1 + 1}",
            _ when key >= VirtualKey.Number0 && key <= VirtualKey.Number9 => $"{(int)key - (int)VirtualKey.Number0}",
            _ when key >= VirtualKey.A && key <= VirtualKey.Z => key.ToString(),
            _ => key.ToString()
        };
    }
}

public sealed record HotkeyCaptureResult(
    VirtualKey Key,
    bool Ctrl,
    bool Alt,
    bool Shift,
    bool Win);
