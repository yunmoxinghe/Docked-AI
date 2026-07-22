using System;

namespace Docked_AI.Features.Tray
{
    /// <summary>
    /// 输入设备类型枚举
    /// </summary>
    public enum InputDeviceType
    {
        /// <summary>
        /// 鼠标输入
        /// </summary>
        Mouse,
        
        /// <summary>
        /// 触摸输入
        /// </summary>
        Touch,
        
        /// <summary>
        /// 触控笔输入
        /// </summary>
        Pen,
        
        /// <summary>
        /// 未知输入类型
        /// </summary>
        Unknown
    }

    public class SystemTrayIconEventArgs : EventArgs
    {
        internal SystemTrayIconEventArgs() { }
        
        /// <summary>
        /// 要显示的 Flyout 菜单
        /// </summary>
        public Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase? Flyout { get; set; }
        
        /// <summary>
        /// 是否已处理该事件
        /// </summary>
        public bool Handled { get; set; }
        
        /// <summary>
        /// 触发事件的输入设备类型
        /// </summary>
        public InputDeviceType InputDevice { get; internal set; } = InputDeviceType.Unknown;
    }
}
