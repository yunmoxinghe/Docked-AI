using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace Docked_AI.Features.AppEntry.AutoLaunch
{
    /// <summary>
    /// 手动测试工具，用于验证 StartupTaskManager 的核心功能
    /// 这不是自动化单元测试，而是用于手动验证的辅助类
    /// </summary>
    public class StartupTaskManagerManualTest
    {
        private readonly StartupTaskManager _manager;

        public StartupTaskManagerManualTest()
        {
            _manager = new StartupTaskManager();
        }

        /// <summary>
        /// 运行所有手动测试
        /// </summary>
        public async Task RunAllTestsAsync()
        {
            Console.WriteLine("=== StartupTaskManager 手动测试 ===\n");

            await TestGetStateAsync();
            await TestCanModifyState();
            await TestRequestEnableAsync();
            await TestDisableAsync();

            Console.WriteLine("\n=== 所有测试完成 ===");
        }

        /// <summary>
        /// 测试 GetStateAsync 方法
        /// </summary>
        private async Task TestGetStateAsync()
        {
            Console.WriteLine("测试 1: GetStateAsync()");
            try
            {
                var state = await _manager.GetStateAsync();
                Console.WriteLine($"  ✓ 当前状态: {state}");
                Console.WriteLine($"  状态说明: {GetStateDescription(state)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 错误: {ex.Message}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 测试 CanModifyState 方法
        /// </summary>
        private async Task TestCanModifyState()
        {
            Console.WriteLine("测试 2: CanModifyState()");
            try
            {
                var state = await _manager.GetStateAsync();
                var canModify = _manager.CanModifyState(state);
                Console.WriteLine($"  当前状态: {state}");
                Console.WriteLine($"  ✓ 是否可修改: {canModify}");
                
                // 测试所有可能的状态
                Console.WriteLine("  所有状态的可修改性:");
                Console.WriteLine($"    Enabled: {_manager.CanModifyState(StartupTaskState.Enabled)}");
                Console.WriteLine($"    Disabled: {_manager.CanModifyState(StartupTaskState.Disabled)}");
                Console.WriteLine($"    DisabledByUser: {_manager.CanModifyState(StartupTaskState.DisabledByUser)}");
                Console.WriteLine($"    DisabledByPolicy: {_manager.CanModifyState(StartupTaskState.DisabledByPolicy)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 错误: {ex.Message}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 测试 RequestEnableAsync 方法
        /// 注意：这可能会显示系统权限对话框
        /// </summary>
        private async Task TestRequestEnableAsync()
        {
            Console.WriteLine("测试 3: RequestEnableAsync()");
            try
            {
                var initialState = await _manager.GetStateAsync();
                Console.WriteLine($"  初始状态: {initialState}");

                if (!_manager.CanModifyState(initialState))
                {
                    Console.WriteLine($"  ⚠ 跳过测试: 当前状态不允许修改");
                    Console.WriteLine($"  提示: {GetModificationHint(initialState)}");
                    return;
                }

                Console.WriteLine("  正在请求启用...");
                var newState = await _manager.RequestEnableAsync();
                Console.WriteLine($"  ✓ 操作后状态: {newState}");
                Console.WriteLine($"  状态说明: {GetStateDescription(newState)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 错误: {ex.Message}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 测试 DisableAsync 方法
        /// </summary>
        private async Task TestDisableAsync()
        {
            Console.WriteLine("测试 4: DisableAsync()");
            try
            {
                var initialState = await _manager.GetStateAsync();
                Console.WriteLine($"  初始状态: {initialState}");

                if (!_manager.CanModifyState(initialState))
                {
                    Console.WriteLine($"  ⚠ 跳过测试: 当前状态不允许修改");
                    Console.WriteLine($"  提示: {GetModificationHint(initialState)}");
                    return;
                }

                Console.WriteLine("  正在禁用...");
                var newState = await _manager.DisableAsync();
                Console.WriteLine($"  ✓ 操作后状态: {newState}");
                Console.WriteLine($"  状态说明: {GetStateDescription(newState)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 错误: {ex.Message}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 获取状态的描述信息
        /// </summary>
        private string GetStateDescription(StartupTaskState state)
        {
            return state switch
            {
                StartupTaskState.Enabled => "已启用 - 应用将在系统启动时自动运行",
                StartupTaskState.Disabled => "已禁用 - 可以请求启用",
                StartupTaskState.DisabledByUser => "被用户禁用 - 需要在系统设置中重新启用",
                StartupTaskState.DisabledByPolicy => "被组策略禁用 - 无法启用",
                _ => "未知状态"
            };
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
