namespace Docked_AI.功能.主窗口v2.状态机;

/// <summary>
/// 窗口状态枚举
/// 描述窗口当前所处的状态。任意时刻只有一个状态有效（互斥）。
/// </summary>
public enum WindowState
{
    /// <summary>
    /// 首次创建，WinUI3内部行为，不受状态机控制
    /// 唯一起点，只能从此状态出发，不可转入
    /// </summary>
    Initializing,

    /// <summary>
    /// 隐藏（窗口存在但不可见）
    /// </summary>
    Hidden,

    /// <summary>
    /// 浮窗模式
    /// </summary>
    Floating,

    /// <summary>
    /// 全屏模式
    /// </summary>
    Fullscreen,

    /// <summary>
    /// 边栏模式
    /// </summary>
    Sidebar
}
