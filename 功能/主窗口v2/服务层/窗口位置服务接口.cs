namespace Docked_AI.功能.主窗口v2.服务层;

/// <summary>
/// 窗口位置服务接口
/// 负责保存和恢复浮窗位置、尺寸以及与屏幕边缘的距离
/// </summary>
public interface IWindowPositionService
{
    /// <summary>
    /// 保存浮窗位置、尺寸和边缘距离
    /// </summary>
    /// <param name="width">窗口宽度</param>
    /// <param name="height">窗口高度</param>
    /// <param name="rightDistance">距屏幕右边缘的距离</param>
    /// <param name="bottomDistance">距屏幕下边缘的距离</param>
    void SaveFloatingPosition(double width, double height, double rightDistance, double bottomDistance);

    /// <summary>
    /// 获取上次保存的浮窗位置和尺寸
    /// </summary>
    /// <returns>保存的位置信息，如果没有保存则返回 null</returns>
    FloatingPositionData? GetLastFloatingPosition();
}

/// <summary>
/// 浮窗位置数据
/// </summary>
public class FloatingPositionData
{
    /// <summary>
    /// 窗口宽度
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// 窗口高度
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// 距屏幕右边缘的距离
    /// </summary>
    public double RightDistance { get; set; }

    /// <summary>
    /// 距屏幕下边缘的距离
    /// </summary>
    public double BottomDistance { get; set; }
}
