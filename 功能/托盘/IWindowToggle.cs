namespace Docked_AI.Features.Tray
{
    /// <summary>
    /// 窗口切换接口
    /// 用于解耦托盘管理器与具体窗口类型
    /// 支持插件窗口、浮动窗口等多种窗口模式
    /// </summary>
    public interface IWindowToggle
    {
        /// <summary>
        /// 切换窗口的显示/隐藏状态
        /// </summary>
        void ToggleWindow();

        /// <summary>
        /// 获取窗口当前是否可见
        /// </summary>
        bool IsWindowVisible { get; }
    }
}
