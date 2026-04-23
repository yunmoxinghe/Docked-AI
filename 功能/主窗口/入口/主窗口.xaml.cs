using Docked_AI.Features.MainWindow.State;
using Docked_AI.Features.MainWindow.Visibility;
using Docked_AI.Features.MainWindowContent.Linker;
using Docked_AI.Features.Tray;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Docked_AI
{
    /// <summary>
    /// 主窗口类 - 应用程序的核心 UI 容器
    /// 
    /// 【文件职责】
    /// 1. 作为应用的主窗口入口，协调 ViewModel、Controller 和 UI 组件
    /// 2. 管理窗口生命周期事件（激活、关闭、尺寸变化）
    /// 3. 响应用户交互（固定/取消固定、最大化/还原）
    /// 4. 同步 ViewModel 状态到 UI 表现（图标、圆角、边距）
    /// 
    /// 【核心逻辑流程】
    /// 初始化阶段：
    ///   1. 构造函数中通过 DWM API 设置纯色背景，避免亚克力在启动屏幕前显示
    ///   2. 创建 MainWindowViewModel（状态容器）和 WindowHostController（状态转换执行器）
    ///   3. 订阅 Linker 事件（用户交互）、ViewModel 属性变化（状态同步）、AppWindow 事件（OS 窗口状态）
    ///   4. 初始化 UI 状态（图标、圆角、边距）
    /// 
    /// 启动屏幕流程：
    ///   1. 窗口激活时显示纯色背景（黑/白）
    ///   2. ShowSplash() 被调用，立即播放淡入动画（纯色 → 启动屏幕图片）
    ///   3. 启动屏幕淡出完成后，设置亚克力背景
    /// 
    /// 运行时状态同步：
    ///   - ViewModel.CurrentState 变化 → 触发 OnViewModelPropertyChanged → 刷新 UI（图标、圆角、边距）
    ///   - AppWindow.Changed 事件 → 检测 OS 窗口状态变化 → 同步到 Controller
    ///   - Linker 事件（用户点击按钮）→ 调用 Controller 方法 → 触发状态转换
    /// 
    /// 【关键依赖关系】
    /// - MainWindowViewModel: 状态容器，持有 CurrentState（Windowed/Pinned/Maximized/Hidden）
    /// - WindowHostController: 状态转换执行器，负责动画、样式、布局的实际操作
    /// - Linker: UI 组件桥接器，提供 NavBar 和内容区的访问接口
    /// - AppWindow: WinUI 窗口对象，提供 OS 级别的窗口状态（最大化/还原）
    /// 
    /// 【潜在副作用】
    /// 1. DwmSetWindowAttribute 在构造函数和 ShowSplash 中调用，修改窗口 DWM 属性（不可逆）
    /// 2. ViewModel.PropertyChanged 事件触发 UI 更新（可能导致布局重排）
    /// 3. AppWindow.Changed 事件可能在动画执行期间触发，需要防重入
    /// 4. Linker 事件订阅/取消订阅必须成对，否则导致内存泄漏
    /// 
    /// 【重构风险点】
    /// 1. 事件订阅顺序：必须在 InitializeComponent() 之后订阅，否则 RootGrid 为 null
    /// 2. DWM API 调用时机：
    ///    - 构造函数中设置纯色背景（避免亚克力在启动屏幕前显示）
    ///    - ShowSplash 淡出后设置亚克力背景（确保平滑过渡）
    /// 3. RefreshViewModelDrivenState 和 RefreshWindowChromeState 的调用时机：
    ///    - 前者依赖 ViewModel.CurrentState，后者依赖 AppWindow.Presenter.State
    ///    - 两者必须分开调用，避免状态不一致
    /// 4. OnAppWindowChanged 中的 SyncFromOSWindowState：
    ///    - 仅在 DidPresenterChange 时调用，避免尺寸变化时误触发状态同步
    /// 5. 窗口关闭时必须取消所有事件订阅，否则导致内存泄漏
    /// </summary>
    public sealed partial class MainWindow : Window, IWindowToggle
    {
        // 核心依赖：状态容器、控制器、UI 桥接器
        private readonly MainWindowViewModel _viewModel;
        private readonly WindowHostController _windowController;
        private readonly Linker? _linker;

        /// <summary>
        /// 公开当前窗口状态，供外部组件（如托盘管理器）查询
        /// </summary>
        public WindowState CurrentWindowState => _viewModel.CurrentState;

        /// <summary>
        /// 构造函数 - 初始化窗口、ViewModel、Controller 和事件订阅
        /// 
        /// 【关键设计决策】
        /// 1. 为什么构造函数中不设置任何 DWM 背景效果？
        ///    - 启动时使用默认的纯色背景（由 XAML 的 Background 属性控制）
        ///    - 亚克力效果在启动屏幕淡出后由 ShowSplash() 设置
        ///    - 避免在启动屏幕显示前出现任何透明或特殊效果
        /// 
        /// 2. 为什么不在构造函数中调用 Activate()？
        ///    - Activate() 会触发窗口显示动画，应由 WindowHostController.RequestSlideIn() 控制
        ///    - 过早调用会导致窗口在未完成布局配置时显示，产生闪烁
        /// 
        /// 3. 为什么先创建 ViewModel 再创建 Controller？
        ///    - Controller 需要 ViewModel 引用来订阅状态变化
        ///    - ViewModel 是纯数据容器，不依赖 Controller
        /// 
        /// 【副作用】
        /// - 订阅多个事件（必须在 OnWindowClosed 中取消订阅）
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // 不设置任何 DWM 背景效果，使用默认的纯色背景
            // 亚克力效果将在启动屏幕淡出后由 ShowSplash() 设置

            // 创建 ViewModel（状态容器）和 Controller（状态转换执行器）
            _viewModel = new MainWindowViewModel();
            if (Content is FrameworkElement rootElement)
            {
                rootElement.DataContext = _viewModel;
            }

            // 获取 Linker（UI 桥接器），用于访问 NavBar 和内容区
            _linker = RootGrid.Children.OfType<Linker>().FirstOrDefault();
            _windowController = new WindowHostController(this, _viewModel);

            // 订阅事件：用户交互、状态变化、窗口事件
            SubscribeToLinkerEvents();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            AppWindow.Changed += OnAppWindowChanged;
            Closed += OnWindowClosed;

            // 初始化 UI 状态（图标、圆角、边距）
            RefreshViewModelDrivenState();
            RefreshWindowChromeState();
        }

        /// <summary>
        /// 订阅 Linker 事件 - 响应用户交互（固定/取消固定、最大化/还原）
        /// 
        /// 【设计原因】
        /// Linker 是 UI 组件桥接器，封装了 NavBar 和内容区的访问接口
        /// 通过事件机制解耦 UI 交互和业务逻辑，避免直接依赖 UI 控件
        /// 
        /// 【重构风险】
        /// 如果 Linker 未在 XAML 中定义，_linker 为 null，事件订阅失败
        /// 必须在 XAML 中确保 Linker 存在于 RootGrid.Children 中
        /// </summary>
        private void SubscribeToLinkerEvents()
        {
            if (_linker is null)
            {
                Debug.WriteLine("MainWindow: Linker not found in RootGrid.");
                return;
            }

            _linker.DockToggleRequested += OnDockToggleRequested;
            _linker.WindowStateToggleRequested += OnWindowStateToggleRequested;
        }

        /// <summary>
        /// 取消订阅 Linker 事件 - 防止内存泄漏
        /// 
        /// 【重要性】
        /// 必须在窗口关闭时调用，否则 Linker 持有 MainWindow 引用，导致内存泄漏
        /// </summary>
        private void UnsubscribeFromLinkerEvents()
        {
            if (_linker is null)
            {
                return;
            }

            _linker.DockToggleRequested -= OnDockToggleRequested;
            _linker.WindowStateToggleRequested -= OnWindowStateToggleRequested;
        }

        /// <summary>
        /// AppWindow.Changed 事件处理器 - 同步 OS 窗口状态到内部状态
        /// 
        /// 【触发时机】
        /// - 用户通过 Win+↑/↓ 快捷键最大化/还原窗口
        /// - 用户拖动窗口到屏幕顶部触发最大化
        /// - 窗口尺寸变化（需要过滤，避免误触发状态同步）
        /// 
        /// 【核心逻辑】
        /// 1. 仅在 DidPresenterChange 时同步状态（避免尺寸变化时误触发）
        /// 2. 调用 DetermineOSWindowState() 获取 OS 窗口状态
        /// 3. 调用 Controller.SyncFromOSWindowState() 同步到内部状态
        /// 4. 刷新窗口 Chrome 状态（图标、边距）
        /// 
        /// 【重构风险】
        /// 如果移除 DidPresenterChange 检查，尺寸变化会触发状态同步，导致状态抖动
        /// </summary>
        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (!args.DidPresenterChange && !args.DidSizeChange)
            {
                return;
            }

            // 刷新窗口 Chrome 状态（图标、边距）
            RefreshWindowChromeState();

            // 仅在 Presenter 状态变化时同步（避免尺寸变化时误触发）
            if (args.DidPresenterChange)
            {
                _windowController.SyncFromOSWindowState(DetermineOSWindowState());
            }
        }

        /// <summary>
        /// 从 OS 窗口状态映射到内部状态
        /// 
        /// 【映射规则】
        /// - OverlappedPresenterState.Maximized → WindowState.Maximized
        /// - OverlappedPresenterState.Restored → WindowState.Windowed
        /// - OverlappedPresenterState.Minimized → WindowState.Hidden
        /// - 其他状态 → 保持当前状态（避免状态丢失）
        /// 
        /// 【设计原因】
        /// OS 窗口状态和内部状态不完全一致，需要映射：
        /// - OS 没有 Pinned 状态，Pinned 是应用自定义状态
        /// - OS 的 Minimized 映射到 Hidden（应用不使用最小化）
        /// </summary>
        private WindowState DetermineOSWindowState()
        {
            return AppWindow.Presenter is OverlappedPresenter presenter
                ? presenter.State switch
                {
                    OverlappedPresenterState.Maximized => WindowState.Maximized,
                    OverlappedPresenterState.Restored => WindowState.Windowed,
                    OverlappedPresenterState.Minimized => WindowState.Hidden,
                    _ => _viewModel.CurrentState
                }
                : _viewModel.CurrentState;
        }

        /// <summary>
        /// 检查窗口是否处于最大化状态
        /// 用于 UI 更新（图标、边距）
        /// </summary>
        private bool IsWindowMaximized()
        {
            return AppWindow.Presenter is OverlappedPresenter
            {
                State: OverlappedPresenterState.Maximized
            };
        }

        /// <summary>
        /// 刷新窗口 Chrome 状态 - 更新图标和边距
        /// 
        /// 【调用时机】
        /// - AppWindow.Changed 事件触发时（OS 窗口状态变化）
        /// - 构造函数初始化时
        /// 
        /// 【副作用】
        /// - 调用 Linker 方法更新 NavBar 图标
        /// - 调用 Linker 方法更新内容区边距
        /// </summary>
        private void RefreshWindowChromeState()
        {
            UpdateWindowStateIcon();
            UpdateContentTopMargin();
        }

        /// <summary>
        /// 刷新 ViewModel 驱动的状态 - 更新图标、圆角、边距
        /// 
        /// 【调用时机】
        /// - ViewModel.CurrentState 变化时（通过 PropertyChanged 事件）
        /// - 构造函数初始化时
        /// 
        /// 【副作用】
        /// - 调用 Linker 方法更新 NavBar 图标（固定/取消固定）
        /// - 调用 Linker 方法更新内容区圆角（固定模式下无圆角）
        /// - 调用 Linker 方法更新内容区边距（固定/最大化模式下无边距）
        /// </summary>
        private void RefreshViewModelDrivenState()
        {
            bool isPinned = _viewModel.CurrentState == WindowState.Pinned;
            UpdateDockToggleIcon(isPinned);
            UpdateContentCornerRadius(isPinned);
            UpdateContentTopMargin();
        }

        /// <summary>
        /// 更新窗口状态图标（最大化/还原）
        /// 委托给 Linker.NavBarInstance 处理
        /// </summary>
        private void UpdateWindowStateIcon()
        {
            _linker?.NavBarInstance.UpdateWindowStateIcon(IsWindowMaximized());
        }

        /// <summary>
        /// 切换窗口状态（最大化/还原）
        /// 公开方法，供外部组件（如网页浏览页面）调用
        /// </summary>
        public void ToggleWindowState()
        {
            _windowController.ToggleMaximize();
        }

        /// <summary>
        /// Linker 事件处理器 - 用户点击最大化/还原按钮
        /// </summary>
        private void OnWindowStateToggleRequested(object? sender, System.EventArgs e)
        {
            _windowController.ToggleMaximize();
        }

        /// <summary>
        /// ViewModel.PropertyChanged 事件处理器 - 同步状态到 UI
        /// 
        /// 【触发时机】
        /// - WindowStateManager 提交状态转换后，ViewModel.CurrentState 变化
        /// 
        /// 【核心逻辑】
        /// 仅响应 CurrentState 属性变化，刷新 UI 状态（图标、圆角、边距）
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.CurrentState))
            {
                RefreshViewModelDrivenState();
            }
        }

        /// <summary>
        /// 更新固定/取消固定图标
        /// 委托给 Linker.NavBarInstance 处理
        /// </summary>
        private void UpdateDockToggleIcon(bool isPinned)
        {
            _linker?.NavBarInstance.UpdateDockToggleIcon(isPinned);
        }

        /// <summary>
        /// 更新内容区圆角
        /// 固定模式下无圆角（与屏幕边缘对齐），其他模式有圆角
        /// </summary>
        private void UpdateContentCornerRadius(bool isPinned)
        {
            _linker?.UpdateContentCornerRadius(isPinned);
        }

        /// <summary>
        /// 更新内容区顶部边距
        /// 固定模式或最大化模式下无边距（充满整个窗口），其他模式有边距
        /// </summary>
        private void UpdateContentTopMargin()
        {
            bool isPinnedOrMaximized = _viewModel.CurrentState == WindowState.Pinned || IsWindowMaximized();
            _linker?.UpdateContentTopMargin(isPinnedOrMaximized);
        }

        // ==================== IWindowToggle 接口实现 ====================
        // 这些方法由托盘管理器调用，用于控制窗口显示/隐藏和固定状态

        /// <summary>
        /// 切换窗口显示/隐藏状态
        /// 由托盘图标点击触发
        /// </summary>
        public void ToggleWindow()
        {
            _windowController.ToggleWindow();
        }

        /// <summary>
        /// 切换固定/取消固定状态
        /// 由 Linker 事件触发（用户点击固定按钮）
        /// </summary>
        public void TogglePinnedDock()
        {
            _windowController.TogglePinnedDock();
        }

        /// <summary>
        /// 标记初始化完成
        /// 由托盘管理器在窗口创建完成后调用，解除事件屏蔽
        /// </summary>
        public void SetInitializingComplete()
        {
            _windowController.SetInitializingComplete();
        }

        /// <summary>
        /// 请求执行首次显示动画
        /// 由托盘管理器在窗口创建完成后调用
        /// 利用 Activate() 的内置动画，这是首次创建窗口时唯一不会闪现的方案
        /// </summary>
        public void RequestSlideIn()
        {
            _windowController.RequestSlideIn();
        }

        /// <summary>
        /// Linker 事件处理器 - 用户点击固定/取消固定按钮
        /// </summary>
        private void OnDockToggleRequested(object? sender, System.EventArgs e)
        {
            TogglePinnedDock();
        }

        /// <summary>
        /// 导航到新页面
        /// 由外部组件（如网页浏览页面）调用，用于打开新标签页
        /// </summary>
        /// <param name="url">要导航的 URL</param>
        public void NavigateToNewPage(string url)
        {
            Debug.WriteLine($"MainWindow.NavigateToNewPage called with URL: {url}");

            if (_linker is null)
            {
                Debug.WriteLine("MainWindow.NavigateToNewPage aborted: Linker not found.");
                return;
            }

            _linker.NavigateToNewPage(url);
        }

        /// <summary>
        /// 显示启动屏幕动画
        /// 由应用入口在窗口激活后调用
        /// 
        /// 【动画流程】
        /// 1. 立即播放淡入动画：纯色 → 启动屏幕图片（400ms）
        /// 2. 等待显示时间（1500ms）
        /// 3. 淡出动画：启动屏幕 → 主界面（300ms）
        /// 4. 淡出完成后设置亚克力背景并将 RootGrid 改为透明
        /// 5. 隐藏启动屏幕遮罩
        /// </summary>
        public async void ShowSplash()
        {
            // 启动屏幕已经在 XAML 中默认可见（Visibility="Visible", Opacity="1"）
            // 立即播放淡入动画（纯色 -> 启动屏幕）
            var fadeInStoryboard = (Storyboard)SplashOverlay.Resources["SplashFadeIn"];
            fadeInStoryboard.Begin();

            // 等待淡入完成 + 显示时间
            await Task.Delay(1900); // 400ms 淡入 + 1500ms 显示

            // 使用 TaskCompletionSource 等待淡出动画完全完成
            var tcs = new TaskCompletionSource<bool>();
            
            var fadeOutStoryboard = (Storyboard)SplashOverlay.Resources["SplashFadeOut"];
            fadeOutStoryboard.Completed += (s, e) =>
            {
                tcs.SetResult(true);
            };

            fadeOutStoryboard.Begin();
            
            // 等待淡出动画完成
            await tcs.Task;
            
            // 淡出完成后设置亚克力背景（使用 WinUI SystemBackdrop API）
            try
            {
                this.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
                System.Diagnostics.Debug.WriteLine("[MainWindow] Acrylic backdrop set after splash screen");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to set acrylic backdrop: {ex.Message}");
            }
            
            // 将 RootGrid 背景改为透明，让亚克力效果透过来
            RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(0, 0, 0, 0));
            
            // 确保启动屏幕完全隐藏
            SplashOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 窗口关闭事件处理器 - 清理资源和取消事件订阅
        /// 
        /// 【重要性】
        /// 必须取消所有事件订阅，否则导致内存泄漏：
        /// - ViewModel.PropertyChanged
        /// - AppWindow.Changed
        /// - Closed
        /// - Linker 事件
        /// 
        /// 【重构风险】
        /// 如果添加新的事件订阅，必须在此处取消订阅
        /// </summary>
        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            AppWindow.Changed -= OnAppWindowChanged;
            Closed -= OnWindowClosed;
            UnsubscribeFromLinkerEvents();
        }
    }
}
