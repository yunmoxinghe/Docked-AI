using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace Docked_AI.Features.AppEntry.AutoLaunch
{
    /// <summary>
    /// 手动测试工具，用于验证 AutoLaunchHandler 的自启动检测功能
    /// </summary>
    public class AutoLaunchHandlerManualTest
    {
        /// <summary>
        /// 运行自启动检测测试
        /// </summary>
        public static void RunAutoLaunchDetectionTest()
        {
            Console.WriteLine("=== AutoLaunchHandler 自启动检测测试 ===\n");

            try
            {
                // 创建一个临时的 App 实例用于测试
                // 注意：在实际应用中，App 实例已经存在
                var app = Microsoft.UI.Xaml.Application.Current as App;
                if (app == null)
                {
                    Console.WriteLine("  ✗ 错误: 无法获取 App 实例");
                    return;
                }

                var handler = new AutoLaunchHandler(app);

                // 测试 IsAutoLaunch 方法
                Console.WriteLine("测试 1: IsAutoLaunch() 检测");
                var isAutoLaunch = handler.IsAutoLaunch();
                Console.WriteLine($"  ✓ 检测结果: {isAutoLaunch}");

                // 获取实际的启动参数
                var activationArgs = AppInstance.GetActivatedEventArgs();
                if (activationArgs is ILaunchActivatedEventArgs launchArgs)
                {
                    Console.WriteLine($"  启动参数: '{launchArgs.Arguments}'");
                    Console.WriteLine($"  包含 '--autolaunch': {launchArgs.Arguments?.Contains("--autolaunch") ?? false}");
                }
                else
                {
                    Console.WriteLine("  启动参数: (无法获取)");
                }

                Console.WriteLine("\n测试说明:");
                Console.WriteLine("  - 如果应用是通过系统自启动启动的，IsAutoLaunch() 应返回 true");
                Console.WriteLine("  - 如果应用是用户手动启动的，IsAutoLaunch() 应返回 false");
                Console.WriteLine("  - 启动参数应包含 '--autolaunch' 标志（仅在自启动时）");

                Console.WriteLine("\n如何测试自启动:");
                Console.WriteLine("  1. 启用应用的开机自启动功能");
                Console.WriteLine("  2. 重启计算机");
                Console.WriteLine("  3. 系统启动后，应用应自动运行并检测到 '--autolaunch' 参数");
                Console.WriteLine("  4. 查看日志文件确认 IsAutoLaunch() 返回 true");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 错误: {ex.Message}");
                Console.WriteLine($"  堆栈跟踪: {ex.StackTrace}");
            }

            Console.WriteLine("\n=== 测试完成 ===");
        }

        /// <summary>
        /// 测试 HandleAsync 方法
        /// </summary>
        public static async Task RunHandleAsyncTest()
        {
            Console.WriteLine("=== AutoLaunchHandler HandleAsync 测试 ===\n");

            try
            {
                var app = Microsoft.UI.Xaml.Application.Current as App;
                if (app == null)
                {
                    Console.WriteLine("  ✗ 错误: 无法获取 App 实例");
                    return;
                }

                var handler = new AutoLaunchHandler(app);

                Console.WriteLine("测试 2: HandleAsync() 执行");
                Console.WriteLine("  正在执行自启动处理...");
                
                await handler.HandleAsync();
                
                Console.WriteLine("  ✓ HandleAsync() 执行完成");
                Console.WriteLine("  注意: 此方法应该:");
                Console.WriteLine("    - 初始化核心服务");
                Console.WriteLine("    - 不显示主窗口");
                Console.WriteLine("    - 执行后台任务");
                Console.WriteLine("    - 记录日志");
                Console.WriteLine("    - 不抛出异常（即使发生错误）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 错误: {ex.Message}");
                Console.WriteLine($"  注意: HandleAsync 不应抛出异常！");
            }

            Console.WriteLine("\n=== 测试完成 ===");
        }
    }
}
