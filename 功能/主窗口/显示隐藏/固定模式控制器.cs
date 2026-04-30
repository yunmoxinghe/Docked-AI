using Docked_AI.Features.MainWindow.Appearance;
using Docked_AI.Features.MainWindow.Placement;
using Microsoft.UI.Xaml;
using System;

namespace Docked_AI.Features.MainWindow.Visibility
{
    /// <summary>
    /// 固定模式控制器 - 封装固定模式（AppBar）的进入和退出逻辑
    ///
    /// 【文件职责】
    /// 1. 进入固定模式：应用无边框窗口样式、注册 AppBar、设置 TopMost
    /// 2. 退出固定模式：还原标准窗口样式、注销 AppBar、取消 TopMost
    /// 3. 管理 AppBar 注册状态，确保注册/注销成对调用
    /// 4. 封装所有与固定模式相关的 Win32 API 调用
    ///
    /// 【架构设计】
    /// 从 WindowHostController 中提取，职责单一：
    /// - WindowHostController 负责协调所有状态转换
    /// - PinnedModeController 只负责固定模式的进入/退出细节
    ///
    /// 【进入固定模式流程】
    ///   1. ApplyPinnedWindowStyle()  - 移除边框/标题栏，设置 DWM 属性
    ///   2. ApplyPinnedBounds()       - 注册 AppBar，计算并应用固定位置
    ///   3. 调用方负责设置背景（Mica）和激活窗口
    ///
    /// 【退出固定模式流程】
    ///   1. RemoveAppBar()            - 注销 AppBar，释放屏幕空间
    ///   2. RestoreStandardWindowStyle() - 还原边框/标题栏样式
    ///   3. 调用方负责还原背景（Acrylic）和标题栏配置
    ///
    /// 【重构风险点】
    /// 1. ApplyPinnedWindowStyle 和 RestoreStandardWindowStyle 必须成对调用
    ///    - 如果只调用进入不调用退出，窗口样式会永久保持固定模式
    /// 2. RegisterAppBarIfNeeded 和 RemoveAppBar 必须成对调用
    ///    - 如果忘记注销，AppBar 会一直占用屏幕右侧空间
    /// 3. _isApplyingPinnedBounds 防重入标志
    ///    - ApplyPinnedBounds 会触发 AppWindow.Changed 事件
    ///    - 必须用此标志防止事件处理器递归调用 ApplyPinnedBounds
    /// 4. 基础样式只捕获一次（_hasCapturedBaseWindowStyle）
    ///    - 首次进入固定模式时捕获，后续复用
    ///    - 如果在捕获前窗口样式已被修改，还原后样式可能不正确
    /// </summary>
    internal sealed class PinnedModeController
    {
        private readonly Window _window;
        private readonly WindowLayoutState _state;
        private readonly uint _appBarMessageId;

        // AppBar 注册状态
        private bool _isAppBarRegistered;

        // 防重入标志（ApplyPinnedBounds 会触发 AppWindow.Changed）
        internal bool IsApplyingPinnedBounds { get; private set; }

        // 基础窗口样式（进入固定模式前捕获，退出时还原）
        private bool _hasCapturedBaseWindowStyle;
        private bool _hasCapturedBaseExtendedWindowStyle;
        private IntPtr _baseWindowStyle;
        private IntPtr _baseExtendedWindowStyle;

        // 窗口句柄（由外部提供，延迟初始化）
        private IntPtr _hwnd;

        public PinnedModeController(Window window, WindowLayoutState state, uint appBarMessageId)
        {
            _window = window;
            _state = state;
            _appBarMessageId = appBarMessageId;
        }

        /// <summary>
        /// 提供窗口句柄（在窗口创建后由 WindowHostController 调用）
        /// </summary>
        public void SetWindowHandle(IntPtr hwnd)
        {
            _hwnd = hwnd;
        }

        // ==================== 进入固定模式 ====================

        /// <summary>
        /// 进入固定模式：应用无边框样式并注册 AppBar
        ///
        /// 【调用顺序】
        /// 必须先调用 ApplyPinnedWindowStyle，再调用 ApplyPinnedBounds
        /// ApplyPinnedBounds 依赖 AppBar 注册，而注册需要窗口句柄有效
        /// </summary>
        public void EnterPinnedMode()
        {
            ApplyPinnedWindowStyle();
            ApplyPinnedBounds();
        }

        /// <summary>
        /// 应用固定模式窗口样式：移除边框/标题栏，设置 DWM 无圆角/无边框颜色
        ///
        /// 【副作用】
        /// - 修改 GWL_STYLE：移除 WS_OVERLAPPEDWINDOW，添加 WS_POPUP
        /// - 修改 GWL_EXSTYLE：移除所有边框扩展样式
        /// - 调用 DwmExtendFrameIntoClientArea（margins 全为 0）
        /// - 调用 DwmSetWindowAttribute 设置圆角/边框/标题栏颜色
        /// - 调用 RefreshWindowFrame 使样式立即生效
        /// </summary>
        public void ApplyPinnedWindowStyle()
        {
            if (_hwnd == IntPtr.Zero) return;

            // 配置 AppWindow Presenter（禁用边框、标题栏、调整大小）
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
                presenter.IsResizable = false;
                presenter.IsAlwaysOnTop = true;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
            }

            // 捕获基础样式（只捕获一次，用于退出时还原）
            IntPtr currentStyle = AppearanceWin32Api.GetWindowLongPtr(_hwnd, AppearanceWin32Api.GWL_STYLE);
            IntPtr currentExtendedStyle = AppearanceWin32Api.GetWindowLongPtr(_hwnd, AppearanceWin32Api.GWL_EXSTYLE);

            if (!_hasCapturedBaseWindowStyle)
            {
                _baseWindowStyle = currentStyle;
                _hasCapturedBaseWindowStyle = true;
            }
            if (!_hasCapturedBaseExtendedWindowStyle)
            {
                _baseExtendedWindowStyle = currentExtendedStyle;
                _hasCapturedBaseExtendedWindowStyle = true;
            }

            // 修改窗口样式：移除所有边框/标题栏，只保留 POPUP + VISIBLE
            int style = currentStyle.ToInt32();
            style &= ~AppearanceWin32Api.WS_OVERLAPPEDWINDOW;
            style &= ~AppearanceWin32Api.WS_CAPTION;
            style &= ~AppearanceWin32Api.WS_SYSMENU;
            style &= ~AppearanceWin32Api.WS_THICKFRAME;
            style &= ~AppearanceWin32Api.WS_BORDER;
            style &= ~AppearanceWin32Api.WS_DLGFRAME;
            style |= AppearanceWin32Api.WS_POPUP;
            style |= AppearanceWin32Api.WS_VISIBLE;

            int extendedStyle = currentExtendedStyle.ToInt32();
            extendedStyle &= ~AppearanceWin32Api.WS_EX_DLGMODALFRAME;
            extendedStyle &= ~AppearanceWin32Api.WS_EX_WINDOWEDGE;
            extendedStyle &= ~AppearanceWin32Api.WS_EX_CLIENTEDGE;
            extendedStyle &= ~AppearanceWin32Api.WS_EX_STATICEDGE;

            _ = AppearanceWin32Api.SetWindowLongPtr(_hwnd, AppearanceWin32Api.GWL_STYLE, new IntPtr(style));
            _ = AppearanceWin32Api.SetWindowLongPtr(_hwnd, AppearanceWin32Api.GWL_EXSTYLE, new IntPtr(extendedStyle));

            // DWM：将框架延伸到客户区（margins 全为 0，消除系统边框视觉残留）
            AppearanceWin32Api.MARGINS margins = new() { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
            _ = AppearanceWin32Api.DwmExtendFrameIntoClientArea(_hwnd, ref margins);

            // DWM：不要圆角
            int cornerPreference = AppearanceWin32Api.DWMWCP_DONOTROUND;
            _ = AppearanceWin32Api.DwmSetWindowAttribute(
                _hwnd,
                AppearanceWin32Api.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref cornerPreference,
                sizeof(int));

            // DWM：移除边框颜色
            int borderColor = AppearanceWin32Api.DWMWA_COLOR_NONE;
            _ = AppearanceWin32Api.DwmSetWindowAttribute(
                _hwnd,
                AppearanceWin32Api.DWMWA_BORDER_COLOR,
                ref borderColor,
                sizeof(int));

            // DWM：标题栏颜色设为几乎透明（让 Mica 背景透过）
            int captionColor = 0x01000000;
            _ = AppearanceWin32Api.DwmSetWindowAttribute(
                _hwnd,
                AppearanceWin32Api.DWMWA_CAPTION_COLOR,
                ref captionColor,
                sizeof(int));

            RefreshWindowFrame();

            System.Diagnostics.Debug.WriteLine("PinnedModeController.ApplyPinnedWindowStyle: All styles and DWM attributes applied");
        }

        /// <summary>
        /// 应用固定模式边界：注册 AppBar，查询并设置窗口位置
        ///
        /// 【前置条件】
        /// 调用方（WindowHostController）必须在调用此方法前先刷新布局状态
        ///
        /// 【副作用】
        /// - 注册 AppBar（SHAppBarMessage ABM_NEW）
        /// - 查询 AppBar 位置（ABM_QUERYPOS）
        /// - 设置 AppBar 位置（ABM_SETPOS）
        /// - 更新 _state 中的位置和尺寸
        /// - 调用 ApplyPinnedWindowFrame 精确对齐窗口
        /// </summary>
        public void ApplyPinnedBounds()
        {
            RegisterAppBarIfNeeded();

            VisibilityWin32Api.APPBARDATA appBarData = CreateAppBarData();
            int desiredWidth = _state.WindowWidth;

            appBarData.uEdge = VisibilityWin32Api.ABE_RIGHT;
            appBarData.rc.Top = _state.WorkArea.Top;
            appBarData.rc.Bottom = _state.WorkArea.Bottom;
            appBarData.rc.Right = _state.WorkArea.Right;
            appBarData.rc.Left = appBarData.rc.Right - desiredWidth;

            _ = VisibilityWin32Api.SHAppBarMessage(VisibilityWin32Api.ABM_QUERYPOS, ref appBarData);

            appBarData.rc.Top = _state.WorkArea.Top;
            appBarData.rc.Bottom = _state.WorkArea.Bottom;
            appBarData.rc.Right = _state.WorkArea.Right;
            appBarData.rc.Left = appBarData.rc.Right - desiredWidth;

            _ = VisibilityWin32Api.SHAppBarMessage(VisibilityWin32Api.ABM_SETPOS, ref appBarData);

            int width = Math.Max(_state.MinWindowWidth, appBarData.rc.Right - appBarData.rc.Left);
            int height = Math.Max(1, appBarData.rc.Bottom - appBarData.rc.Top);

            _state.WindowWidth = width;
            _state.WindowHeight = height;
            _state.TargetX = appBarData.rc.Left;
            _state.TargetY = appBarData.rc.Top;
            _state.CurrentX = _state.TargetX;
            _state.CurrentY = _state.TargetY;

            IsApplyingPinnedBounds = true;
            try
            {
                ApplyPinnedWindowFrame(appBarData.rc);
            }
            finally
            {
                IsApplyingPinnedBounds = false;
            }
        }

        // ==================== 退出固定模式 ====================

        /// <summary>
        /// 退出固定模式：注销 AppBar 并还原标准窗口样式
        ///
        /// 【调用顺序】
        /// 必须先调用 RemoveAppBar，再调用 RestoreStandardWindowStyle
        /// 确保屏幕空间在样式还原前已释放
        /// </summary>
        public void ExitPinnedMode()
        {
            RemoveAppBar();
            RestoreStandardWindowStyle();
        }

        /// <summary>
        /// 注销 AppBar，释放屏幕右侧占用的空间
        /// 幂等操作：未注册时调用无副作用
        /// </summary>
        public void RemoveAppBar()
        {
            if (!_isAppBarRegistered) return;

            VisibilityWin32Api.APPBARDATA appBarData = CreateAppBarData();
            _ = VisibilityWin32Api.SHAppBarMessage(VisibilityWin32Api.ABM_REMOVE, ref appBarData);
            _isAppBarRegistered = false;
        }

        /// <summary>
        /// 还原标准窗口样式（进入固定模式前捕获的基础样式）
        /// 幂等操作：未捕获基础样式时调用无副作用
        /// </summary>
        public void RestoreStandardWindowStyle()
        {
            if (_hwnd == IntPtr.Zero || !_hasCapturedBaseWindowStyle) return;

            _ = AppearanceWin32Api.SetWindowLongPtr(_hwnd, AppearanceWin32Api.GWL_STYLE, _baseWindowStyle);
            if (_hasCapturedBaseExtendedWindowStyle)
            {
                _ = AppearanceWin32Api.SetWindowLongPtr(_hwnd, AppearanceWin32Api.GWL_EXSTYLE, _baseExtendedWindowStyle);
            }
            RefreshWindowFrame();
        }

        // ==================== 内部辅助方法 ====================

        private void RegisterAppBarIfNeeded()
        {
            if (_isAppBarRegistered) return;

            VisibilityWin32Api.APPBARDATA appBarData = CreateAppBarData();
            _ = VisibilityWin32Api.SHAppBarMessage(VisibilityWin32Api.ABM_NEW, ref appBarData);
            _isAppBarRegistered = true;
        }

        private VisibilityWin32Api.APPBARDATA CreateAppBarData()
        {
            return new VisibilityWin32Api.APPBARDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<VisibilityWin32Api.APPBARDATA>(),
                hWnd = _hwnd,
                uCallbackMessage = _appBarMessageId
            };
        }

        /// <summary>
        /// 精确对齐固定模式窗口位置，补偿 DWM 扩展边框偏移
        /// </summary>
        private void ApplyPinnedWindowFrame(PlacementWin32Api.RECT approvedRect)
        {
            if (_hwnd == IntPtr.Zero) return;

            ApplyWindowRect(approvedRect);

            if (TryGetExtendedFrameBounds(out PlacementWin32Api.RECT actualBounds))
            {
                PlacementWin32Api.RECT correctedRect = new()
                {
                    Left = approvedRect.Left + (approvedRect.Left - actualBounds.Left),
                    Top = approvedRect.Top + (approvedRect.Top - actualBounds.Top),
                    Right = approvedRect.Right + (approvedRect.Right - actualBounds.Right),
                    Bottom = approvedRect.Bottom + (approvedRect.Bottom - actualBounds.Bottom)
                };

                ApplyWindowRect(correctedRect);
                approvedRect = correctedRect;
            }

            _state.TargetX = approvedRect.Left;
            _state.TargetY = approvedRect.Top;
            _state.CurrentX = approvedRect.Left;
            _state.CurrentY = approvedRect.Top;
            _state.WindowWidth = Math.Max(_state.MinWindowWidth, approvedRect.Right - approvedRect.Left);
            _state.WindowHeight = Math.Max(1, approvedRect.Bottom - approvedRect.Top);
        }

        private void ApplyWindowRect(PlacementWin32Api.RECT rect)
        {
            int width = Math.Max(_state.MinWindowWidth, rect.Right - rect.Left);
            int height = Math.Max(1, rect.Bottom - rect.Top);

            _ = VisibilityWin32Api.SetWindowPos(
                _hwnd,
                VisibilityWin32Api.HWND_TOPMOST,
                rect.Left,
                rect.Top,
                width,
                height,
                VisibilityWin32Api.SWP_SHOWWINDOW);
        }

        private bool TryGetExtendedFrameBounds(out PlacementWin32Api.RECT bounds)
        {
            if (_hwnd == IntPtr.Zero)
            {
                bounds = default;
                return false;
            }

            int hr = AppearanceWin32Api.DwmGetWindowAttribute(
                _hwnd,
                AppearanceWin32Api.DWMWA_EXTENDED_FRAME_BOUNDS,
                out bounds,
                System.Runtime.InteropServices.Marshal.SizeOf<PlacementWin32Api.RECT>());

            return hr >= 0;
        }

        private void RefreshWindowFrame()
        {
            if (_hwnd == IntPtr.Zero) return;

            _ = VisibilityWin32Api.SetWindowPos(
                _hwnd,
                IntPtr.Zero,
                0, 0, 0, 0,
                VisibilityWin32Api.SWP_NOSIZE |
                VisibilityWin32Api.SWP_NOMOVE |
                VisibilityWin32Api.SWP_NOZORDER |
                VisibilityWin32Api.SWP_NOACTIVATE |
                VisibilityWin32Api.SWP_FRAMECHANGED);
        }
    }
}
