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

        // ShowWindow 参数
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        // GetSystemMetrics 参数
        private const int SM_CXSCREEN = 0;  // 屏幕宽度
        private const int SM_CYSCREEN = 1;  // 屏幕高度

        private IntPtr hwnd;
        private double targetY;
        private double currentY;
        private int targetX;
        private int windowWidth;
        private int windowHeight;
        private bool animationStarted = false;
        private bool isVisible = true; // 窗口可见状态
        private int screenHeight; // 保存屏幕高度

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

                // 获取屏幕尺寸
                screenHeight = GetSystemMetrics(SM_CYSCREEN);
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);

                // 获取窗口尺寸
                windowWidth = (int)this.AppWindow.ClientSize.Width;
                windowHeight = (int)this.AppWindow.ClientSize.Height;

                // 计算位置
                targetX = (screenWidth - windowWidth) / 2; // 水平居中
                int startY = screenHeight; // 从屏幕底部开始

                // 最终目标位置
                targetY = screenHeight / 2 - windowHeight / 2;
                currentY = startY;

                // 设置初始位置
                SetWindowPos(hwnd, IntPtr.Zero, targetX, (int)currentY, windowWidth, windowHeight, 0);

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
            
            // 设置目标位置为屏幕中央
            targetY = screenHeight / 2 - windowHeight / 2;
            currentY = screenHeight; // 从屏幕底部开始

            // 设置初始位置
            SetWindowPos(hwnd, IntPtr.Zero, targetX, (int)currentY, windowWidth, windowHeight, 0);
            
            // 激活窗口并开始动画
            this.Activate();
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnFrame;
        }

        private void HideWindow()
        {
            isVisible = false;
            
            // 设置目标位置为屏幕底部之外
            targetY = screenHeight + windowHeight;
            currentY = screenHeight / 2 - windowHeight / 2; // 从当前位置开始

            // 开始隐藏动画
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnFrame;
        }

        private void OnFrame(object sender, object e)
        {
            double distance = targetY - currentY;
            double speed;
            
            if (isVisible)
            {
                // 显示动画：减速缓动（先快后慢）
                speed = distance * 0.4;
            }
            else
            {
                // 隐藏动画：加速缓动（先慢后快）
                // 使用反向公式：速度随着接近目标而增加
                double totalDistance = screenHeight + windowHeight - (screenHeight / 2 - windowHeight / 2);
                double remainingRatio = Math.Abs(distance) / totalDistance;
                double accelerationFactor = 1.0 - remainingRatio; // 越接近目标，加速因子越大
                speed = distance * (0.2 + accelerationFactor * 0.4); // 基础速度0.2，最大0.6
            }
            
            currentY += speed;

            // 判断是否到位
            if (Math.Abs(distance) < 1)
            {
                currentY = targetY;
                // 停止动画
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnFrame;
                
                // 如果是隐藏动画完成，隐藏窗口
                if (!isVisible)
                {
                    ShowWindow(hwnd, SW_HIDE);
                }
            }

            // 更新窗口位置
            SetWindowPos(hwnd, IntPtr.Zero, targetX, (int)currentY, windowWidth, windowHeight, 0);
        }
    }
}
