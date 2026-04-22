using System;

namespace Docked_AI.Features.Notification
{
    /// <summary>
    /// 通知工具类 - 双模式兼容（MSIX 打包 / WinExe 无包）
    /// 
    /// 【核心能力】
    /// ✔ 自动检测运行模式（有包/无包）
    /// ✔ MSIX 模式：使用系统原生通知（无需 AUMID）
    /// ✔ WinExe 模式：使用自定义 AUMID 发送通知
    /// 
    /// 【使用方式】
    /// NotifyHelper.Show("标题", "内容");
    /// 
    /// 【技术细节】
    /// - MSIX 模式：通过 Windows.ApplicationModel.Package.Current 检测
    /// - WinExe 模式：需要在注册表注册 AUMID（首次运行时）
    /// - 依赖：CommunityToolkit.WinUI.Notifications
    /// </summary>
    public static class NotifyHelper
    {
        /// <summary>
        /// 显示系统通知
        /// </summary>
        /// <param name="title">通知标题</param>
        /// <param name="content">通知内容</param>
        public static void Show(string title, string content)
        {
            try
            {
                if (HasPackageIdentity())
                {
                    // ⭐ MSIX 模式：使用系统原生通知
                    ShowPackagedNotification(title, content);
                }
                else
                {
                    // ⭐ 无包模式：使用自定义 AUMID
                    ShowUnpackagedNotification(title, content);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotifyHelper] Failed to show notification: {ex.Message}");
            }
        }

        /// <summary>
        /// MSIX 打包模式通知
        /// </summary>
        private static void ShowPackagedNotification(string title, string content)
        {
            // 使用 CommunityToolkit.WinUI.Notifications（如果已安装）
            // 或者使用 Windows.UI.Notifications.ToastNotificationManager
            
            // 简化实现：使用 Windows 原生 API
            var toastXml = $@"
                <toast>
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>{System.Security.SecurityElement.Escape(title)}</text>
                            <text>{System.Security.SecurityElement.Escape(content)}</text>
                        </binding>
                    </visual>
                </toast>";

            var xmlDoc = new Windows.Data.Xml.Dom.XmlDocument();
            xmlDoc.LoadXml(toastXml);

            var toast = new Windows.UI.Notifications.ToastNotification(xmlDoc);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        /// <summary>
        /// WinExe 无包模式通知
        /// </summary>
        private static void ShowUnpackagedNotification(string title, string content)
        {
            // 无包模式需要指定 AUMID（Application User Model ID）
            const string AUMID = "DockedAI.App";

            var toastXml = $@"
                <toast>
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>{System.Security.SecurityElement.Escape(title)}</text>
                            <text>{System.Security.SecurityElement.Escape(content)}</text>
                        </binding>
                    </visual>
                </toast>";

            var xmlDoc = new Windows.Data.Xml.Dom.XmlDocument();
            xmlDoc.LoadXml(toastXml);

            var toast = new Windows.UI.Notifications.ToastNotification(xmlDoc);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier(AUMID).Show(toast);
        }

        /// <summary>
        /// 检测是否为 MSIX 打包模式
        /// </summary>
        /// <returns>true = MSIX 打包，false = WinExe 无包</returns>
        private static bool HasPackageIdentity()
        {
            try
            {
                // 尝试访问 Package.Current，如果成功则为 MSIX 模式
                var _ = Windows.ApplicationModel.Package.Current;
                return true;
            }
            catch
            {
                // 访问失败则为无包模式
                return false;
            }
        }
    }
}
