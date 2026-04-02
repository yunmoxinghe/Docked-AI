using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace Docked_AI.Features.AppEntry.AutoLaunch
{
    /// <summary>
    /// 手动测试工具，用于验证 StartupSettingsViewModel 的核心功能
    /// 这不是自动化单元测试，而是用于手动验证的辅助类
    /// </summary>
    public class StartupSettingsViewModelManualTest
    {
        private readonly StartupSettingsViewModel _viewModel;
        private readonly StartupTaskManager _manager;

        public StartupSettingsViewModelManualTest()
        {
            _manager = new StartupTaskManager();
            _viewModel = new StartupSettingsViewModel(_manager);
        }

        /// <summary>
        /// 运行所有手动测试
        /// </summary>
        public async Task RunAllTestsAsync()
        {
            Console.WriteLine("=== StartupSettingsViewModel 手动测试 ===\n");

            await TestInitializeAsync();
            await TestPropertyValuesForAllStates();
            await TestHandleToggleAsync();
            await TestConcurrentOperationPrevention();
            await TestNavigateToSystemSettingsAsync();

            Console.WriteLine("\n=== 所有测试完成 ===");
        }

        /// <summary>
        /// 测试 InitializeAsync 正确加载状态
        /// </summary>
        private async Task TestInitializeAsync()
        {
            Console.WriteLine("测试 1: InitializeAsync() - 正确加载状态");
            try
            {
                await _viewModel.InitializeAsync();
                
                var actualState = await _manager.GetStateAsync();
                var expectedIsEnabled = actualState == StartupTaskState.Enabled;
                
                Console.WriteLine($"  实际系统状态: {actualState}");
                Console.WriteLine($"  ViewModel.IsStartupEnabled: {_viewModel.IsStartupEnabled}");
                Console.WriteLine($"  ViewModel.CanToggle: {_viewModel.CanToggle}");
                Console.WriteLine($"  ViewModel.CanNavigateToSettings: {_viewModel.CanNavigateToSettings}");
                Console.WriteLine($"  ViewModel.ShowPolicyWarning: {_viewModel.ShowPolicyWarning}");
                Console.WriteLine($"  ViewModel.ShowUserDisabledInfo: {_viewModel.ShowUserDisabledInfo}");
                
                if (_viewModel.IsStartupEnabled == expectedIsEnabled)
                {
                    Console.WriteLine("  ✓ IsStartupEnabled 与实际状态一致");
                }
                else
                {
                    Console.WriteLine("  ✗ IsStartupEnabled 与实际状态不一致");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 错误: {ex.Message}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 测试每个 StartupTaskState 的属性值
        /// </summary>
        private async Task TestPropertyValuesForAllStates()
        {
            Console.WriteLine("测试 2: 每个 StartupTaskState 的属性值");
            try
            {
                var currentState = await _manager.GetStateAsync();
                Console.WriteLine($"  当前系统状态: {currentState}");
                Console.WriteLine();
                
                // 显示当前状态的预期属性值
                Console.WriteLine("  当前状态的属性值:");
                DisplayExpectedProperties(currentState);
                
                Console.WriteLine("\n  所有状态的预期属性值:");
                Console.WriteLine("  ----------------------------------------");
                DisplayExpectedProperties(StartupTaskState.Enabled);
                DisplayExpectedProperties(StartupTaskState.Disabled);
                DisplayExpectedProperties(StartupTaskState.DisabledByUser);
                DisplayExpectedProperties(StartupTaskState.DisabledByPolicy);
                
                Console.WriteLine("  ✓ 属性映射规则已显示");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 错误: {ex.Message}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 测试 HandleToggleAsync 更新 UI 属性
        /// </summary>
        private async Task TestHandleToggleAsync()
        {
            Console.WriteLine("测试 3: HandleToggleAsync() - 更新 UI 属性");
            try
            {
                await _viewModel.InitializeAsync();
                
                var initialState = await _manager.GetStateAsync();
                Console.WriteLine($"  初始状态: {initialState}");
                Console.WriteLine($"  初始 IsStartupEnabled: {_viewModel.IsStartupEnabled}");
                Console.WriteLine($"  初始 CanToggle: {_viewModel.CanToggle}");
                
                if (!_viewModel.CanToggle)
                {
                    Console.WriteLine($"  ⚠ 跳过测试: 当前状态不允许切换");
                    Console.WriteLine($"  提示: {GetModificationHint(initialState)}");
                    return;
                }
                
                // 尝试切换状态
                var targetState = !_viewModel.IsStartupEnabled;
                Console.WriteLine($"\n  正在切换到: {(targetState ? "启用" : "禁用")}");
                
                await _viewModel.HandleToggleAsync(targetState);
                
                var newState = await _manager.GetStateAsync();
                Console.WriteLine($"  操作后状态: {newState}");
                Console.WriteLine($"  操作后 IsStartupEnabled: {_viewModel.IsStartupEnabled}");
                Console.WriteLine($"  操作后 CanToggle: {_viewModel.CanToggle}");
                
                Console.WriteLine("  ✓ HandleToggleAsync 执行完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 错误: {ex.Message}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 测试 HandleToggleAsync 防止并发操作
        /// </summary>
        private async Task TestConcurrentOperationPrevention()
        {
            Console.WriteLine("测试 4: HandleToggleAsync() - 防止并发操作");
            try
            {
                await _viewModel.InitializeAsync();
                
                var initialState = await _manager.GetStateAsync();
                Console.WriteLine($"  初始状态: {initialState}");
                
                if (!_viewModel.CanToggle)
                {
                    Console.WriteLine($"  ⚠ 跳过测试: 当前状态不允许切换");
                    return;
                }
                
                Console.WriteLine("  启动两个并发操作...");
                
                // 启动两个并发操作
                var task1 = _viewModel.HandleToggleAsync(true);
                var task2 = _viewModel.HandleToggleAsync(false);
                
                await Task.WhenAll(task1, task2);
                
                Console.WriteLine("  ✓ 并发操作已完成（第二个操作应该被忽略）");
                Console.WriteLine($"  最终状态: {await _manager.GetStateAsync()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 错误: {ex.Message}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 测试 NavigateToSystemSettingsAsync 打开正确的 URI
        /// </summary>
        private async Task TestNavigateToSystemSettingsAsync()
        {
            Console.WriteLine("测试 5: NavigateToSystemSettingsAsync() - 打开系统设置");
            try
            {
                Console.WriteLine("  正在尝试打开系统设置...");
                Console.WriteLine("  URI: ms-settings:startupapps");
                
                await _viewModel.NavigateToSystemSettingsAsync();
                
                Console.WriteLine("  ✓ NavigateToSystemSettingsAsync 执行完成");
                Console.WriteLine("  请检查系统设置页面是否已打开");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 错误: {ex.Message}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 显示指定状态的预期属性值
        /// </summary>
        private void DisplayExpectedProperties(StartupTaskState state)
        {
            var isEnabled = state == StartupTaskState.Enabled;
            var canToggle = state != StartupTaskState.DisabledByUser && 
                           state != StartupTaskState.DisabledByPolicy;
            var canNavigate = state == StartupTaskState.DisabledByUser;
            var showPolicy = state == StartupTaskState.DisabledByPolicy;
            var showUserDisabled = state == StartupTaskState.DisabledByUser;
            
            Console.WriteLine($"  {state}:");
            Console.WriteLine($"    IsStartupEnabled: {isEnabled}");
            Console.WriteLine($"    CanToggle: {canToggle}");
            Console.WriteLine($"    CanNavigateToSettings: {canNavigate}");
            Console.WriteLine($"    ShowPolicyWarning: {showPolicy}");
            Console.WriteLine($"    ShowUserDisabledInfo: {showUserDisabled}");
        }

        /// <summary>
        /// 获取修改提示信息
        /// </summary>
        private string GetModificationHint(StartupTaskState state)
        {
            return state switch
            {
                StartupTaskState.DisabledByUser => "请在 Windows 设置 > 应用 > 启动 中手动启用",
                StartupTaskState.DisabledByPolicy => "请联系系统管理员修改组策略",
                _ => "状态正常"
            };
        }
    }
}
