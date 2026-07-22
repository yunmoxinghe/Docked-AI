using System;
using Windows.Storage;
using Windows.System;

namespace Docked_AI.Features.Hotkey
{
    /// <summary>
    /// 快捷键设置存储和管理
    /// </summary>
    public class HotkeySettings
    {
        private const string SETTINGS_KEY_ENABLED = "GlobalHotkey_Enabled";
        private const string SETTINGS_KEY_KEY = "GlobalHotkey_Key";
        private const string SETTINGS_KEY_CTRL = "GlobalHotkey_Ctrl";
        private const string SETTINGS_KEY_ALT = "GlobalHotkey_Alt";
        private const string SETTINGS_KEY_SHIFT = "GlobalHotkey_Shift";
        private const string SETTINGS_KEY_WIN = "GlobalHotkey_Win";

        private readonly ApplicationDataContainer _localSettings;

        public HotkeySettings()
        {
            _localSettings = ApplicationData.Current.LocalSettings;
        }

        /// <summary>
        /// 是否启用全局快捷键
        /// </summary>
        public bool IsEnabled
        {
            get => _localSettings.Values[SETTINGS_KEY_ENABLED] as bool? ?? true;
            set => _localSettings.Values[SETTINGS_KEY_ENABLED] = value;
        }

        /// <summary>
        /// 快捷键的主键
        /// </summary>
        public VirtualKey Key
        {
            get => (VirtualKey)(_localSettings.Values[SETTINGS_KEY_KEY] as int? ?? (int)VirtualKey.Space);
            set => _localSettings.Values[SETTINGS_KEY_KEY] = (int)value;
        }

        /// <summary>
        /// 是否包含 Ctrl 修饰键
        /// </summary>
        public bool Ctrl
        {
            get => _localSettings.Values[SETTINGS_KEY_CTRL] as bool? ?? true;
            set => _localSettings.Values[SETTINGS_KEY_CTRL] = value;
        }

        /// <summary>
        /// 是否包含 Alt 修饰键
        /// </summary>
        public bool Alt
        {
            get => _localSettings.Values[SETTINGS_KEY_ALT] as bool? ?? true;
            set => _localSettings.Values[SETTINGS_KEY_ALT] = value;
        }

        /// <summary>
        /// 是否包含 Shift 修饰键
        /// </summary>
        public bool Shift
        {
            get => _localSettings.Values[SETTINGS_KEY_SHIFT] as bool? ?? false;
            set => _localSettings.Values[SETTINGS_KEY_SHIFT] = value;
        }

        /// <summary>
        /// 是否包含 Win 修饰键
        /// </summary>
        public bool Win
        {
            get => _localSettings.Values[SETTINGS_KEY_WIN] as bool? ?? false;
            set => _localSettings.Values[SETTINGS_KEY_WIN] = value;
        }

        /// <summary>
        /// 获取快捷键的显示文本
        /// </summary>
        public string GetDisplayText()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(GetKeyDisplayName(Key));

            return string.Join(" + ", parts);
        }

        private string GetKeyDisplayName(VirtualKey key)
        {
            // 常见按键的显示名称
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
}
