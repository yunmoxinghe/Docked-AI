using DockedTools.Features.MainWindow.State;
using System;

namespace DockedTools.Features.UnifiedCalls.MainWindow
{
    /// <summary>
    /// 主窗口统一调用服务
    /// 提供主窗口状态查询、状态变更请求、状态变化通知订阅等功能
    /// 
    /// 【服务职责】
    /// 1. 状态查询服务：检查当前窗口状态
    /// 2. 状态变更服务：申请改变窗口状态（显示/隐藏、固定/取消固定、最大化/还原）
    /// 3. 状态通知服务：订阅窗口状态变化事件
    /// 4. 窗口生命周期管理：背景切换、透明度设置等
    /// 
    /// 【使用示例】
    /// ```csharp
    /// // 检查当前状态
    /// var state = MainWindowService.CurrentState;
    /// bool isVisible = MainWindowService.IsVisible;
    /// bool isPinned = MainWindowService.IsPinned;
    /// 
    /// // 订阅状态变化
    /// MainWindowService.StateChanged += (sender, args) => {
    ///     Console.WriteLine($"窗口状态从 {args.PreviousState} 变为 {args.CurrentState}");
    /// };
    /// 
    /// // 申请改变状态
    /// MainWindowService.RequestToggleWindow();      // 切换显示/隐藏
    /// MainWindowService.RequestTogglePinned();      // 切换固定/取消固定
    /// MainWindowService.RequestToggleMaximize();    // 切换最大化/还原
    /// MainWindowService.RequestShow();              // 显示窗口
    /// MainWindowService.RequestHide();              // 隐藏窗口
    /// ```
    /// 
    /// 【架构设计】
    /// - 服务作为全局单例，通过静态方法访问
    /// - 不直接持有窗口引用，而是通过接口 IWindowController 解耦
    /// - 状态管理器负责状态逻辑，控制器负责执行动作
    /// - 视图模型负责 UI 绑定和属性通知
    /// </summary>
    public static class MainWindowService
    {
        private static IWindowController? _windowController;
        private static MainWindowViewModel? _viewModel;

        /// <summary>
        /// 注册主窗口控制器（由主窗口初始化时调用）
        /// </summary>
        public static void Register(IWindowController windowController, MainWindowViewModel viewModel)
        {
            _windowController = windowController ?? throw new ArgumentNullException(nameof(windowController));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            
            System.Diagnostics.Debug.WriteLine("[MainWindowService] 主窗口控制器已注册");
        }

        /// <summary>
        /// 取消注册主窗口控制器
        /// </summary>
        public static void Unregister()
        {
            _windowController = null;
            _viewModel = null;
            System.Diagnostics.Debug.WriteLine("[MainWindowService] 主窗口控制器已取消注册");
        }

        #region 状态查询服务

        /// <summary>
        /// 获取当前窗口状态
        /// </summary>
        public static WindowState CurrentState
        {
            get
            {
                if (_viewModel == null)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindowService] ViewModel 未注册，返回 NotCreated");
                    return WindowState.NotCreated;
                }
                return _viewModel.CurrentState;
            }
        }

        /// <summary>
        /// 窗口是否可见（非隐藏状态）
        /// </summary>
        public static bool IsVisible => _viewModel?.IsWindowVisible ?? false;

        /// <summary>
        /// 窗口是否处于固定模式
        /// </summary>
        public static bool IsPinned => _viewModel?.IsDockPinned ?? false;

        /// <summary>
        /// 窗口是否处于最大化状态
        /// </summary>
        public static bool IsMaximized => CurrentState == WindowState.Maximized;

        /// <summary>
        /// 窗口是否处于窗口化模式
        /// </summary>
        public static bool IsWindowed => CurrentState == WindowState.Windowed;

        /// <summary>
        /// 窗口是否已隐藏
        /// </summary>
        public static bool IsHidden => CurrentState == WindowState.Hidden;

        #endregion

        #region 状态变更请求服务

        /// <summary>
        /// 申请切换窗口显示/隐藏状态
        /// 如果当前可见，则隐藏；如果当前隐藏，则显示
        /// </summary>
        public static void RequestToggleWindow()
        {
            EnsureRegistered();
            _windowController?.ToggleWindow();
        }

        /// <summary>
        /// 申请切换固定/取消固定状态
        /// </summary>
        public static void RequestTogglePinned()
        {
            EnsureRegistered();
            _windowController?.TogglePinnedDock();
        }

        /// <summary>
        /// 申请切换最大化/还原状态
        /// </summary>
        public static void RequestToggleMaximize()
        {
            EnsureRegistered();
            _windowController?.ToggleMaximize();
        }

        /// <summary>
        /// 申请显示窗口（如果当前隐藏）
        /// </summary>
        public static void RequestShow()
        {
            EnsureRegistered();
            if (IsHidden)
            {
                _windowController?.ToggleWindow();
            }
        }

        /// <summary>
        /// 申请隐藏窗口（如果当前可见且未固定）
        /// </summary>
        public static void RequestHide()
        {
            EnsureRegistered();
            if (IsVisible && !IsPinned)
            {
                _windowController?.ToggleWindow();
            }
        }

        /// <summary>
        /// 申请设置窗口为固定模式
        /// </summary>
        public static void RequestPin()
        {
            EnsureRegistered();
            if (!IsPinned)
            {
                _windowController?.TogglePinnedDock();
            }
        }

        /// <summary>
        /// 申请取消窗口固定模式
        /// </summary>
        public static void RequestUnpin()
        {
            EnsureRegistered();
            if (IsPinned)
            {
                _windowController?.TogglePinnedDock();
            }
        }

        /// <summary>
        /// 申请最大化窗口
        /// </summary>
        public static void RequestMaximize()
        {
            EnsureRegistered();
            if (!IsMaximized)
            {
                _windowController?.ToggleMaximize();
            }
        }

        /// <summary>
        /// 申请还原窗口（从最大化状态）
        /// </summary>
        public static void RequestRestore()
        {
            EnsureRegistered();
            if (IsMaximized)
            {
                _windowController?.ToggleMaximize();
            }
        }

        #endregion

        #region 状态通知服务

        /// <summary>
        /// 窗口状态变化事件（动画开始时触发）
        /// 订阅此事件以接收窗口状态开始变化通知
        /// </summary>
        public static event EventHandler<StateChangedEventArgs>? StateChanged
        {
            add
            {
                if (_windowController != null)
                {
                    _windowController.StateChanged += value;
                }
            }
            remove
            {
                if (_windowController != null)
                {
                    _windowController.StateChanged -= value;
                }
            }
        }

        /// <summary>
        /// 窗口状态变化完成事件（动画播放完成后触发）
        /// 订阅此事件以接收窗口状态完成变化通知
        /// ⭐ 推荐使用此事件而非 StateChanged，因为动画完成后才触发
        /// </summary>
        public static event EventHandler<StateCompletedEventArgs>? StateCompleted
        {
            add
            {
                if (_windowController != null)
                {
                    _windowController.StateCompleted += value;
                }
            }
            remove
            {
                if (_windowController != null)
                {
                    _windowController.StateCompleted -= value;
                }
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 确保服务已注册，否则抛出异常
        /// </summary>
        private static void EnsureRegistered()
        {
            if (_windowController == null)
            {
                throw new InvalidOperationException(
                    "主窗口服务未注册。请确保在主窗口初始化时调用 MainWindowService.Register()");
            }
        }

        #endregion
    }

    /// <summary>
    /// 窗口控制器接口
    /// 用于解耦服务和具体实现
    /// </summary>
    public interface IWindowController
    {
        /// <summary>
        /// 切换窗口显示/隐藏状态
        /// </summary>
        void ToggleWindow();

        /// <summary>
        /// 切换固定/取消固定状态
        /// </summary>
        void TogglePinnedDock();

        /// <summary>
        /// 切换最大化/还原状态
        /// </summary>
        void ToggleMaximize();

        /// <summary>
        /// 窗口状态变化事件（动画开始时触发）
        /// </summary>
        event EventHandler<StateChangedEventArgs>? StateChanged;

        /// <summary>
        /// 窗口状态变化完成事件（动画播放完成后触发）
        /// </summary>
        event EventHandler<StateCompletedEventArgs>? StateCompleted;
    }
}
