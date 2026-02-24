using System;

namespace Docked_AI.Features.Pages.WebApp.Shared
{
    public static class WebAppEventBus
    {
        public static event EventHandler<WebAppShortcut>? ShortcutCreated;

        public static void PublishShortcutCreated(WebAppShortcut shortcut)
        {
            ShortcutCreated?.Invoke(null, shortcut);
        }
    }
}
