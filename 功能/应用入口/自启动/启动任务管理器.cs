using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace Docked_AI.Features.AppEntry.AutoLaunch
{
    /// <summary>
    /// 管理应用的启动任务，封装 Windows.ApplicationModel.StartupTask API
    /// </summary>
    public class StartupTaskManager
    {
        private const string TaskId = "AppStartupTask";
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 获取当前启动任务状态
        /// </summary>
        /// <returns>启动任务状态</returns>
        public async Task<StartupTaskState> GetStateAsync()
        {
            var task = await StartupTask.GetAsync(TaskId);
            return task.State;
        }

        /// <summary>
        /// 请求启用自启动功能
        /// 使用信号量避免并发调用，防止快速切换时的竞态条件
        /// </summary>
        /// <returns>操作后的状态</returns>
        public async Task<StartupTaskState> RequestEnableAsync()
        {
            // 尝试获取锁，如果已有操作在进行中，直接返回当前状态
            if (!await _operationLock.WaitAsync(0))
            {
                // 已有操作在进行中，返回当前状态避免并发调用
                return await GetStateAsync();
            }

            try
            {
                var task = await StartupTask.GetAsync(TaskId);
                var result = await task.RequestEnableAsync();
                return result;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// 禁用自启动功能
        /// 使用信号量避免并发调用
        /// 注意：内部调用 StartupTask.Disable() 是同步方法，不会触发系统对话框
        /// </summary>
        /// <returns>禁用后的状态</returns>
        public async Task<StartupTaskState> DisableAsync()
        {
            // 尝试获取锁，如果已有操作在进行中，等待完成
            await _operationLock.WaitAsync();

            try
            {
                var task = await StartupTask.GetAsync(TaskId);
                task.Disable(); // 同步调用，立即生效
                return task.State; // 直接返回状态，避免额外的系统调用
            }
            finally
            {
                _operationLock.Release();
            }
        }

        /// <summary>
        /// 检查是否可以修改自启动状态
        /// DisabledByUser 状态下返回 false，因为必须在系统设置中启用
        /// DisabledByPolicy 状态下返回 false，因为被组策略限制
        /// </summary>
        /// <param name="currentState">当前状态</param>
        /// <returns>是否可修改</returns>
        public bool CanModifyState(StartupTaskState currentState)
        {
            return currentState != StartupTaskState.DisabledByUser &&
                   currentState != StartupTaskState.DisabledByPolicy;
        }
    }
}
