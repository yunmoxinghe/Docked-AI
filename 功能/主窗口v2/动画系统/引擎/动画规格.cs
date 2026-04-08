using System;

namespace Docked_AI.功能.主窗口v2.动画系统.引擎;

/// <summary>
/// 动画规格，定义如何从当前状态过渡到目标状态
/// </summary>
public class AnimationSpec
{
    /// <summary>
    /// 动画时长
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// 缓动函数（如 EaseInOut）
    /// </summary>
    public Easing Easing { get; set; }

    /// <summary>
    /// 可选：针对不同属性使用不同缓动
    /// </summary>
    public Func<double, double>? BoundsEasing { get; set; }

    /// <summary>
    /// 可选：针对圆角使用不同缓动
    /// </summary>
    public Func<double, double>? CornerRadiusEasing { get; set; }

    /// <summary>
    /// 可选：使用 Spring 物理模拟
    /// </summary>
    public SpringConfig? Spring { get; set; }

    /// <summary>
    /// 创建默认的动画规格
    /// </summary>
    public AnimationSpec()
    {
        Duration = TimeSpan.FromMilliseconds(250);
        Easing = Easing.EaseInOutCubic;
    }
}

/// <summary>
/// Spring 物理模拟配置
/// </summary>
public class SpringConfig
{
    /// <summary>
    /// 刚度
    /// </summary>
    public double Stiffness { get; set; }

    /// <summary>
    /// 阻尼
    /// </summary>
    public double Damping { get; set; }

    /// <summary>
    /// 创建默认的 Spring 配置
    /// </summary>
    public SpringConfig()
    {
        Stiffness = 300;
        Damping = 30;
    }
}

/// <summary>
/// 缓动函数枚举
/// </summary>
public enum Easing
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    EaseInCubic,
    EaseOutCubic,
    EaseInOutCubic
}
