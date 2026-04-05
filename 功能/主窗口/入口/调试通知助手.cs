using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;

namespace Docked_AI.Features.MainWindow.Entry
{
    /// <summary>
    /// 调试通知助手，用于在窗口初始化过程中发送系统通知
    /// </summary>
    public static class DebugNotificationHelper
    {
        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化通知管理器
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                var notificationManager = AppNotificationManager.Default;
                notificationManager.NotificationInvoked += (sender, args) => { };
                notificationManager.Register();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DebugNotificationHelper] Failed to initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送调试通知
        /// </summary>
        /// <param name="title">通知标题</param>
        /// <param name="message">通知内容</param>
        public static void SendNotification(string title, string message)
        {
            try
            {
                Initialize();

                var builder = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(message);

                var notification = builder.BuildNotification();
                AppNotificationManager.Default.Show(notification);

                System.Diagnostics.Debug.WriteLine($"[Notification] {title}: {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DebugNotificationHelper] Failed to send notification: {ex.Message}");
            }
        }
    }
}
