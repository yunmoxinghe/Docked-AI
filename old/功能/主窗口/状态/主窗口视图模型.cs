using System;
using Docked_AI.Features.MainWindow.State;

namespace Docked_AI.Features.MainWindow.State
{
    /// <summary>
    /// 主窗口视图模型 - 状态容器，连接状态管理器和 UI
    /// 
    /// 【文件职责】
    /// 1. 作为 UI 绑定的数据源，持有 CurrentState 属性
    /// 2. 订阅 WindowStateManager 的状态变化事件，同步状态到 UI
    /// 3. 提供兼容性属性和方法，支持旧代码迁移
    /// 
    /// 【架构设计】
    /// 重构后的职责分离：
    /// - WindowStateManager: 状态转换逻辑（状态机、转换验证、历史记录）
    /// - MainWindowViewModel: 状态容器（UI 绑定、属性通知）
    /// - WindowHostController: 状态转换执行器（动画、样式、布局）
    /// 
    /// 为什么不让 ViewModel 持有 StateManager？
    /// 1. 单一职责：ViewModel 只负责 UI 绑定，不负责状态转换逻辑
    /// 2. 依赖反转：ViewModel 订阅 StateManager 事件，而不是直接调用
    /// 3. 测试友好：可以独立测试 StateManager 和 ViewModel
    /// 
    /// 【核心逻辑流程】
    /// 初始化阶段：
    ///   1. Controller 创建 StateManager
    ///   2. Controller 调用 ViewModel.SubscribeToStateManager()
    ///   3. ViewModel 订阅 StateManager.StateChanged 事件
    ///   4. ViewModel 同步初始状态到 CurrentState
    /// 
    /// 运行时状态同步：
    ///   1. StateManager 提交状态转换（CommitTransition）
    ///   2. StateManager 触发 StateChanged 事件
    ///   3. ViewModel.OnStateChanged 更新 CurrentState
    ///   4. CurrentState 属性变化触发 PropertyChanged 事件
    ///   5. UI 绑定自动更新（图标、圆角、边距）
    /// 
    /// 【关键依赖关系】
    /// - WindowStateManager: 状态管理器，提供状态变化事件
    /// - ObservableObject: 基类，提供 INotifyPropertyChanged 实现
    /// - MainWindow: 订阅 PropertyChanged 事件，刷新 UI
    /// 
    /// 【潜在副作用】
    /// 1. CurrentState 变化触发 PropertyChanged 事件（UI 更新）
    /// 2. 订阅 StateManager 事件（必须在 Dispose 时取消订阅）
    /// 
    /// 【重构风险点】
    /// 1. 兼容性方法（MarkVisible、MarkHidden、SetDockPinned）：
    ///    - 这些方法仅用于保持 API 兼容性，不执行实际操作
    ///    - 实际状态转换由 WindowHostController 通过 WindowStateManager 处理
    ///    - 如果删除这些方法，旧代码可能编译失败
    /// 2. 订阅/取消订阅必须成对：
    ///    - SubscribeToStateManager 和 UnsubscribeFromStateManager 必须成对调用
    ///    - 否则导致内存泄漏或事件重复触发
    /// 3. CurrentState 的初始值：
    ///    - 默认为 NotCreated，必须在订阅后同步 StateManager 的初始状态
    ///    - 否则 UI 显示的状态与实际状态不一致
    /// </summary>
    public sealed class MainWindowViewModel : ObservableObject
    {
        private WindowState _currentState = WindowState.NotCreated;

        /// <summary>
        /// 当前窗口状态
        /// </summary>
        public WindowState CurrentState
        {
            get => _currentState;
            private set => SetProperty(ref _currentState, value);
        }

        /// <summary>
        /// 兼容性属性：窗口是否可见
        /// 映射到新的状态系统
        /// </summary>
        public bool IsWindowVisible => CurrentState != WindowState.Hidden && CurrentState != WindowState.NotCreated;

        /// <summary>
        /// 兼容性属性：窗口是否固定
        /// 映射到新的状态系统
        /// </summary>
        public bool IsDockPinned => CurrentState == WindowState.Pinned;

        /// <summary>
        /// 兼容性方法：标记窗口为可见
        /// 映射到新的状态系统（仅用于测试兼容性）
        /// </summary>
        public void MarkVisible()
        {
            // 此方法仅用于保持 API 兼容性
            // 实际的状态转换由 WindowHostController 通过 WindowStateManager 处理
        }

        /// <summary>
        /// 兼容性方法：标记窗口为隐藏
        /// 映射到新的状态系统（仅用于测试兼容性）
        /// </summary>
        public void MarkHidden()
        {
            // 此方法仅用于保持 API 兼容性
            // 实际的状态转换由 WindowHostController 通过 WindowStateManager 处理
        }

        /// <summary>
        /// 兼容性方法：设置固定状态
        /// 映射到新的状态系统（仅用于测试兼容性）
        /// </summary>
        public void SetDockPinned(bool pinned)
        {
            // 此方法仅用于保持 API 兼容性
            // 实际的状态转换由 WindowHostController 通过 WindowStateManager 处理
        }

        /// <summary>
        /// 订阅 WindowStateManager 的状态变化事件
        /// 由 WindowHostController 调用
        /// </summary>
        /// <param name="stateManager">状态管理器实例</param>
        public void SubscribeToStateManager(WindowStateManager stateManager)
        {
            if (stateManager == null)
            {
                throw new ArgumentNullException(nameof(stateManager));
            }

            stateManager.StateChanged += OnStateChanged;

            // 同步初始状态
            CurrentState = stateManager.CurrentState;
        }

        /// <summary>
        /// 取消订阅 WindowStateManager 的状态变化事件
        /// 由 WindowHostController 调用
        /// </summary>
        /// <param name="stateManager">状态管理器实例</param>
        public void UnsubscribeFromStateManager(WindowStateManager stateManager)
        {
            if (stateManager != null)
            {
                stateManager.StateChanged -= OnStateChanged;
            }
        }

        /// <summary>
        /// 状态变化事件处理器
        /// 更新 CurrentState 属性，触发 UI 绑定更新
        /// </summary>
        private void OnStateChanged(object? sender, StateChangedEventArgs args)
        {
            CurrentState = args.CurrentState;
        }
    }
}
