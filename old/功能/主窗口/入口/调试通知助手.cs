using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;

namespace Docked_AI.Features.MainWindow.Entry
{
    /// <summary>
    /// 调试通知助手 - 在窗口初始化过程中发送系统通知
    /// 
    /// 【文件职责】
    /// 1. 封装 Windows 系统通知 API，简化通知发送流程
    /// 2. 提供调试信息的可视化反馈（用于开发和测试）
    /// 3. 自动初始化通知管理器，避免重复初始化
    /// 
    /// 【使用场景】
    /// - 窗口初始化过程中的关键节点通知（如"窗口已创建"、"动画已完成"）
    /// - 调试状态转换流程，验证事件触发顺序
    /// - 生产环境应禁用或移除，避免干扰用户
    /// 
    /// 【核心逻辑流程】
    /// 1. 首次调用 SendNotification() 时自动初始化通知管理器
    /// 2. 注册通知管理器到系统（AppNotificationManager.Register）
    /// 3. 构建通知内容（标题 + 消息）
    /// 4. 显示系统通知（AppNotificationManager.Show）
    /// 
    /// 【关键依赖关系】
    /// - Microsoft.Windows.AppNotifications: Windows 系统通知 API
    /// - AppNotificationManager: 通知管理器单例
    /// 
    /// 【潜在副作用】
    /// 1. 首次调用会注册通知管理器到系统（全局副作用）
    /// 2. 通知会显示在 Windows 通知中心（用户可见）
    /// 3. 初始化失败会静默忽略（不影响主流程）
    /// 
    /// 【重构风险点】
    /// 1. 生产环境应移除或禁用此类，避免干扰用户
    /// 2. 通知管理器注册是全局操作，不应重复调用
    /// 3. 异常处理采用静默忽略策略，可能隐藏初始化问题
    /// </summary>
    public static class DebugNotificationHelper
    {
        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化通知管理器 - 注册到系统
        /// 
        /// 【调用时机】
        /// 首次调用 SendNotification() 时自动调用
        /// 
        /// 【幂等性】
        /// 使用 _isInitialized 标志防止重复初始化
        /// 
        /// 【异常处理】
        /// 初始化失败静默忽略，不影响主流程
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
        /// 发送调试通知 - 显示系统通知
        /// 
        /// 【使用示例】
        /// SendNotification("窗口状态", "窗口已创建");
        /// 
        /// 【副作用】
        /// - 首次调用会初始化通知管理器
        /// - 通知会显示在 Windows 通知中心
        /// - 同时输出到调试控制台
        /// 
        /// 【异常处理】
        /// 发送失败静默忽略，不影响主流程
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
