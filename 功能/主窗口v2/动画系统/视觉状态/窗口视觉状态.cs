using Windows.Foundation;

namespace Docked_AI.功能.主窗口v2.动画系统.视觉状态;

/// <summary>
/// 窗口视觉状态快照，定义窗口的完整外观
/// 所有可动画属性都在此定义（Bounds、CornerRadius、Opacity 等连续量）
/// 支持线性插值（LERP）或 Spring 插值
/// </summary>
public class WindowVisualState
{
    /// <summary>
    /// 窗口位置和尺寸
    /// </summary>
    public Rect Bounds { get; set; }

    /// <summary>
    /// 圆角半径
    /// </summary>
    public double CornerRadius { get; set; }

    /// <summary>
    /// 不透明度（0.0 - 1.0）
    /// </summary>
    public double Opacity { get; set; }

    /// <summary>
    /// 是否置顶
    /// </summary>
    public bool IsTopmost { get; set; }

    /// <summary>
    /// Win32 扩展样式
    /// </summary>
    public int ExtendedStyle { get; set; }

    /// <summary>
    /// 创建默认的窗口视觉状态
    /// </summary>
    public WindowVisualState()
    {
        Bounds = new Rect(0, 0, 400, 600);
        CornerRadius = 0;
        Opacity = 1.0;
        IsTopmost = false;
        ExtendedStyle = 0;
    }

    /// <summary>
    /// 创建窗口视觉状态的副本
    /// </summary>
    public WindowVisualState Clone()
    {
        return new WindowVisualState
        {
            Bounds = this.Bounds,
            CornerRadius = this.CornerRadius,
            Opacity = this.Opacity,
            IsTopmost = this.IsTopmost,
            ExtendedStyle = this.ExtendedStyle
        };
    }
}
