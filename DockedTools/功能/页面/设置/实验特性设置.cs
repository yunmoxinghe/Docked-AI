using System;
using Windows.Storage;
using DockedTools.Features.Shared.AotOptimization;

namespace DockedTools.Features.Pages.Settings
{
    /// <summary>
    /// 实验特性设置管理（AOT 优化版本）
    /// 
    /// <para>
    /// 使用 AotSafeSettingsHelper 确保在 Native AOT 编译环境下设置的读写操作安全可靠。
    /// </para>
    /// </summary>
    public static class ExperimentalSettings
    {
        private const string EnableWinUIContextMenuKey = "ExperimentalFeature_EnableWinUIContextMenu";
        private const string MaxWebViewCountKey = "WebSettings_MaxWebViewCount";
        private const string FrameNavigationAnimationKey = "NavigationSettings_FrameAnimation";
        private const string SubPageNavigationAnimationKey = "NavigationSettings_SubPageAnimation";
        private const string EnableAILabKey = "ExperimentalFeature_EnableAILab";
        private const string EnableBackButtonKey = "NavigationSettings_EnableBackButton";
        private const string EnableTopBarBackButtonKey = "TopBarSettings_EnableBackButton";
        private const string EnableTopBarMenuButtonKey = "TopBarSettings_EnableMenuButton";
        private const string WindowDockSideKey = "WindowSettings_DockSide";
        private const string PlaceNavigationBarOnLeftWhenDockedLeftKey = "WindowSettings_PlaceNavigationBarOnLeftWhenDockedLeft";
        private const string TrayCloseWindowBehaviorKey = "TraySettings_CloseWindowBehavior";
        private const string HideTrayRateButtonKey = "TraySettings_HideTrayRateButton";
        private const string HideWebViewCloseButtonKey = "WebSettings_HideCloseButton";
        
        // 外观设置
        private const string ContentAreaBackdropTypeKey = "AppearanceSettings_ContentAreaBackdropType";
        
        // WebView2 性能优化设置
        private const string WebViewMemoryModeKey = "WebSettings_MemoryMode";
        private const string WebViewAutoClearCacheKey = "WebSettings_AutoClearCache";
        private const string WebViewSuspendInactiveKey = "WebSettings_SuspendInactive";
        private const string WebViewDisableBackgroundNetworkKey = "WebSettings_DisableBackgroundNetwork";
        private const string WebViewDisableExtensionsKey = "WebSettings_DisableExtensions";
        private const string WebViewDisablePluginsKey = "WebSettings_DisablePlugins";
        private const string WebViewDiskCacheSizeKey = "WebSettings_DiskCacheSize";
        private const string WebViewFastStartupModeKey = "WebSettings_FastStartupMode";
        private const string WebViewSingleProcessModeKey = "WebSettings_SingleProcessMode";
        
        // GPU 优化设置
        private const string WebViewEnableHardwareAccelerationKey = "WebSettings_EnableHardwareAcceleration";
        private const string WebViewEnableHardwareOverlaysKey = "WebSettings_EnableHardwareOverlays";
        private const string WebViewEnableHardwareVideoDecoderKey = "WebSettings_EnableHardwareVideoDecoder";
        private const string WebViewDisableSoftwareRasterizerKey = "WebSettings_DisableSoftwareRasterizer";
        
        // 链接打开方式设置
        private const string LinkOpenBehaviorKey = "WebSettings_LinkOpenBehavior";
        
        private static readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        /// <summary>
        /// 获取或设置是否启用 WinUI 右键菜单
        /// </summary>
        public static bool EnableWinUIContextMenu
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, EnableWinUIContextMenuKey, defaultValue: false);
            set => AotSafeSettingsHelper.SetBool(_localSettings, EnableWinUIContextMenuKey, value);
        }

        /// <summary>
        /// 获取或设置同时打开的 WebView 最大数量（范围：1-20）
        /// </summary>
        public static int MaxWebViewCount
        {
            get => Math.Clamp(
                AotSafeSettingsHelper.GetInt(_localSettings, MaxWebViewCountKey, defaultValue: 2),
                1, 20
            );
            set => AotSafeSettingsHelper.SetInt(
                _localSettings,
                MaxWebViewCountKey,
                Math.Clamp(value, 1, 20)
            );
        }

        /// <summary>
        /// 获取或设置 Frame 导航动画类型
        /// </summary>
        public static FrameAnimationType FrameNavigationAnimation
        {
            get => AotSafeSettingsHelper.GetEnum(
                _localSettings,
                FrameNavigationAnimationKey,
                FrameAnimationType.EntranceTransition
            );
            set => AotSafeSettingsHelper.SetEnum(_localSettings, FrameNavigationAnimationKey, value);
        }

        /// <summary>
        /// 获取或设置子页面导航动画类型（如从设置到实验室）
        /// 默认使用从右侧滑入动画
        /// </summary>
        public static FrameAnimationType SubPageNavigationAnimation
        {
            get => AotSafeSettingsHelper.GetEnum(
                _localSettings,
                SubPageNavigationAnimationKey,
                FrameAnimationType.SlideFromRight
            );
            set => AotSafeSettingsHelper.SetEnum(_localSettings, SubPageNavigationAnimationKey, value);
        }

        /// <summary>
        /// 获取或设置是否启用 AI 实验室
        /// </summary>
        public static bool EnableAILab
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, EnableAILabKey, defaultValue: false);
            set => AotSafeSettingsHelper.SetBool(_localSettings, EnableAILabKey, value);
        }

        /// <summary>
        /// 获取或设置是否在侧边栏显示返回按钮
        /// </summary>
        public static bool EnableBackButton
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, EnableBackButtonKey, defaultValue: false);
            set => AotSafeSettingsHelper.SetBool(_localSettings, EnableBackButtonKey, value);
        }

        /// <summary>
        /// 获取或设置是否在顶栏显示返回按钮
        /// </summary>
        public static bool EnableTopBarBackButton
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, EnableTopBarBackButtonKey, defaultValue: false);
            set => AotSafeSettingsHelper.SetBool(_localSettings, EnableTopBarBackButtonKey, value);
        }

        /// <summary>
        /// 获取或设置是否在顶栏显示菜单按钮
        /// </summary>
        public static bool EnableTopBarMenuButton
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, EnableTopBarMenuButtonKey, defaultValue: false);
            set => AotSafeSettingsHelper.SetBool(_localSettings, EnableTopBarMenuButtonKey, value);
        }

        /// <summary>
        /// 获取或设置窗口停靠位置（左侧或右侧）
        /// </summary>
        public static WindowDockSide DockSide
        {
            get => AotSafeSettingsHelper.GetEnum(
                _localSettings,
                WindowDockSideKey,
                WindowDockSide.Right
            );
            set => AotSafeSettingsHelper.SetEnum(_localSettings, WindowDockSideKey, value);
        }

        /// <summary>
        /// 获取或设置左侧停靠时是否将导航栏也放在左侧
        /// </summary>
        public static bool PlaceNavigationBarOnLeftWhenDockedLeft
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, PlaceNavigationBarOnLeftWhenDockedLeftKey, defaultValue: false);
            set => AotSafeSettingsHelper.SetBool(_localSettings, PlaceNavigationBarOnLeftWhenDockedLeftKey, value);
        }

        /// <summary>
        /// 获取或设置托盘"关闭窗口"按钮的行为
        /// </summary>
        public static TrayCloseWindowBehavior CloseWindowBehavior
        {
            get => AotSafeSettingsHelper.GetEnum(
                _localSettings,
                TrayCloseWindowBehaviorKey,
                TrayCloseWindowBehavior.DestroyWindow
            );
            set => AotSafeSettingsHelper.SetEnum(_localSettings, TrayCloseWindowBehaviorKey, value);
        }

        /// <summary>
        /// 获取或设置是否隐藏托盘菜单中的评价按钮
        /// </summary>
        public static bool HideTrayRateButton
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, HideTrayRateButtonKey, defaultValue: false);
            set => AotSafeSettingsHelper.SetBool(_localSettings, HideTrayRateButtonKey, value);
        }

        /// <summary>
        /// 获取或设置是否隐藏网页浏览器的关闭按钮
        /// </summary>
        public static bool HideWebViewCloseButton
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, HideWebViewCloseButtonKey, defaultValue: false);
            set => AotSafeSettingsHelper.SetBool(_localSettings, HideWebViewCloseButtonKey, value);
        }

        /// <summary>
        /// 获取或设置内容区背景材质类型
        /// </summary>
        public static ContentAreaBackdropType ContentAreaBackdrop
        {
            get => AotSafeSettingsHelper.GetEnum(
                _localSettings,
                ContentAreaBackdropTypeKey,
                ContentAreaBackdropType.SolidColor
            );
            set => AotSafeSettingsHelper.SetEnum(_localSettings, ContentAreaBackdropTypeKey, value);
        }

        /// <summary>
        /// 获取或设置 WebView2 内存模式
        /// </summary>
        public static WebViewMemoryMode MemoryMode
        {
            get => AotSafeSettingsHelper.GetEnum(
                _localSettings,
                WebViewMemoryModeKey,
                WebViewMemoryMode.Normal
            );
            set => AotSafeSettingsHelper.SetEnum(_localSettings, WebViewMemoryModeKey, value);
        }

        /// <summary>
        /// 获取或设置是否自动清理缓存
        /// </summary>
        public static bool AutoClearCache
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, WebViewAutoClearCacheKey, defaultValue: false);
            set => AotSafeSettingsHelper.SetBool(_localSettings, WebViewAutoClearCacheKey, value);
        }

        /// <summary>
        /// 获取或设置是否暂停不活跃的 WebView
        /// </summary>
        public static bool SuspendInactiveWebView
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, WebViewSuspendInactiveKey, defaultValue: false);
            set => AotSafeSettingsHelper.SetBool(_localSettings, WebViewSuspendInactiveKey, value);
        }

        /// <summary>
        /// 获取或设置是否禁用后台网络
        /// </summary>
        public static bool DisableBackgroundNetwork
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, WebViewDisableBackgroundNetworkKey, defaultValue: false);
            set => AotSafeSettingsHelper.SetBool(_localSettings, WebViewDisableBackgroundNetworkKey, value);
        }

        /// <summary>
        /// 获取或设置是否禁用扩展
        /// </summary>
        public static bool DisableExtensions
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, WebViewDisableExtensionsKey, defaultValue: true);
            set => AotSafeSettingsHelper.SetBool(_localSettings, WebViewDisableExtensionsKey, value);
        }

        /// <summary>
        /// 获取或设置是否禁用插件
        /// </summary>
        public static bool DisablePlugins
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, WebViewDisablePluginsKey, defaultValue: true);
            set => AotSafeSettingsHelper.SetBool(_localSettings, WebViewDisablePluginsKey, value);
        }

        /// <summary>
        /// 获取或设置磁盘缓存大小（MB，范围：10-500）
        /// </summary>
        public static int DiskCacheSize
        {
            get => Math.Clamp(
                AotSafeSettingsHelper.GetInt(_localSettings, WebViewDiskCacheSizeKey, defaultValue: 100),
                10, 500
            );
            set => AotSafeSettingsHelper.SetInt(
                _localSettings,
                WebViewDiskCacheSizeKey,
                Math.Clamp(value, 10, 500)
            );
        }

        /// <summary>
        /// 获取或设置是否启用快速启动模式（优化首次网页打开速度）
        /// </summary>
        public static bool FastStartupMode
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, WebViewFastStartupModeKey, defaultValue: true);
            set => AotSafeSettingsHelper.SetBool(_localSettings, WebViewFastStartupModeKey, value);
        }

        /// <summary>
        /// 获取或设置是否启用单进程模式（减少辅助进程，降低内存占用）
        /// </summary>
        public static bool SingleProcessMode
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, WebViewSingleProcessModeKey, defaultValue: false);
            set => AotSafeSettingsHelper.SetBool(_localSettings, WebViewSingleProcessModeKey, value);
        }

        /// <summary>
        /// 获取或设置是否启用硬件加速
        /// </summary>
        public static bool EnableHardwareAcceleration
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, WebViewEnableHardwareAccelerationKey, defaultValue: true);
            set => AotSafeSettingsHelper.SetBool(_localSettings, WebViewEnableHardwareAccelerationKey, value);
        }

        /// <summary>
        /// 获取或设置是否启用硬件叠加层
        /// </summary>
        public static bool EnableHardwareOverlays
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, WebViewEnableHardwareOverlaysKey, defaultValue: true);
            set => AotSafeSettingsHelper.SetBool(_localSettings, WebViewEnableHardwareOverlaysKey, value);
        }

        /// <summary>
        /// 获取或设置是否启用硬件视频解码
        /// </summary>
        public static bool EnableHardwareVideoDecoder
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, WebViewEnableHardwareVideoDecoderKey, defaultValue: true);
            set => AotSafeSettingsHelper.SetBool(_localSettings, WebViewEnableHardwareVideoDecoderKey, value);
        }

        /// <summary>
        /// 获取或设置是否禁用软件光栅化
        /// </summary>
        public static bool DisableSoftwareRasterizer
        {
            get => AotSafeSettingsHelper.GetBool(_localSettings, WebViewDisableSoftwareRasterizerKey, defaultValue: true);
            set => AotSafeSettingsHelper.SetBool(_localSettings, WebViewDisableSoftwareRasterizerKey, value);
        }

        /// <summary>
        /// 获取或设置链接打开方式(target="_blank"等)
        /// </summary>
        public static LinkOpenBehavior LinkOpenBehavior
        {
            get => AotSafeSettingsHelper.GetEnum(
                _localSettings,
                LinkOpenBehaviorKey,
                LinkOpenBehavior.Ask
            );
            set => AotSafeSettingsHelper.SetEnum(_localSettings, LinkOpenBehaviorKey, value);
        }
    }

    /// <summary>
    /// 内容区背景材质类型
    /// </summary>
    public enum ContentAreaBackdropType
    {
        /// <summary>
        /// 纯色背景（默认）
        /// </summary>
        SolidColor = 0,

        /// <summary>
        /// 云母材质（Mica Base）- 融合桌面壁纸
        /// </summary>
        MicaBase = 1,

        /// <summary>
        /// 云母替代材质（Mica Alt）- 更深的层次感
        /// </summary>
        MicaAlt = 2,

        /// <summary>
        /// 桌面亚克力（Desktop Acrylic）- 半透明磨砂玻璃
        /// </summary>
        DesktopAcrylic = 3
    }

    /// <summary>
    /// WebView2 内存模式
    /// </summary>
    public enum WebViewMemoryMode
    {
        /// <summary>
        /// 正常模式（默认）
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 低内存模式（推荐后台标签页）
        /// </summary>
        Low = 1
    }

    /// <summary>
    /// 窗口停靠位置枚举
    /// </summary>
    public enum WindowDockSide
    {
        /// <summary>
        /// 停靠在屏幕左侧
        /// </summary>
        Left = 0,

        /// <summary>
        /// 停靠在屏幕右侧
        /// </summary>
        Right = 1
    }

    /// <summary>
    /// Frame 导航动画类型
    /// </summary>
    public enum FrameAnimationType
    {
        /// <summary>
        /// 无动画
        /// </summary>
        None = 0,

        /// <summary>
        /// 入场动画（默认）- 向上滑动+淡入效果，适用于导航堆栈顶部
        /// </summary>
        EntranceTransition = 1,

        /// <summary>
        /// 从右侧滑入 - 适用于同级页面水平导航
        /// </summary>
        SlideFromRight = 2,

        /// <summary>
        /// 从左侧滑入 - 适用于同级页面水平导航
        /// </summary>
        SlideFromLeft = 3,

        /// <summary>
        /// 从底部滑入 - 适用于模态或深层导航
        /// </summary>
        SlideFromBottom = 4,

        /// <summary>
        /// 钻取动画（向前导航）- 表示深入应用层级
        /// </summary>
        DrillIn = 5,

        /// <summary>
        /// 淡入淡出动画 - 平滑过渡效果
        /// </summary>
        FadeInOut = 6,

        /// <summary>
        /// 缩放动画 - 现代感的缩放过渡
        /// </summary>
        ScaleAnimation = 7
    }

    /// <summary>
    /// 托盘"关闭窗口"按钮的行为
    /// </summary>
    public enum TrayCloseWindowBehavior
    {
        /// <summary>
        /// 直接销毁窗口（释放内存，保留托盘）
        /// </summary>
        DestroyWindow = 0,

        /// <summary>
        /// 重启到仅托盘（完全重启应用，不显示窗口）
        /// </summary>
        RestartToTrayOnly = 1
    }

    /// <summary>
    /// 链接打开方式(target="_blank"等新窗口请求)
    /// </summary>
    public enum LinkOpenBehavior
    {
        /// <summary>
        /// 每次询问(默认)
        /// </summary>
        Ask = 0,

        /// <summary>
        /// 在系统默认浏览器打开
        /// </summary>
        SystemBrowser = 1,

        /// <summary>
        /// 在 WebView 窗口内打开
        /// </summary>
        WebViewWindow = 2
    }
}
