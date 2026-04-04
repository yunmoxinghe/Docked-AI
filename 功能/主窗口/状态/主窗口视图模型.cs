using System;
using Docked_AI.Features.MainWindow.State;

namespace Docked_AI.Features.MainWindow.State
{
    /// <summary>
    /// 主窗口视图模型
    /// 重构后不再持有 WindowStateManager，只订阅状态变化事件
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
