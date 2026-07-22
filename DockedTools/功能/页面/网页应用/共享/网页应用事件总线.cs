using System;

namespace DockedTools.Features.Pages.WebApp.Shared
{
    public static class WebAppEventBus
    {
        public static event EventHandler<WebAppShortcut>? ShortcutCreated;
        public static event EventHandler? ShortcutsRefreshRequested;

        public static void PublishShortcutCreated(WebAppShortcut shortcut)
        {
            ShortcutCreated?.Invoke(null, shortcut);
        }

        public static void RaiseShortcutCreated(WebAppShortcut? shortcut)
        {
            // 触发事件通知主页刷新，传入 null 时创建一个空的快捷方式对象
            var dummyShortcut = shortcut ?? new WebAppShortcut("", "", "", null);
            ShortcutCreated?.Invoke(null, dummyShortcut);
        }

        public static void RequestRefresh()
        {
            // 触发刷新事件，不传递具体的快捷方式对象
            ShortcutsRefreshRequested?.Invoke(null, EventArgs.Empty);
        }
    }
}
