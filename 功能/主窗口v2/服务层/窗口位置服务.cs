using System;
using Windows.Storage;

namespace Docked_AI.功能.主窗口v2.服务层;

/// <summary>
/// 窗口位置服务实现
/// 使用 ApplicationData.LocalSettings 持久化存储浮窗位置、尺寸和边缘距离
/// </summary>
public class WindowPositionService : IWindowPositionService
{
    private const string WidthKey = "FloatingWindow_Width";
    private const string HeightKey = "FloatingWindow_Height";
    private const string RightDistanceKey = "FloatingWindow_RightDistance";
    private const string BottomDistanceKey = "FloatingWindow_BottomDistance";

    private readonly ApplicationDataContainer _localSettings;
    private readonly object _lock = new object();

    /// <summary>
    /// 创建窗口位置服务实例
    /// </summary>
    public WindowPositionService()
    {
        _localSettings = ApplicationData.Current.LocalSettings;
    }

    /// <summary>
    /// 保存浮窗位置、尺寸和边缘距离
    /// </summary>
    /// <param name="width">窗口宽度</param>
    /// <param name="height">窗口高度</param>
    /// <param name="rightDistance">距屏幕右边缘的距离</param>
    /// <param name="bottomDistance">距屏幕下边缘的距离</param>
    public void SaveFloatingPosition(double width, double height, double rightDistance, double bottomDistance)
    {
        lock (_lock)
        {
            try
            {
                _localSettings.Values[WidthKey] = width;
                _localSettings.Values[HeightKey] = height;
                _localSettings.Values[RightDistanceKey] = rightDistance;
                _localSettings.Values[BottomDistanceKey] = bottomDistance;
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常，避免影响应用运行
                System.Diagnostics.Debug.WriteLine($"Failed to save floating position: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 获取上次保存的浮窗位置和尺寸
    /// </summary>
    /// <returns>保存的位置信息，如果没有保存则返回 null</returns>
    public FloatingPositionData? GetLastFloatingPosition()
    {
        lock (_lock)
        {
            try
            {
                // 检查是否所有必需的值都存在
                if (!_localSettings.Values.ContainsKey(WidthKey) ||
                    !_localSettings.Values.ContainsKey(HeightKey) ||
                    !_localSettings.Values.ContainsKey(RightDistanceKey) ||
                    !_localSettings.Values.ContainsKey(BottomDistanceKey))
                {
                    return null;
                }

                // 读取所有值
                var width = _localSettings.Values[WidthKey];
                var height = _localSettings.Values[HeightKey];
                var rightDistance = _localSettings.Values[RightDistanceKey];
                var bottomDistance = _localSettings.Values[BottomDistanceKey];

                // 验证值的类型和有效性
                if (width is not double widthValue || widthValue <= 0 ||
                    height is not double heightValue || heightValue <= 0 ||
                    rightDistance is not double rightDistanceValue || rightDistanceValue < 0 ||
                    bottomDistance is not double bottomDistanceValue || bottomDistanceValue < 0)
                {
                    return null;
                }

                return new FloatingPositionData
                {
                    Width = widthValue,
                    Height = heightValue,
                    RightDistance = rightDistanceValue,
                    BottomDistance = bottomDistanceValue
                };
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常，返回 null 表示没有保存的位置
                System.Diagnostics.Debug.WriteLine($"Failed to load floating position: {ex.Message}");
                return null;
            }
        }
    }
}
