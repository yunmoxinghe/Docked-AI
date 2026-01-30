using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;

namespace Docked_AI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(int uAction, int uParam, ref RECT lpvParam, int fuWinIni);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // ShowWindow 参数
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        // GetSystemMetrics 参数
        private const int SM_CXSCREEN = 0;  // 屏幕宽度
        private const int SM_CYSCREEN = 1;  // 屏幕高度

        // SystemParametersInfo 参数
        private const int SPI_GETWORKAREA = 0x0030;  // 获取工作区域（排除任务栏）

        private IntPtr hwnd;
        private double targetY;
        private double currentY;
        private double currentX; // 当前X位置
        private int targetX;
        private int windowWidth;
        private int windowHeight;
        private bool animationStarted = false;
        private bool isVisible = true; // 窗口可见状态
        private int screenHeight; // 保存屏幕高度
        private int screenWidth; // 保存屏幕宽度
        private RECT workArea; // 工作区域（排除任务栏）
        private const int MARGIN = 10; // 距离边缘的逻辑像素距离

        // 公共属性：检查窗口是否可见
        public bool IsWindowVisible => isVisible;

        public MainWindow()
        {
            this.InitializeComponent();

            // Enable the custom title bar
            ExtendsContentIntoTitleBar = true;

            this.Activated += MainWindow_Activated;
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            // 只在首次激活时执行动画
            if (args.WindowActivationState != WindowActivationState.Deactivated && !animationStarted)
            {
                animationStarted = true;
                this.Activated -= MainWindow_Activated; // 移除事件处理器

                // 获取窗口句柄
                hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

                if (hwnd == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("警告: 无法获取有效的窗口句柄");
                    return;
                }

                // 隐藏任务栏图标
                this.AppWindow.IsShownInSwitchers = false;

                // 获取屏幕尺寸和工作区域
                screenHeight = GetSystemMetrics(SM_CYSCREEN);
                screenWidth = GetSystemMetrics(SM_CXSCREEN);
                
                // 获取工作区域（排除任务栏）
                SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0);

                // 设置窗口尺寸
                windowWidth = 500; // 固定宽度500像素
                // 计算窗口高度：工作区域高度减去上下各10像素边距
                windowHeight = workArea.Bottom - workArea.Top - (MARGIN * 2);

                // 计算目标位置：右边缘10px，上边缘10px
                targetX = workArea.Right - windowWidth - MARGIN; // 距离右边缘10px
                targetY = workArea.Top + MARGIN; // 距离上边缘10px
                
                // 动画起始位置：从屏幕右侧外部滑入
                currentX = screenWidth; // 从屏幕右边缘外开始
                currentY = targetY; // Y位置保持不变

                // 设置初始位置
                SetWindowPos(hwnd, IntPtr.Zero, (int)currentX, (int)currentY, windowWidth, windowHeight, 0);

                // 开始动画
                StartSlideAnimation();
            }
        }

        private void StartSlideAnimation()
        {
            // 用 CompositionTarget.Rendering 高帧率更新窗口位置
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnFrame;
        }

        // 公共方法：切换窗口显示/隐藏
        public void ToggleWindow()
        {
            if (hwnd == IntPtr.Zero)
            {
                hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            }

            if (isVisible)
            {
                // 当前可见，执行隐藏动画
                HideWindow();
            }
            else
            {
                // 当前隐藏，执行显示动画
                ShowWindow();
            }
        }

        private void ShowWindow()
        {
            isVisible = true;
            
            // 隐藏任务栏图标
            this.AppWindow.IsShownInSwitchers = false;
            
            // 显示窗口
            ShowWindow(hwnd, SW_SHOW);
            
            // 重新获取工作区域（防止任务栏位置变化）
            SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0);
            
            // 重新计算窗口高度
            windowHeight = workArea.Bottom - workArea.Top - (MARGIN * 2);
            
            // 设置目标位置
            targetX = workArea.Right - windowWidth - MARGIN;
            targetY = workArea.Top + MARGIN;
            
            // 从屏幕右侧外部开始
            currentX = screenWidth;
            currentY = targetY;

            // 设置初始位置
            SetWindowPos(hwnd, IntPtr.Zero, (int)currentX, (int)currentY, windowWidth, windowHeight, 0);
            
            // 激活窗口并开始动画
            this.Activate();
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnFrame;
        }

        private void HideWindow()
        {
            isVisible = false;
            
            // 如果窗口当前是最大化状态，先恢复到正常状态
            if (this.AppWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped)
            {
                var overlappedPresenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                if (overlappedPresenter != null && overlappedPresenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
                {
                    // 恢复到正常窗口状态
                    overlappedPresenter.Restore();
                    
                    // 重新设置窗口位置和尺寸到我们期望的停靠位置
                    // 重新获取工作区域
                    SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0);
                    
                    // 重新计算窗口尺寸和位置
                    windowHeight = workArea.Bottom - workArea.Top - (MARGIN * 2);
                    int normalX = workArea.Right - windowWidth - MARGIN;
                    int normalY = workArea.Top + MARGIN;
                    
                    // 设置到正常的停靠位置
                    SetWindowPos(hwnd, IntPtr.Zero, normalX, normalY, windowWidth, windowHeight, 0);
                    
                    // 更新当前位置
                    currentX = normalX;
                    currentY = normalY;
                }
            }
            
            // 设置目标位置为屏幕右侧外部
            targetX = screenWidth;
            // 如果没有设置currentX，使用当前停靠位置
            if (currentX == 0)
            {
                currentX = workArea.Right - windowWidth - MARGIN;
            }
            targetY = workArea.Top + MARGIN;
            currentY = targetY;

            // 开始隐藏动画
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnFrame;
        }

        private void OnFrame(object? sender, object e)
        {
            double distance = targetX - currentX;
            double speed;
            
            if (isVisible)
            {
                // 显示动画：减速缓动（先快后慢）
                speed = distance * 0.4;
            }
            else
            {
                // 隐藏动画：加速缓动（先慢后快）
                double totalDistance = screenWidth - (workArea.Right - windowWidth - MARGIN);
                double remainingRatio = Math.Abs(distance) / totalDistance;
                double accelerationFactor = 1.0 - remainingRatio;
                speed = distance * (0.2 + accelerationFactor * 0.4);
            }
            
            currentX += speed;

            // 判断是否到位
            if (Math.Abs(distance) < 1)
            {
                currentX = targetX;
                // 停止动画
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnFrame;
                
                // 如果是隐藏动画完成，隐藏窗口
                if (!isVisible)
                {
                    ShowWindow(hwnd, SW_HIDE);
                }
            }

            // 更新窗口位置
            SetWindowPos(hwnd, IntPtr.Zero, (int)currentX, (int)currentY, windowWidth, windowHeight, 0);
        }
    }
}
