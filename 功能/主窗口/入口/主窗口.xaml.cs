// 引入主窗口状态管理相关类（WindowState、MainWindowViewModel）
using Docked_AI.Features.MainWindow.State;
// 引入窗口可见性控制相关类（WindowHostController）
using Docked_AI.Features.MainWindow.Visibility;
// 引入主窗口内容连接器（Linker，负责连接导航栏和内容区域）
using Docked_AI.Features.MainWindowContent.Linker;
// 引入导航栏相关类
using Docked_AI.Features.MainWindowContent.NavigationBar;
// 引入新建页面相关类
using Docked_AI.Features.Pages.New;
// 引入托盘图标管理相关类（IWindowToggle 接口）
using Docked_AI.Features.Tray;
// 引入 WinUI 核心类型（Window、FrameworkElement 等）
using Microsoft.UI.Xaml;
// 引入属性变化通知接口（INotifyPropertyChanged）
using System.ComponentModel;

namespace Docked_AI
{
    /// <summary>
    /// 主窗口类
    /// 继承自 Window（WinUI 窗口基类）
    /// 实现 IWindowToggle 接口（支持窗口显示/隐藏切换）
    /// sealed 关键字表示该类不能被继承
    /// partial 关键字表示该类的定义分散在多个文件中（与 .xaml 文件配合）
    /// </summary>
    public sealed partial class MainWindow : Window, IWindowToggle
    {
        // 视图模型，负责管理窗口状态数据（Docked、Pinned、Windowed、Maximized 等）
        private readonly MainWindowViewModel _viewModel;
        // 窗口主机控制器，负责处理窗口状态转换逻辑（显示/隐藏、停靠/取消停靠、最大化/还原等）
        private readonly WindowHostController _windowController;

        /// <summary>
        /// 公开当前窗口状态属性
        /// 供外部（如托盘图标管理器）查询窗口当前状态
        /// </summary>
        public WindowState CurrentWindowState => _viewModel.CurrentState;

        /// <summary>
        /// 主窗口构造函数
        /// 在窗口实例化时执行，负责初始化所有组件和事件订阅
        /// </summary>
        public MainWindow()
        {
            // 初始化 XAML 定义的 UI 组件（由编译器自动生成的方法）
            // 该方法会加载 主窗口.xaml 文件中定义的 UI 结构
            InitializeComponent();

            // 创建视图模型实例，初始状态为 NotCreated（未创建）
            _viewModel = new MainWindowViewModel();
            // 检查窗口内容是否为 FrameworkElement（WinUI 的基础 UI 元素类型）
            if (Content is FrameworkElement rootElement)
            {
                // 将视图模型设置为根元素的数据上下文
                // 这样 XAML 中的数据绑定（{Binding}）就能访问视图模型的属性
                rootElement.DataContext = _viewModel;
            }

            // 创建窗口主机控制器，传入窗口实例和视图模型
            // 控制器负责协调窗口的物理状态（OS 层面）和逻辑状态（应用层面）
            _windowController = new WindowHostController(this, _viewModel);

            // 检查根网格（RootGrid）是否包含子元素，且第一个子元素是否为 Linker
            // Linker 是连接导航栏和内容区域的中间层组件
            if (RootGrid.Children.Count > 0 && RootGrid.Children[0] is Linker linker)
            {
                // 订阅停靠切换请求事件（用户点击导航栏的停靠按钮时触发）
                linker.DockToggleRequested += OnDockToggleRequested;
                // 订阅窗口状态切换请求事件（用户点击导航栏的最大化/还原按钮时触发）
                linker.WindowStateToggleRequested += OnWindowStateToggleRequested;
            }

            // 订阅视图模型的属性变化事件
            // 当窗口状态改变时（如从 Windowed 变为 Pinned），会触发此事件
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            
            // 订阅 AppWindow 的状态变化事件（OS 层面的窗口状态变化）
            // 例如用户通过 Windows 系统按钮最大化/还原窗口时触发
            this.AppWindow.Changed += OnAppWindowChanged;
            
            // 订阅窗口关闭事件，用于在窗口关闭时清理所有事件订阅
            // 防止内存泄漏（事件订阅会持有对象引用）
            this.Closed += OnWindowClosed;
        }

        /// <summary>
        /// AppWindow 状态变化事件处理函数
        /// 当 OS 层面的窗口状态发生变化时触发（如用户通过系统按钮最大化窗口）
        /// </summary>
        /// <param name="sender">AppWindow 对象</param>
        /// <param name="args">变化事件参数，包含变化类型信息</param>
        private void OnAppWindowChanged(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
        {
            // 检查是否发生了窗口呈现器变化（最大化/还原/最小化）或窗口大小变化
            if (args.DidPresenterChange || args.DidSizeChange)
            {
                // 更新导航栏中的窗口状态图标（最大化图标 ⬜ 或还原图标 ⧉）
                UpdateWindowStateIcon();
                // 更新内容区域的顶部边距（最大化或停靠时需要调整边距）
                UpdateContentTopMargin();
                
                // 如果窗口呈现器发生变化（用户通过系统按钮操作了窗口）
                if (args.DidPresenterChange)
                {
                    // 确定当前 OS 层面的窗口状态（Maximized、Windowed、Hidden 等）
                    WindowState osState = DetermineOSWindowState();
                    // 将 OS 状态同步到应用层的状态管理器
                    // 这样应用层的状态（_viewModel.CurrentState）就能与 OS 状态保持一致
                    _windowController.SyncFromOSWindowState(osState);
                }
            }
        }
        
        /// <summary>
        /// 确定当前 OS 层面的窗口状态
        /// 将 Windows 系统的窗口状态映射到应用层的 WindowState 枚举
        /// </summary>
        /// <returns>应用层的窗口状态枚举值</returns>
        private WindowState DetermineOSWindowState()
        {
            // 检查窗口的呈现器是否为 OverlappedPresenter（标准桌面窗口呈现器）
            if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                // 使用 switch 表达式将 OS 状态映射到应用状态
                return presenter.State switch
                {
                    // OS 最大化状态 → 应用最大化状态
                    Microsoft.UI.Windowing.OverlappedPresenterState.Maximized => WindowState.Maximized,
                    // OS 还原状态（正常窗口）→ 应用窗口化状态
                    Microsoft.UI.Windowing.OverlappedPresenterState.Restored => WindowState.Windowed,
                    // OS 最小化状态 → 应用隐藏状态
                    Microsoft.UI.Windowing.OverlappedPresenterState.Minimized => WindowState.Hidden,
                    // 其他未知状态 → 保持当前应用状态不变
                    _ => _viewModel.CurrentState // Fallback to current state
                };
            }
            
            // 如果呈现器类型不是 OverlappedPresenter，保持当前状态不变
            return _viewModel.CurrentState; // Fallback to current state
        }

        /// <summary>
        /// 更新导航栏中的窗口状态图标
        /// 根据窗口是否最大化显示不同的图标（最大化图标或还原图标）
        /// </summary>
        private void UpdateWindowStateIcon()
        {
            // 默认假设窗口未最大化
            bool isMaximized = false;
            // 检查窗口呈现器是否为 OverlappedPresenter
            if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                // 判断窗口是否处于最大化状态
                isMaximized = presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;
            }

            // 检查根网格是否包含 Linker 组件
            if (RootGrid.Children.Count > 0 && RootGrid.Children[0] is Linker linker)
            {
                // 调用导航栏实例的方法更新图标
                // 传入 isMaximized 参数，导航栏会根据此值显示对应图标
                linker.NavBarInstance.UpdateWindowStateIcon(isMaximized);
            }
        }

        /// <summary>
        /// 窗口状态切换请求事件处理函数（异步）
        /// 当用户点击导航栏的最大化/还原按钮时触发
        /// </summary>
        /// <param name="sender">事件发送者（通常是 Linker 组件）</param>
        /// <param name="e">事件参数</param>
        private async void OnWindowStateToggleRequested(object? sender, System.EventArgs e)
        {
            // 委托给窗口主机控制器处理最大化/还原切换逻辑
            // 控制器会根据当前状态决定是最大化还是还原窗口
            _windowController.ToggleMaximize();
        }

        /// <summary>
        /// 切换窗口状态（最大化/还原）的公共方法
        /// 实现 IWindowToggle 接口的一部分
        /// </summary>
        public void ToggleWindowState()
        {
            // 委托给窗口主机控制器处理
            _windowController.ToggleMaximize();
        }

        /// <summary>
        /// 视图模型属性变化事件处理函数
        /// 当窗口状态（Docked、Pinned、Windowed 等）发生变化时触发
        /// </summary>
        /// <param name="sender">事件发送者（视图模型）</param>
        /// <param name="e">属性变化事件参数，包含变化的属性名称</param>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 检查变化的属性是否为 CurrentState（当前窗口状态）
            if (e.PropertyName == nameof(MainWindowViewModel.CurrentState))
            {
                // 获取当前窗口状态
                var currentState = _viewModel.CurrentState;
                // 判断窗口是否处于停靠（Pinned）状态
                bool isPinned = currentState == WindowState.Pinned;
                
                // 更新导航栏中的停靠切换图标（停靠图标 📌 或取消停靠图标）
                UpdateDockToggleIcon(isPinned);
                // 更新内容区域的圆角半径（停靠时无圆角，非停靠时有圆角）
                UpdateContentCornerRadius(isPinned);
                // 更新内容区域的顶部边距（停靠或最大化时需要调整边距）
                UpdateContentTopMargin();
            }
        }

        /// <summary>
        /// 更新导航栏中的停靠切换图标
        /// 根据窗口是否停靠显示不同的图标
        /// </summary>
        /// <param name="isPinned">窗口是否处于停靠状态</param>
        private void UpdateDockToggleIcon(bool isPinned)
        {
            // 检查根网格是否包含 Linker 组件
            if (RootGrid.Children.Count > 0 && 
                RootGrid.Children[0] is Linker linker)
            {
                // 调用导航栏实例的方法更新停靠图标
                // 传入 isPinned 参数，导航栏会根据此值显示对应图标
                linker.NavBarInstance.UpdateDockToggleIcon(isPinned);
            }
        }

        /// <summary>
        /// 更新内容区域的圆角半径
        /// 停靠状态下无圆角，非停靠状态下有圆角（美观效果）
        /// </summary>
        /// <param name="isPinned">窗口是否处于停靠状态</param>
        private void UpdateContentCornerRadius(bool isPinned)
        {
            // 检查根网格是否包含 Linker 组件
            if (RootGrid.Children.Count > 0 && 
                RootGrid.Children[0] is Linker linker)
            {
                // 调用 Linker 的方法更新内容区域的圆角半径
                // 停靠时圆角为 0，非停靠时圆角为预设值（如 8px）
                linker.UpdateContentCornerRadius(isPinned);
            }
        }

        /// <summary>
        /// 更新内容区域的顶部边距
        /// 停靠或最大化状态下需要调整边距以适应窗口布局
        /// </summary>
        private void UpdateContentTopMargin()
        {
            // 默认假设窗口未最大化
            bool isMaximized = false;
            // 检查窗口呈现器是否为 OverlappedPresenter
            if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                // 判断窗口是否处于最大化状态
                isMaximized = presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;
            }

            // 获取当前窗口状态
            var currentState = _viewModel.CurrentState;
            // 判断窗口是否处于停靠或最大化状态
            bool isPinnedOrMaximized = currentState == WindowState.Pinned || isMaximized;

            // 检查根网格是否包含 Linker 组件
            if (RootGrid.Children.Count > 0 && 
                RootGrid.Children[0] is Linker linker)
            {
                // 调用 Linker 的方法更新内容区域的顶部边距
                // 停靠或最大化时边距较小，正常窗口时边距较大
                linker.UpdateContentTopMargin(isPinnedOrMaximized);
            }
        }

        /// <summary>
        /// 切换窗口显示/隐藏状态的公共方法
        /// 实现 IWindowToggle 接口，供托盘图标管理器调用
        /// </summary>
        public void ToggleWindow()
        {
            // 委托给窗口主机控制器处理显示/隐藏逻辑
            _windowController.ToggleWindow();
        }

        /// <summary>
        /// 切换停靠状态的公共方法
        /// 在停靠（Pinned）和非停靠状态之间切换
        /// </summary>
        public void TogglePinnedDock()
        {
            // 委托给窗口主机控制器处理停靠/取消停靠逻辑
            _windowController.TogglePinnedDock();
        }

        /// <summary>
        /// 停靠切换请求事件处理函数（异步）
        /// 当用户点击导航栏的停靠按钮时触发
        /// </summary>
        /// <param name="sender">事件发送者（通常是 Linker 组件）</param>
        /// <param name="e">事件参数</param>
        private async void OnDockToggleRequested(object? sender, System.EventArgs e)
        {
            // 委托给窗口主机控制器处理停靠/取消停靠逻辑
            // StateManager 会自动处理从 Maximized 到 Pinned 的转换
            TogglePinnedDock();
        }

        /// <summary>
        /// 导航到新建页面并传入 URL
        /// 用于处理分享目标激活或其他需要打开特定 URL 的场景
        /// </summary>
        /// <param name="url">要导航到的 URL 地址</param>
        public void NavigateToNewPage(string url)
        {
            // 输出调试信息：记录方法调用和传入的 URL
            System.Diagnostics.Debug.WriteLine($"MainWindow.NavigateToNewPage called with URL: {url}");
            // 输出调试信息：记录根网格的子元素数量
            System.Diagnostics.Debug.WriteLine($"RootGrid.Children.Count: {RootGrid.Children.Count}");
            
            // 检查根网格是否包含 Linker 组件
            if (RootGrid.Children.Count > 0 && RootGrid.Children[0] is Linker linker)
            {
                // 输出调试信息：确认找到 Linker 组件
                System.Diagnostics.Debug.WriteLine("Linker found, calling NavigateToNewPage");
                // 调用 Linker 的导航方法，传入 URL
                // Linker 会负责将 URL 传递给内容区域进行页面导航
                linker.NavigateToNewPage(url);
            }
            else
            {
                // 输出调试信息：未找到 Linker 组件（异常情况）
                System.Diagnostics.Debug.WriteLine("Linker NOT found!");
            }
        }
        
        /// <summary>
        /// 窗口关闭事件处理函数
        /// 在窗口关闭时取消所有事件订阅，防止内存泄漏
        /// </summary>
        /// <param name="sender">事件发送者（窗口对象）</param>
        /// <param name="args">窗口事件参数</param>
        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            // 取消订阅视图模型的属性变化事件
            // 防止视图模型持有窗口的引用导致内存泄漏
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            // 取消订阅 AppWindow 的状态变化事件
            this.AppWindow.Changed -= OnAppWindowChanged;
            // 取消订阅窗口关闭事件（自身）
            this.Closed -= OnWindowClosed;
            
            // 检查根网格是否包含 Linker 组件
            if (RootGrid.Children.Count > 0 && RootGrid.Children[0] is Linker linker)
            {
                // 取消订阅 Linker 的停靠切换请求事件
                linker.DockToggleRequested -= OnDockToggleRequested;
                // 取消订阅 Linker 的窗口状态切换请求事件
                linker.WindowStateToggleRequested -= OnWindowStateToggleRequested;
            }
        }
    }
}
