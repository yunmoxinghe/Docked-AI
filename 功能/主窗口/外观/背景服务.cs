using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

namespace Docked_AI.Features.MainWindow.Appearance
{
    /// <summary>
    /// 背景服务 - 管理窗口背景效果（Mica/Acrylic）
    /// 
    /// 【文件职责】
    /// 1. 根据窗口状态切换背景效果（固定模式用 Mica，标准模式用 Acrylic）
    /// 2. 检测系统兼容性，提供降级方案
    /// 3. 确保背景透明度，避免遮挡背景效果
    /// 
    /// 【核心设计】
    /// 
    /// 为什么固定模式用 Mica，标准模式用 Acrylic？
    /// - Mica: 半透明效果，与桌面壁纸融合，适合固定在屏幕边缘的窗口
    /// - Acrylic: 毛玻璃效果，模糊背景内容，适合浮动窗口
    /// - 用户体验：固定模式更像系统组件，标准模式更像应用窗口
    /// 
    /// 为什么需要系统兼容性检查？
    /// - Mica 需要 Windows 11 (Build 22000+)
    /// - Acrylic 需要 Windows 10 1809 (Build 18362+)
    /// - 旧系统降级到渐变背景
    /// 
    /// 【核心逻辑流程】
    /// 
    /// 设置 Mica 背景流程：
    ///   1. 检查系统是否支持 Mica（Windows 11+）
    ///   2. 如果不支持，设置降级背景
    ///   3. 如果支持，创建 MicaBackdrop 并设置到 window.SystemBackdrop
    ///   4. 异步验证 Mica 效果是否生效
    ///   5. 确保根元素背景透明（让 Mica 透过）
    /// 
    /// 设置 Acrylic 背景流程：
    ///   1. 检查系统是否支持 Acrylic（Windows 10 1809+）
    ///   2. 如果不支持，设置降级背景
    ///   3. 如果支持，创建 DesktopAcrylicBackdrop 并设置到 window.SystemBackdrop
    ///   4. 异步验证 Acrylic 效果是否生效
    ///   5. 确保根元素背景透明
    /// 
    /// 降级背景流程：
    ///   1. 尝试使用 MicaBackdrop（即使系统不支持，也可能部分生效）
    ///   2. 如果失败，使用渐变背景（LinearGradientBrush）
    ///   3. 渐变背景：深灰色到浅灰色，半透明
    /// 
    /// 【关键依赖关系】
    /// - Window: WinUI 窗口对象，提供 SystemBackdrop 属性
    /// - MicaBackdrop: WinUI 3 Mica 背景效果
    /// - DesktopAcrylicBackdrop: WinUI 3 Acrylic 背景效果
    /// - Grid: 根元素，需要设置透明背景
    /// 
    /// 【潜在副作用】
    /// 1. 修改 window.SystemBackdrop 属性（全局副作用）
    /// 2. 修改根元素的 Background 属性（UI 更新）
    /// 3. 异步验证可能在后台线程执行（需要调度到 UI 线程）
    /// 
    /// 【重构风险点】
    /// 1. 系统版本检查：
    ///    - 如果 Windows 版本号变化，需要更新检查逻辑
    ///    - 如果 WinUI 3 API 变化，需要更新兼容性检查
    /// 2. 降级策略：
    ///    - 如果降级背景不美观，需要调整渐变颜色
    ///    - 如果降级失败，窗口可能完全透明或黑色
    /// 3. 透明背景的设置：
    ///    - StableBackdropHostBrush 使用 ARGB(1,0,0,0)，几乎透明的黑色
    ///    - 如果设置为完全透明 ARGB(0,0,0,0)，可能导致背景效果失效
    /// 4. 异步验证：
    ///    - 验证失败时设置降级背景，可能导致背景闪烁
    ///    - 如果验证逻辑错误，可能误判背景效果
    /// </summary>
    internal sealed class BackdropService
    {
        // 稳定的背景画刷：几乎透明的黑色 ARGB(1,0,0,0)
        // 为什么不用完全透明？完全透明可能导致背景效果失效
        private static readonly SolidColorBrush StableBackdropHostBrush = new(ColorHelper.FromArgb(1, 0, 0, 0));

        /// <summary>
        /// 确保 Mica 背景效果
        /// 
        /// 【调用时机】
        /// 窗口切换到固定模式时调用
        /// 
        /// 【副作用】
        /// - 修改 window.SystemBackdrop
        /// - 修改根元素的 Background
        /// - 异步验证背景效果
        /// </summary>
        public void EnsureMicaBackdrop(Window window)
        {
            try
            {
                if (!IsMicaSupported())
                {
                    SetFallbackBackground(window);
                    return;
                }

                if (window.SystemBackdrop == null || window.SystemBackdrop is not MicaBackdrop)
                {
                    window.SystemBackdrop = new MicaBackdrop();
                    window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                    {
                        ValidateMicaEffect(window);
                    });
                }

                EnsureTransparentBackground(window);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set mica backdrop: {ex.Message}");
                SetFallbackBackground(window);
            }
        }

        public void EnsureAcrylicBackdrop(Window window)
        {
            try
            {
                if (!IsAcrylicSupported())
                {
                    SetFallbackBackground(window);
                    return;
                }

                if (window.SystemBackdrop == null || window.SystemBackdrop is not DesktopAcrylicBackdrop)
                {
                    window.SystemBackdrop = new DesktopAcrylicBackdrop();
                    window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                    {
                        ValidateAcrylicEffect(window);
                    });
                }

                EnsureTransparentBackground(window);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set acrylic backdrop: {ex.Message}");
                SetFallbackBackground(window);
            }
        }

        private bool IsMicaSupported()
        {
            try
            {
                var version = Environment.OSVersion.Version;
                if (version.Major < 10 || (version.Major == 10 && version.Build < 22000))
                {
                    return false;
                }

                try
                {
                    _ = new MicaBackdrop();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to check mica support: {ex.Message}");
                return false;
            }
        }

        private bool IsAcrylicSupported()
        {
            try
            {
                var version = Environment.OSVersion.Version;
                if (version.Major < 10 || (version.Major == 10 && version.Build < 18362))
                {
                    return false;
                }

                try
                {
                    _ = new DesktopAcrylicBackdrop();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to check acrylic support: {ex.Message}");
                return false;
            }
        }

        private void ValidateMicaEffect(Window window)
        {
            try
            {
                if (window.SystemBackdrop is not MicaBackdrop)
                {
                    SetFallbackBackground(window);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to validate mica effect: {ex.Message}");
                SetFallbackBackground(window);
            }
        }

        private void ValidateAcrylicEffect(Window window)
        {
            try
            {
                if (window.SystemBackdrop is not DesktopAcrylicBackdrop)
                {
                    SetFallbackBackground(window);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to validate acrylic effect: {ex.Message}");
                SetFallbackBackground(window);
            }
        }

        private void EnsureTransparentBackground(Window window)
        {
            if (window.Content is Grid rootGrid)
            {
                rootGrid.Background = StableBackdropHostBrush;
            }
        }

        private void SetFallbackBackground(Window window)
        {
            try
            {
                try
                {
                    window.SystemBackdrop = new MicaBackdrop();
                    return;
                }
                catch (Exception micaEx)
                {
                    System.Diagnostics.Debug.WriteLine($"MicaBackdrop failed: {micaEx.Message}");
                }

                window.SystemBackdrop = null;
                if (window.Content is Grid rootGrid)
                {
                    var gradientBrush = new LinearGradientBrush
                    {
                        StartPoint = new Windows.Foundation.Point(0, 0),
                        EndPoint = new Windows.Foundation.Point(1, 1)
                    };
                    gradientBrush.GradientStops.Add(new GradientStop
                    {
                        Color = ColorHelper.FromArgb(180, 40, 40, 40),
                        Offset = 0
                    });
                    gradientBrush.GradientStops.Add(new GradientStop
                    {
                        Color = ColorHelper.FromArgb(160, 60, 60, 60),
                        Offset = 1
                    });

                    rootGrid.Background = gradientBrush;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fallback background failed: {ex.Message}");
            }
        }
    }
}
