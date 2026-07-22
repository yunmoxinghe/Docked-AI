using Windows.System;

namespace DockedTools.Features.Pages.WebApp.Shared
{
    /// <summary>
    /// 键盘映射按钮配置
    /// </summary>
    public sealed record KeyboardMappingButtonConfig
    {
        /// <summary>
        /// 是否启用此按钮（默认关闭）
        /// </summary>
        public bool IsEnabled { get; init; }

        /// <summary>
        /// 图标类型（"Static" 或 "Animated"）
        /// </summary>
        public string IconType { get; init; } = "Static";

        /// <summary>
        /// 静态图标 Glyph（当 IconType = "Static" 时使用）
        /// </summary>
        public string StaticIconGlyph { get; init; } = "\uE92E"; // 默认：键盘图标

        /// <summary>
        /// AnimatedIcon 类型名称（当 IconType = "Animated" 时使用）
        /// </summary>
        public string AnimatedIconType { get; init; } = "AnimatedChevronDownSmallVisualSource";

        /// <summary>
        /// 按钮工具提示文字
        /// </summary>
        public string Tooltip { get; init; } = "执行快捷键";

        /// <summary>
        /// 要发送的快捷键 - 主键（例如：VirtualKey.S）
        /// </summary>
        public VirtualKey Key { get; init; } = VirtualKey.None;

        /// <summary>
        /// 是否包含 Ctrl 修饰键
        /// </summary>
        public bool Ctrl { get; init; }

        /// <summary>
        /// 是否包含 Shift 修饰键
        /// </summary>
        public bool Shift { get; init; }

        /// <summary>
        /// 是否包含 Alt 修饰键
        /// </summary>
        public bool Alt { get; init; }

        /// <summary>
        /// 创建一个默认配置（功能关闭）
        /// </summary>
        public static KeyboardMappingButtonConfig CreateDefault()
        {
            return new KeyboardMappingButtonConfig
            {
                IsEnabled = false,
                IconType = "Static",
                StaticIconGlyph = "\uE92E",
                AnimatedIconType = "AnimatedChevronDownSmallVisualSource",
                Tooltip = "执行快捷键",
                Key = VirtualKey.None,
                Ctrl = false,
                Shift = false,
                Alt = false
            };
        }

        /// <summary>
        /// 获取快捷键的显示文本（例如："Ctrl+S"）
        /// </summary>
        public string GetHotkeyDisplayText()
        {
            if (Key == VirtualKey.None)
            {
                return "未设置";
            }

            var parts = new System.Collections.Generic.List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Shift) parts.Add("Shift");
            if (Alt) parts.Add("Alt");
            parts.Add(GetKeyDisplayName(Key));

            return string.Join("+", parts);
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
}
