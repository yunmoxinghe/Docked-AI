using System;
using Microsoft.UI.Windowing;

namespace Docked_AI.Features.MainWindow.Placement
{
    /// <summary>
    /// 窗口布局服务 - 计算窗口位置和尺寸
    /// 
    /// 【文件职责】
    /// 1. 计算窗口在屏幕上的目标位置（右侧边缘）
    /// 2. 计算窗口的合适尺寸（基于工作区和边距）
    /// 3. 准备显示/隐藏动画的起始位置
    /// 4. 刷新布局信息（屏幕尺寸、工作区变化时）
    /// 
    /// 【核心设计】
    /// 
    /// 为什么窗口停靠在右侧？
    /// - 用户习惯：大多数用户习惯在右侧查看辅助信息
    /// - 避免遮挡：左侧通常是主要工作区域
    /// - 一致性：与 Windows 通知中心、侧边栏等系统组件一致
    /// 
    /// 为什么需要边距（Margin）？
    /// - 标准模式：窗口与屏幕边缘保持 10px 边距，避免贴边
    /// - 固定模式：窗口贴边，边距为 0（由 AppBar 管理）
    /// - 美观性：边距让窗口看起来更舒适
    /// 
    /// 【核心逻辑流程】
    /// 
    /// 初始化流程：
    ///   1. CreateInitialState() 创建布局状态
    ///   2. Refresh() 刷新屏幕尺寸和工作区
    ///   3. 计算窗口宽度（工作区宽度的 1/3）
    ///   4. 计算窗口高度（工作区高度 - 边距）
    ///   5. 计算目标位置（右侧边缘 - 窗口宽度 - 边距）
    /// 
    /// 准备显示流程：
    ///   1. PrepareForShow() 刷新布局信息
    ///   2. 设置 CurrentX 为屏幕外（ScreenWidth）
    ///   3. 动画控制器从 CurrentX 滑动到 TargetX
    /// 
    /// 准备隐藏流程：
    ///   1. PrepareForHide() 刷新布局信息
    ///   2. 如果 CurrentX 为 0，设置为 TargetX（修正位置）
    ///   3. 动画控制器从 CurrentX 滑动到屏幕外
    /// 
    /// 【关键依赖关系】
    /// - WindowLayoutState: 布局状态，存储屏幕尺寸、工作区、窗口尺寸
    /// - PlacementWin32Api: Win32 API 封装，提供 GetSystemMetrics、SystemParametersInfo
    /// 
    /// 【潜在副作用】
    /// 1. 修改 WindowLayoutState 的所有属性
    /// 2. 调用 Win32 API 获取系统信息
    /// 
    /// 【重构风险点】
    /// 1. 窗口宽度计算：
    ///    - 当前为工作区宽度的 1/3
    ///    - 如果修改比例，需要考虑不同屏幕尺寸的适配
    /// 2. 边距设置：
    ///    - 当前为 10px
    ///    - 如果修改边距，需要同步修改固定模式的边距
    /// 3. 最小窗口宽度：
    ///    - 当前为 380px（在 WindowLayoutState 中定义）
    ///    - 如果修改最小宽度，需要考虑 UI 布局的适配
    /// 4. 工作区获取：
    ///    - 使用 SystemParametersInfo(SPI_GETWORKAREA) 获取工作区
    ///    - 工作区不包括任务栏，确保窗口不被任务栏遮挡
    /// 5. PrepareForHide 的位置修正：
    ///    - 如果 CurrentX 为 0，说明窗口位置异常，需要修正
    ///    - 如果不修正，动画起始位置错误，导致闪烁
    /// </summary>
    internal sealed class WindowLayoutService
    {
        /// <summary>
        /// 创建初始布局状态
        /// 
        /// 【调用时机】
        /// WindowHostController 构造函数中调用
        /// 
        /// 【返回值】
        /// 包含屏幕尺寸、工作区、窗口尺寸的布局状态
        /// </summary>
        public WindowLayoutState CreateInitialState()
        {
            var state = new WindowLayoutState();
            Refresh(state);
            return state;
        }

        /// <summary>
        /// 刷新布局信息 - 重新计算屏幕尺寸、工作区、窗口尺寸
        ///
        /// 【调用时机】
        /// - 初始化时（hwnd 为 Zero，回退到 Win32 主显示器）
        /// - 屏幕分辨率变化时
        /// - 工作区变化时（任务栏位置/尺寸变化）
        /// - 显示/隐藏动画前
        ///
        /// 【核心逻辑】
        /// 1. 优先用 DisplayArea（多显示器感知，物理像素，WinUI 3 原生）
        /// 2. hwnd 为 Zero 时回退到 GetSystemMetrics + SystemParametersInfo
        /// 3. 计算可用宽度（工作区宽度 - 边距）
        /// 4. 计算窗口宽度（可用宽度的 1/3，不小于最小宽度）
        /// 5. 计算窗口高度（工作区高度 - 边距）
        /// 6. 计算目标位置（右侧边缘 - 窗口宽度 - 边距）
        /// </summary>
        public void Refresh(WindowLayoutState state, IntPtr hwnd = default)
        {
            if (hwnd != IntPtr.Zero)
            {
                // DisplayArea：多显示器感知，自动跟随窗口所在屏幕
                var windowId  = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var display   = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
                var workArea  = display.WorkArea; // RectInt32，物理像素，已排除任务栏

                state.ScreenWidth  = display.OuterBounds.Width;
                state.ScreenHeight = display.OuterBounds.Height;
                state.WorkArea.Left   = workArea.X;
                state.WorkArea.Top    = workArea.Y;
                state.WorkArea.Right  = workArea.X + workArea.Width;
                state.WorkArea.Bottom = workArea.Y + workArea.Height;
            }
            else
            {
                // 回退：窗口尚未创建时使用主显示器数据
                state.ScreenHeight = PlacementWin32Api.GetSystemMetrics(PlacementWin32Api.SM_CYSCREEN);
                state.ScreenWidth  = PlacementWin32Api.GetSystemMetrics(PlacementWin32Api.SM_CXSCREEN);
                PlacementWin32Api.SystemParametersInfo(PlacementWin32Api.SPI_GETWORKAREA, 0, ref state.WorkArea, 0);
            }

            int availableWidth = state.WorkArea.Right - state.WorkArea.Left - (state.Margin * 2);
            if (state.WindowWidth <= 0)
            {
                state.WindowWidth = availableWidth / 3;
            }

            state.WindowWidth  = Math.Max(state.MinWindowWidth, state.WindowWidth);
            state.WindowWidth  = Math.Min(availableWidth, state.WindowWidth);
            state.WindowHeight = state.WorkArea.Bottom - state.WorkArea.Top - (state.Margin * 2);
            state.TargetX      = state.WorkArea.Right - state.WindowWidth - state.Margin;
            state.TargetY      = state.WorkArea.Top + state.Margin;
            state.CurrentY     = state.TargetY;
        }

        /// <summary>
        /// 准备显示动画 - 设置窗口到屏幕外
        /// 
        /// 【调用时机】
        /// 显示动画开始前调用
        /// 
        /// 【核心逻辑】
        /// 1. 刷新布局信息
        /// 2. 设置 CurrentX 为屏幕外（ScreenWidth）
        /// 3. 动画控制器从 CurrentX 滑动到 TargetX
        /// </summary>
        public void PrepareForShow(WindowLayoutState state, IntPtr hwnd = default)
        {
            Refresh(state, hwnd);
            state.CurrentX = state.ScreenWidth;
        }

        /// <summary>
        /// 准备隐藏动画 - 修正窗口位置
        /// 
        /// 【调用时机】
        /// 隐藏动画开始前调用
        /// 
        /// 【核心逻辑】
        /// 1. 刷新布局信息
        /// 2. 如果 CurrentX 为 0（异常位置），修正为 TargetX
        /// 3. 动画控制器从 CurrentX 滑动到屏幕外
        /// 
        /// 【设计原因】
        /// 为什么需要位置修正？
        /// - 如果窗口位置异常（CurrentX=0），动画起始位置错误
        /// - 修正后动画从正确位置开始，避免闪烁
        /// </summary>
        public void PrepareForHide(WindowLayoutState state, IntPtr hwnd = default)
        {
            Refresh(state, hwnd);

            if (state.CurrentX == 0)
            {
                state.CurrentX = state.TargetX;
            }
        }
    }
}
