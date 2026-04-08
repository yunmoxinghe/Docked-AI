using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Docked_AI.功能.主窗口v2.状态机;
using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.动画系统.策略;
using Docked_AI.功能.主窗口v2.服务层;

namespace Docked_AI.功能.主窗口v2.初始化;

/// <summary>
/// 窗口初始化器
/// 负责在 Activate() 之前完成所有初始化设置，避免闪烁
/// 
/// 使用方式：
/// 1. 使用 MainWindowFactory.Create() 创建窗口（不激活）
/// 2. 调用 WindowInitializer.Initialize(window) 完成初始化
/// 3. 调用 window.Activate() 显示窗口
/// 4. 在第一帧后调用 stateManager.TransitionTo(目标状态)
/// </summary>
public static class WindowInitializer
{
    /// <summary>
    /// 初始化窗口及其状态管理器
    /// 在 Activate() 之前调用，完成所有依赖注入和配置
    /// </summary>
    /// <param name="window">要初始化的窗口</param>
    /// <param name="startHidden">是否启动时隐藏（托盘应用场景）</param>
    /// <returns>初始化后的状态管理器</returns>
    public static WindowStateManager Initialize(Window window, bool startHidden = false)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        // 1. 创建 WindowContext（集中管理 HWND 和核心引用）
        var context = new WindowContext(window);

        // 如果需要启动时隐藏，在 Activate() 之前设置 Opacity=0
        if (startHidden)
        {
            WindowService.SetOpacity(context.GetHwnd(), 0.0);
        }

        // 2. 创建 AnimationEngine（统一动画引擎）
        var animationEngine = new AnimationEngine();

        // 3. 创建 IAnimationPolicy（可选，用于统一管理动画参数）
        var animationPolicy = new DefaultAnimationPolicy();

        // 4. 创建 WindowStateManager（状态机）
        var stateManager = new WindowStateManager(
            dispatcher: DispatcherQueue.GetForCurrentThread(),
            animationEngine: animationEngine,
            context: context,
            animationPolicy: animationPolicy
        );

        return stateManager;
    }

    /// <summary>
    /// 在第一帧后转换到目标状态
    /// 应该在 window.Activate() 之后立即调用
    /// </summary>
    /// <param name="stateManager">状态管理器</param>
    /// <param name="targetState">目标状态</param>
    /// <param name="startHidden">是否启动时隐藏</param>
    public static void TransitionAfterFirstFrame(
        WindowStateManager stateManager,
        WindowState targetState,
        bool startHidden = false)
    {
        if (stateManager == null)
            throw new ArgumentNullException(nameof(stateManager));

        // 使用 DispatcherQueue 在第一帧完成后执行状态转换
        DispatcherQueue.GetForCurrentThread().TryEnqueue(
            DispatcherQueuePriority.High,
            () =>
            {
                if (startHidden)
                {
                    // 启动时隐藏：先转换到 Hidden 状态
                    stateManager.TransitionTo(WindowState.Hidden);
                }
                else
                {
                    // 正常启动：转换到目标状态
                    stateManager.TransitionTo(targetState);
                }
            });
    }

    /// <summary>
    /// 完整的初始化流程（推荐使用）
    /// 包含创建窗口、初始化、激活、转换状态的完整流程
    /// </summary>
    /// <param name="windowFactory">窗口工厂函数</param>
    /// <param name="targetState">目标状态</param>
    /// <param name="startHidden">是否启动时隐藏</param>
    /// <returns>初始化后的窗口和状态管理器</returns>
    public static (Window window, WindowStateManager stateManager) InitializeAndActivate(
        Func<Window> windowFactory,
        WindowState targetState = WindowState.Floating,
        bool startHidden = false)
    {
        if (windowFactory == null)
            throw new ArgumentNullException(nameof(windowFactory));

        // 1. 使用工厂创建窗口（不激活）
        var window = windowFactory();

        // 2. 初始化窗口和状态管理器
        var stateManager = Initialize(window, startHidden);

        // 3. 激活窗口（WinUI3 强制显示）
        window.Activate();

        // 4. 在第一帧后转换到目标状态
        TransitionAfterFirstFrame(stateManager, targetState, startHidden);

        return (window, stateManager);
    }
}
