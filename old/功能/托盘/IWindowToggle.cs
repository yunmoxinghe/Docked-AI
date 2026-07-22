using Docked_AI.Features.MainWindow.State;

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
        /// 获取窗口当前状态
        /// </summary>
        WindowState CurrentWindowState { get; }

        /// <summary>
        /// 标记初始化完成
        /// 由托盘管理器在窗口创建完成后调用
        /// </summary>
        void SetInitializingComplete();

        /// <summary>
        /// 请求执行首次显示
        /// 利用 DWM 的首次 Show 动画，简单优雅
        /// </summary>
        void RequestSlideIn();
    }
}
