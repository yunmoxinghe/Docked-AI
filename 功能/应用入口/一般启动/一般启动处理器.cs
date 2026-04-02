using System;
using Microsoft.UI.Xaml;
using Docked_AI.Features.Tray;

namespace Docked_AI.Features.AppEntry.NormalLaunch
{
    /// <summary>
    /// 处理应用的一般启动逻辑
    /// </summary>
    public class NormalLaunchHandler
    {
        private readonly App _app;
        private TrayIconManager? _trayIconManager;

        public NormalLaunchHandler(App app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        /// <summary>
        /// 处理一般启动
        /// </summary>
        public void Handle(Action exitCallback)
        {
            // Initialize the tray icon manager
            _trayIconManager = new TrayIconManager(null, exitCallback);
            _trayIconManager.Initialize();
        }

        public TrayIconManager? TrayIconManager => _trayIconManager;
    }
}
