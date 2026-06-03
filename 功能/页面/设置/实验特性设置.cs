using System;
using Windows.Storage;
using Docked_AI.Features.Shared.AotOptimization;

namespace Docked_AI.Features.Pages.Settings
{
    /// <summary>
    /// 实验特性设置管理
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
        
        private static readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        /// <summary>
        /// 获取或设置是否启用 WinUI 右键菜单
        /// </summary>
        public static bool EnableWinUIContextMenu
        {
            get
            {
                if (_localSettings.Values.TryGetValue(EnableWinUIContextMenuKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认关闭
            }
            set
            {
                _localSettings.Values[EnableWinUIContextMenuKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置同时打开的 WebView 最大数量
        /// </summary>
        public static int MaxWebViewCount
        {
            get
            {
                if (_localSettings.Values.TryGetValue(MaxWebViewCountKey, out object? value))
                {
                    return value is int intValue ? intValue : 2;
                }
                return 2; // 默认值为 2
            }
            set
            {
                // 限制范围在 1-20 之间
                int clampedValue = Math.Max(1, Math.Min(20, value));
                _localSettings.Values[MaxWebViewCountKey] = clampedValue;
            }
        }

        /// <summary>
        /// 获取或设置 Frame 导航动画类型
        /// </summary>
        public static FrameAnimationType FrameNavigationAnimation
        {
            get
            {
                if (_localSettings.Values.TryGetValue(FrameNavigationAnimationKey, out object? value))
                {
                    if (value is int intValue && EnumValidationExtensions.IsValidFrameAnimationType(intValue))
                    {
                        return (FrameAnimationType)intValue;
                    }
                }
                return FrameAnimationType.EntranceTransition; // 默认使用 EntranceTransition
            }
            set
            {
                _localSettings.Values[FrameNavigationAnimationKey] = (int)value;
            }
        }

        /// <summary>
        /// 获取或设置子页面导航动画类型（如从设置到实验室）
        /// 默认使用从右侧滑入动画
        /// </summary>
        public static FrameAnimationType SubPageNavigationAnimation
        {
            get
            {
                if (_localSettings.Values.TryGetValue(SubPageNavigationAnimationKey, out object? value))
                {
                    if (value is int intValue && EnumValidationExtensions.IsValidFrameAnimationType(intValue))
                    {
                        return (FrameAnimationType)intValue;
                    }
                }
                return FrameAnimationType.SlideFromRight; // 默认使用 SlideFromRight
            }
            set
            {
                _localSettings.Values[SubPageNavigationAnimationKey] = (int)value;
            }
        }

        /// <summary>
        /// 获取或设置是否启用 AI 实验室
        /// </summary>
        public static bool EnableAILab
        {
            get
            {
                if (_localSettings.Values.TryGetValue(EnableAILabKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认关闭
            }
            set
            {
                _localSettings.Values[EnableAILabKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否在侧边栏显示返回按钮
        /// </summary>
        public static bool EnableBackButton
        {
            get
            {
                if (_localSettings.Values.TryGetValue(EnableBackButtonKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认关闭
            }
            set
            {
                _localSettings.Values[EnableBackButtonKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否在顶栏显示返回按钮
        /// </summary>
        public static bool EnableTopBarBackButton
        {
            get
            {
                if (_localSettings.Values.TryGetValue(EnableTopBarBackButtonKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认关闭
            }
            set
            {
                _localSettings.Values[EnableTopBarBackButtonKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否在顶栏显示菜单按钮
        /// </summary>
        public static bool EnableTopBarMenuButton
        {
            get
            {
                if (_localSettings.Values.TryGetValue(EnableTopBarMenuButtonKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认关闭
            }
            set
            {
                _localSettings.Values[EnableTopBarMenuButtonKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置窗口停靠位置（左侧或右侧）
        /// </summary>
        public static WindowDockSide DockSide
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WindowDockSideKey, out object? value))
                {
                    if (value is int intValue && EnumValidationExtensions.IsValidWindowDockSide(intValue))
                    {
                        return (WindowDockSide)intValue;
                    }
                }
                return WindowDockSide.Right; // 默认右侧
            }
            set
            {
                _localSettings.Values[WindowDockSideKey] = (int)value;
            }
        }

        /// <summary>
        /// 获取或设置左侧停靠时是否将导航栏也放在左侧
        /// </summary>
        public static bool PlaceNavigationBarOnLeftWhenDockedLeft
        {
            get
            {
                if (_localSettings.Values.TryGetValue(PlaceNavigationBarOnLeftWhenDockedLeftKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认保持导航栏在右侧
            }
            set
            {
                _localSettings.Values[PlaceNavigationBarOnLeftWhenDockedLeftKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置托盘"关闭窗口"按钮的行为
        /// </summary>
        public static TrayCloseWindowBehavior CloseWindowBehavior
        {
            get
            {
                if (_localSettings.Values.TryGetValue(TrayCloseWindowBehaviorKey, out object? value))
                {
                    if (value is int intValue && EnumValidationExtensions.IsValidTrayCloseWindowBehavior(intValue))
                    {
                        return (TrayCloseWindowBehavior)intValue;
                    }
                }
                return TrayCloseWindowBehavior.DestroyWindow; // 默认直接销毁窗口
            }
            set
            {
                _localSettings.Values[TrayCloseWindowBehaviorKey] = (int)value;
            }
        }

        /// <summary>
        /// 获取或设置是否隐藏托盘菜单中的评价按钮
        /// </summary>
        public static bool HideTrayRateButton
        {
            get
            {
                if (_localSettings.Values.TryGetValue(HideTrayRateButtonKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认显示评价按钮
            }
            set
            {
                _localSettings.Values[HideTrayRateButtonKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否隐藏网页浏览器的关闭按钮
        /// </summary>
        public static bool HideWebViewCloseButton
        {
            get
            {
                if (_localSettings.Values.TryGetValue(HideWebViewCloseButtonKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认显示关闭按钮
            }
            set
            {
                _localSettings.Values[HideWebViewCloseButtonKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置内容区背景材质类型
        /// </summary>
        public static ContentAreaBackdropType ContentAreaBackdrop
        {
            get
            {
                if (_localSettings.Values.TryGetValue(ContentAreaBackdropTypeKey, out object? value))
                {
                    if (value is int intValue && EnumValidationExtensions.IsValidContentAreaBackdropType(intValue))
                    {
                        return (ContentAreaBackdropType)intValue;
                    }
                }
                return ContentAreaBackdropType.SolidColor; // 默认纯色背景
            }
            set
            {
                _localSettings.Values[ContentAreaBackdropTypeKey] = (int)value;
            }
        }

        /// <summary>
        /// 获取或设置 WebView2 内存模式
        /// </summary>
        public static WebViewMemoryMode MemoryMode
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewMemoryModeKey, out object? value))
                {
                    if (value is int intValue && EnumValidationExtensions.IsValidWebViewMemoryMode(intValue))
                    {
                        return (WebViewMemoryMode)intValue;
                    }
                }
                return WebViewMemoryMode.Normal; // 默认正常模式
            }
            set
            {
                _localSettings.Values[WebViewMemoryModeKey] = (int)value;
            }
        }

        /// <summary>
        /// 获取或设置是否自动清理缓存
        /// </summary>
        public static bool AutoClearCache
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewAutoClearCacheKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认关闭
            }
            set
            {
                _localSettings.Values[WebViewAutoClearCacheKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否暂停不活跃的 WebView
        /// </summary>
        public static bool SuspendInactiveWebView
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewSuspendInactiveKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认关闭
            }
            set
            {
                _localSettings.Values[WebViewSuspendInactiveKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否禁用后台网络
        /// </summary>
        public static bool DisableBackgroundNetwork
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewDisableBackgroundNetworkKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认关闭
            }
            set
            {
                _localSettings.Values[WebViewDisableBackgroundNetworkKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否禁用扩展
        /// </summary>
        public static bool DisableExtensions
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewDisableExtensionsKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return true; // 默认禁用扩展
            }
            set
            {
                _localSettings.Values[WebViewDisableExtensionsKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否禁用插件
        /// </summary>
        public static bool DisablePlugins
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewDisablePluginsKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return true; // 默认禁用插件
            }
            set
            {
                _localSettings.Values[WebViewDisablePluginsKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置磁盘缓存大小（MB）
        /// </summary>
        public static int DiskCacheSize
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewDiskCacheSizeKey, out object? value))
                {
                    return value is int intValue ? intValue : 100;
                }
                return 100; // 默认 100MB
            }
            set
            {
                // 限制范围在 10-500 MB 之间
                int clampedValue = Math.Max(10, Math.Min(500, value));
                _localSettings.Values[WebViewDiskCacheSizeKey] = clampedValue;
            }
        }

        /// <summary>
        /// 获取或设置是否启用快速启动模式（优化首次网页打开速度）
        /// </summary>
        public static bool FastStartupMode
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewFastStartupModeKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return true; // 默认开启
            }
            set
            {
                _localSettings.Values[WebViewFastStartupModeKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否启用单进程模式（减少辅助进程，降低内存占用）
        /// </summary>
        public static bool SingleProcessMode
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewSingleProcessModeKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认关闭（多进程更稳定）
            }
            set
            {
                _localSettings.Values[WebViewSingleProcessModeKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否启用硬件加速
        /// </summary>
        public static bool EnableHardwareAcceleration
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewEnableHardwareAccelerationKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return true; // 默认开启
            }
            set
            {
                _localSettings.Values[WebViewEnableHardwareAccelerationKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否启用硬件叠加层
        /// </summary>
        public static bool EnableHardwareOverlays
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewEnableHardwareOverlaysKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return true; // 默认开启
            }
            set
            {
                _localSettings.Values[WebViewEnableHardwareOverlaysKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否启用硬件视频解码
        /// </summary>
        public static bool EnableHardwareVideoDecoder
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewEnableHardwareVideoDecoderKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return true; // 默认开启
            }
            set
            {
                _localSettings.Values[WebViewEnableHardwareVideoDecoderKey] = value;
            }
        }

        /// <summary>
        /// 获取或设置是否禁用软件光栅化
        /// </summary>
        public static bool DisableSoftwareRasterizer
        {
            get
            {
                if (_localSettings.Values.TryGetValue(WebViewDisableSoftwareRasterizerKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return true; // 默认开启（禁用软件光栅化）
            }
            set
            {
                _localSettings.Values[WebViewDisableSoftwareRasterizerKey] = value;
            }
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
}
