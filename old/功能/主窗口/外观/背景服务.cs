using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.System.Power;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Windows.UI;

namespace Docked_AI.Features.MainWindow.Appearance
{
    /// <summary>
    /// 背景服务 - 管理窗口背景效果（Mica/Acrylic）
    /// 
    /// 【文件职责】
    /// 1. 根据窗口状态切换背景效果（固定模式用 Mica，标准模式用 Acrylic）
    /// 2. 检测系统兼容性，提供降级方案
    /// 3. 监听省电模式，自动调整背景效果以节省电量
    /// 4. 确保背景透明度，避免遮挡背景效果
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
    /// 为什么需要监听省电模式？
    /// - 亚克力效果使用 GPU 渲染，在省电模式下会自动禁用
    /// - 主动监听省电状态，在省电模式下切换到 Mica（更节能）
    /// - 退出省电模式后恢复原有背景效果
    /// - 参考：https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-power
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

        private Window? _currentWindow;
        private bool _isPinnedMode;
        private bool _isEnergySaverListenerRegistered;
        private bool _isEnergySaverActive;
        
        // 渐变亚克力层（固定模式专用）
        private Grid? _gradientAcrylicLayer;
        
        // 存储所有亚克力条带，用于动态调整透明度
        private List<SystemBackdropElement>? _acrylicSegments;

        /// <summary>
        /// 确保透明背景效果（固定模式专用）
        /// </summary>
        /// <param name="window">窗口对象</param>
        /// <param name="isNavigationBarOnLeft">导航栏是否在左侧（可选，默认 false 表示在右侧）</param>
        public void EnsureTransparentBackdrop(Window window, bool isNavigationBarOnLeft = false)
        {
            try
            {
                _currentWindow = window;
                _isPinnedMode = true;
                RegisterEnergySaverListener();

                // 使用 WinUIEx 的 TransparentTintBackdrop 实现完全透明
                var transparentBackdrop = new WinUIEx.TransparentTintBackdrop(
                    Windows.UI.Color.FromArgb(0, 0, 0, 0));
                window.SystemBackdrop = transparentBackdrop;

                EnsureTransparentBackground(window);
                
                // 显示渐变亚克力层，根据导航栏位置自动调整方向
                ShowGradientAcrylicLayer(window, isNavigationBarOnLeft);
                
                System.Diagnostics.Debug.WriteLine($"[BackdropService] Transparent backdrop applied for pinned mode, nav on left: {isNavigationBarOnLeft}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set transparent backdrop: {ex.Message}");
                SetFallbackBackground(window);
            }
        }

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
        /// - 注册省电模式监听
        /// </summary>
        public void EnsureMicaBackdrop(Window window)
        {
            try
            {
                _currentWindow = window;
                _isPinnedMode = true;
                RegisterEnergySaverListener();

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
                _currentWindow = window;
                _isPinnedMode = false;
                RegisterEnergySaverListener();

                // 隐藏渐变亚克力层（标准模式不需要）
                HideGradientAcrylicLayer();

                // 省电模式下使用 Mica 代替 Acrylic（更节能）
                if (_isEnergySaverActive)
                {
                    System.Diagnostics.Debug.WriteLine("[BackdropService] Energy saver active, using Mica instead of Acrylic");
                    if (IsMicaSupported())
                    {
                        window.SystemBackdrop = new MicaBackdrop();
                    }
                    else
                    {
                        SetFallbackBackground(window);
                    }
                    EnsureTransparentBackground(window);
                    return;
                }

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

        /// <summary>
        /// 注册省电模式监听器
        /// 当省电模式状态改变时，自动调整背景效果
        /// </summary>
        private void RegisterEnergySaverListener()
        {
            // 避免重复注册
            if (_isEnergySaverListenerRegistered)
            {
                return;
            }

            try
            {
                // 检查当前省电模式状态
                _isEnergySaverActive = PowerManager.EnergySaverStatus == EnergySaverStatus.On;
                System.Diagnostics.Debug.WriteLine($"[BackdropService] Initial energy saver status: {PowerManager.EnergySaverStatus}");

                // 注册省电模式变化事件
                PowerManager.EnergySaverStatusChanged += OnEnergySaverStatusChanged;
                _isEnergySaverListenerRegistered = true;
                System.Diagnostics.Debug.WriteLine("[BackdropService] Energy saver listener registered");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackdropService] Failed to register energy saver listener: {ex.Message}");
            }
        }

        /// <summary>
        /// 省电模式状态变化处理
        /// </summary>
        private void OnEnergySaverStatusChanged(object? sender, object e)
        {
            try
            {
                var newStatus = PowerManager.EnergySaverStatus;
                var wasActive = _isEnergySaverActive;
                _isEnergySaverActive = newStatus == EnergySaverStatus.On;

                System.Diagnostics.Debug.WriteLine($"[BackdropService] Energy saver status changed: {newStatus} (was active: {wasActive}, now active: {_isEnergySaverActive})");

                // 状态没有实际变化，不需要更新
                if (wasActive == _isEnergySaverActive || _currentWindow == null)
                {
                    return;
                }

                // 在 UI 线程上更新背景
                _currentWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    if (_currentWindow == null) return;

                    if (_isEnergySaverActive)
                    {
                        // 进入省电模式：切换到 Mica（更节能）
                        System.Diagnostics.Debug.WriteLine("[BackdropService] Switching to Mica for energy saving");
                        if (IsMicaSupported())
                        {
                            _currentWindow.SystemBackdrop = new MicaBackdrop();
                        }
                        else
                        {
                            SetFallbackBackground(_currentWindow);
                        }
                    }
                    else
                    {
                        // 退出省电模式：恢复原有背景
                        if (_isPinnedMode)
                        {
                            System.Diagnostics.Debug.WriteLine("[BackdropService] Restoring Mica backdrop");
                            EnsureMicaBackdrop(_currentWindow);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[BackdropService] Restoring Acrylic backdrop");
                            if (IsAcrylicSupported())
                            {
                                _currentWindow.SystemBackdrop = new DesktopAcrylicBackdrop();
                            }
                            else if (IsMicaSupported())
                            {
                                _currentWindow.SystemBackdrop = new MicaBackdrop();
                            }
                            else
                            {
                                SetFallbackBackground(_currentWindow);
                            }
                        }
                    }

                    EnsureTransparentBackground(_currentWindow);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackdropService] Error handling energy saver status change: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理资源，取消注册事件监听
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_isEnergySaverListenerRegistered)
                {
                    PowerManager.EnergySaverStatusChanged -= OnEnergySaverStatusChanged;
                    _isEnergySaverListenerRegistered = false;
                    System.Diagnostics.Debug.WriteLine("[BackdropService] Energy saver listener unregistered");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackdropService] Error disposing: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示渐变亚克力层（固定模式专用）
        /// 根据导航栏位置自动调整渐变方向
        /// - 导航栏在右侧：左边透明，右边亚克力
        /// - 导航栏在左侧：右边透明，左边亚克力
        /// 方案：使用大量 SystemBackdropElement 分段（100+）实现平滑横向渐变
        /// </summary>
        /// <param name="window">窗口对象</param>
        /// <param name="isNavigationBarOnLeft">导航栏是否在左侧</param>
        private void ShowGradientAcrylicLayer(Window window, bool isNavigationBarOnLeft)
        {
            try
            {
                // 如果已经存在，先移除
                HideGradientAcrylicLayer();

                // 获取 RootGrid（主窗口的根元素）
                if (window.Content is not Grid rootGrid)
                {
                    System.Diagnostics.Debug.WriteLine("[BackdropService] Failed to get RootGrid");
                    return;
                }

                // 创建主容器
                _gradientAcrylicLayer = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    IsHitTestVisible = false // 不阻挡鼠标事件
                };

                // 初始化亚克力条带列表
                _acrylicSegments = new List<SystemBackdropElement>();

                // 使用大量分段（100个）实现平滑横向渐变
                int segmentCount = 100;
                
                // 定义列（横向分段）
                for (int i = 0; i < segmentCount; i++)
                {
                    _gradientAcrylicLayer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                // 创建多个亚克力条带，每个不同 Opacity
                for (int i = 0; i < segmentCount; i++)
                {
                    double t = (double)i / (segmentCount - 1);
                    double opacity;
                    
                    if (isNavigationBarOnLeft)
                    {
                        // 导航栏在左侧：从左到右，不透明到透明
                        opacity = 1.0 - EaseInOutQuad(t); // 左侧不透明，右侧透明
                    }
                    else
                    {
                        // 导航栏在右侧：从左到右，透明到不透明
                        opacity = EaseInOutQuad(t); // 左侧透明，右侧不透明
                    }

                    // 创建一个亚克力元素
                    var acrylicSegment = new SystemBackdropElement
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Opacity = opacity
                    };
                    acrylicSegment.SystemBackdrop = new DesktopAcrylicBackdrop();

                    // 放在对应的列
                    Grid.SetColumn(acrylicSegment, i);
                    _gradientAcrylicLayer.Children.Add(acrylicSegment);
                    
                    // ⭐ 保存到列表中，用于后续调整透明度
                    _acrylicSegments.Add(acrylicSegment);
                }

                // 添加到根元素
                rootGrid.Children.Insert(0, _gradientAcrylicLayer);

                string direction = isNavigationBarOnLeft ? "left-to-right (nav on left)" : "right-to-left (nav on right)";
                System.Diagnostics.Debug.WriteLine($"[BackdropService] Horizontal gradient acrylic layer shown with {segmentCount} segments, direction: {direction}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackdropService] Failed to show gradient acrylic layer: {ex.Message}");
            }
        }

        /// <summary>
        /// 当其他应用最大化时，将所有亚克力条带设置为完全不透明
        /// </summary>
        public void SetGradientFullyOpaque()
        {
            try
            {
                if (_acrylicSegments == null) return;

                foreach (var segment in _acrylicSegments)
                {
                    segment.Opacity = 1.0; // 全部设为不透明
                }

                System.Diagnostics.Debug.WriteLine("[BackdropService] Gradient set to fully opaque (other app maximized)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackdropService] Failed to set gradient fully opaque: {ex.Message}");
            }
        }

        /// <summary>
        /// 当其他应用取消最大化时，恢复渐变效果
        /// </summary>
        public void RestoreGradientOpacity(bool isNavigationBarOnLeft)
        {
            try
            {
                if (_acrylicSegments == null) return;

                int segmentCount = _acrylicSegments.Count;
                
                for (int i = 0; i < segmentCount; i++)
                {
                    double t = (double)i / (segmentCount - 1);
                    double opacity;
                    
                    if (isNavigationBarOnLeft)
                    {
                        opacity = 1.0 - EaseInOutQuad(t);
                    }
                    else
                    {
                        opacity = EaseInOutQuad(t);
                    }

                    _acrylicSegments[i].Opacity = opacity;
                }

                System.Diagnostics.Debug.WriteLine("[BackdropService] Gradient opacity restored");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackdropService] Failed to restore gradient opacity: {ex.Message}");
            }
        }

        /// <summary>
        /// 计算平滑的透明度值
        /// 支持多种缓动曲线
        /// </summary>
        private double CalculateSmoothOpacity(double t)
        {
            // 可选方案 1: EaseInOutQuad（默认使用）
            return EaseInOutQuad(t);

            // 可选方案 2: Smoothstep（更平滑）
            // return t * t * (3.0 - 2.0 * t);

            // 可选方案 3: Smootherstep（最平滑）
            // return t * t * t * (t * (t * 6 - 15) + 10);

            // 可选方案 4: 三次贝塞尔
            // return CubicBezier(t, 0.0, 0.42, 0.58, 1.0);

            // 可选方案 5: 指数缓动（慢开始，快结束）
            // return t == 0.0 ? 0.0 : Math.Pow(2, 10 * (t - 1));
        }

        /// <summary>
        /// 缓动函数：EaseInOutQuad，让渐变更平滑自然
        /// </summary>
        private double EaseInOutQuad(double t)
        {
            return t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
        }

        /// <summary>
        /// 旧的渐变遮罩方法（已弃用）
        /// </summary>
        private void ApplySmoothGradientMask(Grid targetGrid)
        {
            // 已弃用
        }

        /// <summary>
        /// 使用 Composition API 应用真正的透明度渐变
        /// 使用多个 SpriteVisual 分段，每个设置不同的 Opacity
        /// </summary>
        private void ApplyCompositionOpacityGradient(UIElement element)
        {
            try
            {
                // 获取元素的 Visual
                var elementVisual = ElementCompositionPreview.GetElementVisual(element);
                var compositor = elementVisual.Compositor;

                // 创建容器 Visual
                var containerVisual = compositor.CreateContainerVisual();

                // 分段数量（越多越平滑）
                int segmentCount = 30;

                // 创建多个分段，每个分段不同 Opacity
                for (int i = 0; i < segmentCount; i++)
                {
                    // 计算 Opacity（从 0.0 到 1.0）
                    float opacity = (float)i / (segmentCount - 1);

                    // 创建一个 SpriteVisual 代表一个分段
                    var segmentVisual = compositor.CreateSpriteVisual();
                    
                    // 使用透明色画刷（亚克力效果从 SystemBackdrop 显示）
                    segmentVisual.Brush = compositor.CreateColorBrush(Microsoft.UI.Colors.Transparent);
                    
                    // 设置位置和大小（垂直分段）
                    segmentVisual.Offset = new Vector3(0, i * (1.0f / segmentCount), 0);
                    segmentVisual.RelativeSizeAdjustment = new Vector2(1.0f, 1.0f / segmentCount); // 宽度100%，高度为总高度的1/N
                    segmentVisual.Opacity = opacity;

                    containerVisual.Children.InsertAtTop(segmentVisual);
                }

                // 容器填充整个元素
                containerVisual.RelativeSizeAdjustment = Vector2.One;

                // 将容器 Visual 设置为元素的子 Visual
                ElementCompositionPreview.SetElementChildVisual(element, containerVisual);

                System.Diagnostics.Debug.WriteLine($"[BackdropService] Composition opacity gradient applied using {segmentCount} segments");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackdropService] Failed to apply composition opacity gradient: {ex.Message}");
            }
        }

        /// <summary>
        /// 旧的透明度渐变方法（已弃用）
        /// </summary>
        private void ApplyOpacityGradient(Grid targetGrid)
        {
            // 已弃用
        }

        /// <summary>
        /// 使用 Composition API 应用渐变遮罩（已弃用）
        /// </summary>
        private void ApplyCompositionGradientMask(Grid targetGrid)
        {
            // 已弃用：改用 ApplyOpacityGradient
        }

        /// <summary>
        /// 隐藏渐变亚克力层（恢复标准模式时调用）
        /// </summary>
        private void HideGradientAcrylicLayer()
        {
            try
            {
                if (_gradientAcrylicLayer != null && _currentWindow?.Content is Grid rootGrid)
                {
                    rootGrid.Children.Remove(_gradientAcrylicLayer);
                    _gradientAcrylicLayer = null;
                    System.Diagnostics.Debug.WriteLine("[BackdropService] Gradient acrylic layer hidden");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackdropService] Failed to hide gradient acrylic layer: {ex.Message}");
            }
        }
    }
}
