using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Docked_AI.Features.MainWindow.State;

namespace Docked_AI.Features.AppEntry.AutoLaunch
{
    /// <summary>
    /// 自启动设置的 ViewModel，管理 UI 状态和用户交互
    /// </summary>
    public class StartupSettingsViewModel : ObservableObject
    {
        private readonly StartupTaskManager _startupManager;
        private StartupTaskState _currentState;
        private bool _isOperationInProgress;

        public StartupSettingsViewModel(StartupTaskManager startupManager)
        {
            _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
            _currentState = StartupTaskState.Disabled;
            _isOperationInProgress = false;
        }

        /// <summary>
        /// 自启动是否已启用
        /// </summary>
        public bool IsStartupEnabled => _currentState == StartupTaskState.Enabled;

        /// <summary>
        /// 是否可以切换开关
        /// 注意：DisabledByUser 状态下为 false，因为必须在系统设置中启用
        /// 操作进行中时也为 false，防止重复操作
        /// </summary>
        public bool CanToggle => !_isOperationInProgress &&
                                 _currentState != StartupTaskState.DisabledByUser &&
                                 _currentState != StartupTaskState.DisabledByPolicy;

        /// <summary>
        /// 是否可以点击 SettingCard 跳转到系统设置
        /// DisabledByUser 状态下为 true，引导用户去系统设置启用
        /// </summary>
        public bool CanNavigateToSettings => _currentState == StartupTaskState.DisabledByUser;

        /// <summary>
        /// 是否显示组策略警告
        /// </summary>
        public bool ShowPolicyWarning => _currentState == StartupTaskState.DisabledByPolicy;

        /// <summary>
        /// 是否显示用户禁用提示信息
        /// DisabledByUser 状态下为 true
        /// </summary>
        public bool ShowUserDisabledInfo => _currentState == StartupTaskState.DisabledByUser;

        /// <summary>
        /// 当前状态的文本表示（用于调试）
        /// </summary>
        public string CurrentStateText => _currentState.ToString();

        /// <summary>
        /// 初始化并加载当前状态
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _currentState = await _startupManager.GetStateAsync();
                UpdateUIProperties();
                LogInfo($"Initialized with state: {_currentState}");
            }
            catch (Exception ex)
            {
                LogException("InitializeAsync", ex);
                // 发生异常时保持默认状态
                _currentState = StartupTaskState.Disabled;
                UpdateUIProperties();
            }
        }

        /// <summary>
        /// 处理开关切换
        /// 使用标志位防止并发操作
        /// </summary>
        public async Task HandleToggleAsync(bool isOn)
        {
            // 如果已有操作在进行中，忽略此次请求
            if (_isOperationInProgress)
            {
                LogInfo("Operation already in progress, ignoring toggle request");
                return;
            }

            _isOperationInProgress = true;
            RaisePropertyChanged(nameof(CanToggle));

            try
            {
                StartupTaskState newState;

                if (isOn)
                {
                    LogInfo("Requesting to enable startup");
                    newState = await _startupManager.RequestEnableAsync();
                }
                else
                {
                    LogInfo("Requesting to disable startup");
                    newState = await _startupManager.DisableAsync();
                }

                _currentState = newState;
                UpdateUIProperties();
                LogInfo($"Toggle completed, new state: {_currentState}");
            }
            catch (Exception ex)
            {
                LogException("HandleToggleAsync", ex);
                
                // 发生异常时刷新状态，确保 UI 与实际状态一致
                try
                {
                    _currentState = await _startupManager.GetStateAsync();
                    UpdateUIProperties();
                }
                catch (Exception refreshEx)
                {
                    LogException("HandleToggleAsync - State refresh", refreshEx);
                }
            }
            finally
            {
                _isOperationInProgress = false;
                RaisePropertyChanged(nameof(CanToggle));
            }
        }

        /// <summary>
        /// 跳转到系统设置
        /// </summary>
        public async Task NavigateToSystemSettingsAsync()
        {
            try
            {
                LogInfo("Navigating to system settings");
                var uri = new Uri("ms-settings:startupapps");
                var success = await Windows.System.Launcher.LaunchUriAsync(uri);
                
                if (success)
                {
                    LogInfo("Successfully opened system settings");
                }
                else
                {
                    LogWarning("Failed to open system settings");
                }
            }
            catch (Exception ex)
            {
                LogException("NavigateToSystemSettingsAsync", ex);
            }
        }

        /// <summary>
        /// 更新所有 UI 属性
        /// </summary>
        private void UpdateUIProperties()
        {
            RaisePropertyChanged(nameof(IsStartupEnabled));
            RaisePropertyChanged(nameof(CanToggle));
            RaisePropertyChanged(nameof(CanNavigateToSettings));
            RaisePropertyChanged(nameof(ShowPolicyWarning));
            RaisePropertyChanged(nameof(ShowUserDisabledInfo));
            RaisePropertyChanged(nameof(CurrentStateText));
        }

        /// <summary>
        /// 设置模拟状态（仅用于调试）
        /// </summary>
        public void SetDebugState(StartupTaskState state)
        {
            _currentState = state;
            UpdateUIProperties();
            LogInfo($"Debug: State set to {state}");
        }

        private static void LogInfo(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [StartupSettingsViewModel] {message}");
        }

        private static void LogWarning(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [WARNING] [StartupSettingsViewModel] {message}");
        }

        private static void LogException(string source, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] [StartupSettingsViewModel] {source}\n{ex}");
        }
    }
}
