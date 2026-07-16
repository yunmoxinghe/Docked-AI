using System;

namespace Docked_AI.Features.Pages.WebApp.Shared
{
    /// <summary>
    /// 网页应用更新类型
    /// </summary>
    [Flags]
    public enum WebAppUpdateType
    {
        None = 0,
        Name = 1 << 0,      // 名称变化
        Url = 1 << 1,       // URL 变化
        Icon = 1 << 2,      // 图标变化（包括重置为默认）
        All = Name | Url | Icon
    }

    /// <summary>
    /// 网页应用更新事件参数
    /// </summary>
    public class WebAppUpdateEventArgs : EventArgs
    {
        public string AppId { get; }
        public WebAppUpdateType UpdateType { get; }

        public WebAppUpdateEventArgs(string appId, WebAppUpdateType updateType)
        {
            AppId = appId;
            UpdateType = updateType;
        }
    }

    /// <summary>
    /// 统一的网页应用更新状态服务
    /// 负责协调所有 UI 组件（管理页面、主页、导航栏）的细粒度更新
    /// 避免不必要的 UI 重建和闪烁
    /// </summary>
    public static class WebAppUpdateService
    {
        /// <summary>
        /// 更新开始事件 - 用于通知 UI 准备更新（避免闪烁）
        /// </summary>
        public static event EventHandler<WebAppUpdateEventArgs>? UpdateStarting;

        /// <summary>
        /// 更新完成事件 - 用于通知 UI 刷新界面
        /// </summary>
        public static event EventHandler<WebAppUpdateEventArgs>? UpdateCompleted;

        /// <summary>
        /// 触发更新通知（用于详情页保存后）
        /// </summary>
        /// <param name="appId">应用 ID</param>
        /// <param name="updateType">更新类型</param>
        public static void NotifyUpdate(string appId, WebAppUpdateType updateType)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[WebAppUpdateService] 通知更新: {appId}, 类型: {updateType}");

                // 1. 触发更新开始事件
                UpdateStarting?.Invoke(null, new WebAppUpdateEventArgs(appId, updateType));

                // 2. 触发更新完成事件
                UpdateCompleted?.Invoke(null, new WebAppUpdateEventArgs(appId, updateType));

                System.Diagnostics.Debug.WriteLine($"[WebAppUpdateService] 更新通知完成: {appId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebAppUpdateService] 更新通知失败: {appId}, {ex}");
            }
        }
    }
}
