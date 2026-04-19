using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace Docked_AI.Features.MainWindow.Entry
{
    /// <summary>
    /// 主窗口工厂类 - 统一管理主窗口实例的创建和验证
    /// 
    /// 【文件职责】
    /// 1. 提供主窗口创建的统一入口，避免直接 new MainWindow()
    /// 2. 封装窗口有效性检查逻辑，防止使用已释放的窗口
    /// 3. 支持"获取或创建"模式，复用有效窗口或创建新窗口
    /// 
    /// 【核心设计原则】
    /// 为什么需要工厂类？
    /// 1. 窗口创建时机控制：避免在构造函数中调用 Activate()，由调用方控制
    /// 2. 窗口有效性验证：窗口关闭后句柄失效，需要检查后再使用
    /// 3. 单一职责：将窗口创建逻辑从托盘管理器中分离
    /// 
    /// 【关键依赖关系】
    /// - MainWindow: 被创建的窗口类
    /// - TrayIconManager: 主要调用方，用于创建和管理窗口
    /// - WinRT.Interop.WindowNative: 用于获取窗口句柄
    /// 
    /// 【潜在副作用】
    /// 1. Create() 方法创建新窗口但不激活，窗口不可见
    /// 2. CreateAndActivate() 方法已废弃，不应使用（会导致闪烁）
    /// 3. IsWindowValid() 方法访问窗口句柄，可能抛出异常
    /// 
    /// 【重构风险点】
    /// 1. 不要在工厂方法中调用 Activate()：
    ///    - 窗口激活应由 WindowHostController.RequestSlideIn() 控制
    ///    - 过早激活会导致窗口在未完成布局配置时显示，产生闪烁
    /// 2. IsWindowValid() 的检查逻辑：
    ///    - 必须检查窗口句柄和 Content，两者缺一不可
    ///    - 如果只检查句柄，可能返回已关闭但句柄未释放的窗口
    /// 3. GetOrCreate() 的复用逻辑：
    ///    - 必须先验证窗口有效性，再决定是否复用
    ///    - 如果复用无效窗口，会导致 UI 无响应
    /// </summary>
    public static class MainWindowFactory
    {
        /// <summary>
        /// 创建并激活主窗口（已废弃，不推荐使用）
        /// 
        /// 【废弃原因】
        /// 在工厂方法中调用 Activate() 会导致窗口在未完成布局配置时显示，产生闪烁
        /// 应使用 Create() 创建窗口，然后由 WindowHostController.RequestSlideIn() 控制激活时机
        /// 
        /// 【保留原因】
        /// 为了向后兼容，保留此方法，但不推荐使用
        /// </summary>
        /// <returns>新创建的主窗口实例</returns>
        public static global::Docked_AI.MainWindow CreateAndActivate()
        {
            var window = new global::Docked_AI.MainWindow();
            
            // 不在这里调用 Activate()，避免闪烁
            // 让调用方（托盘图标管理器）在准备好后再激活
            
            return window;
        }

        /// <summary>
        /// 创建主窗口但不激活（推荐使用）
        /// 
        /// 【设计原因】
        /// 窗口创建和激活应分离：
        /// 1. 创建阶段：初始化 ViewModel、Controller、事件订阅
        /// 2. 配置阶段：设置窗口位置、大小、样式（由 WindowHostController 处理）
        /// 3. 激活阶段：调用 Activate() 显示窗口（由 RequestSlideIn 处理）
        /// 
        /// 【使用场景】
        /// - 托盘管理器创建窗口后，调用 SetInitializingComplete() 和 RequestSlideIn()
        /// - 避免在窗口未完成配置时显示，产生闪烁
        /// </summary>
        /// <returns>新创建的主窗口实例</returns>
        public static global::Docked_AI.MainWindow Create()
        {
            return new global::Docked_AI.MainWindow();
        }

        /// <summary>
        /// 获取或创建主窗口 - 复用有效窗口或创建新窗口
        /// 
        /// 【使用场景】
        /// 托盘管理器在显示窗口前检查现有窗口是否有效：
        /// - 如果有效，复用现有窗口（避免重复创建）
        /// - 如果无效，创建新窗口
        /// 
        /// 【设计原因】
        /// 窗口关闭后句柄失效，但引用可能仍然存在
        /// 必须验证窗口有效性，避免使用已释放的窗口
        /// </summary>
        /// <param name="existingWindow">现有窗口引用</param>
        /// <returns>有效的主窗口实例</returns>
        public static global::Docked_AI.MainWindow GetOrCreate(Window? existingWindow)
        {
            if (IsWindowValid(existingWindow))
            {
                return (global::Docked_AI.MainWindow)existingWindow!;
            }

            return Create();
        }

        /// <summary>
        /// 检查窗口是否有效 - 验证窗口句柄和内容是否存在
        /// 
        /// 【检查逻辑】
        /// 1. 窗口引用不为 null
        /// 2. 窗口句柄不为 IntPtr.Zero（窗口未关闭）
        /// 3. 窗口内容不为 null（窗口已初始化）
        /// 
        /// 【设计原因】
        /// 窗口关闭后，引用可能仍然存在，但句柄已失效
        /// 必须同时检查句柄和内容，确保窗口可用
        /// 
        /// 【重构风险】
        /// 如果只检查句柄，可能返回已关闭但句柄未释放的窗口
        /// 如果只检查内容，可能返回句柄已失效的窗口
        /// </summary>
        /// <param name="window">要检查的窗口</param>
        /// <returns>窗口是否有效</returns>
        public static bool IsWindowValid(Window? window)
        {
            if (window == null)
            {
                return false;
            }

            try
            {
                // 检查窗口句柄是否有效
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                if (windowHandle == IntPtr.Zero)
                {
                    return false;
                }

                // 检查窗口内容是否存在
                return window.Content != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
