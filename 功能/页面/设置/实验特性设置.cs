using System;
using Windows.Storage;

namespace Docked_AI.Features.Pages.Settings
{
    /// <summary>
    /// 实验特性设置管理
    /// </summary>
    public static class ExperimentalSettings
    {
        private const string EnableRoundedWebViewKey = "ExperimentalFeature_EnableRoundedWebView";
        private const string EnableWinUIContextMenuKey = "ExperimentalFeature_EnableWinUIContextMenu";
        private const string MaxWebViewCountKey = "WebSettings_MaxWebViewCount";
        private const string FrameNavigationAnimationKey = "NavigationSettings_FrameAnimation";
        private const string EnableAILabKey = "ExperimentalFeature_EnableAILab";
        private const string EnableBackButtonKey = "NavigationSettings_EnableBackButton";
        private const string EnableTopBarBackButtonKey = "TopBarSettings_EnableBackButton";
        private const string EnableTopBarMenuButtonKey = "TopBarSettings_EnableMenuButton";
        
        private static readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        /// <summary>
        /// 获取或设置是否启用 WebView2 圆角特性
        /// </summary>
        public static bool EnableRoundedWebView
        {
            get
            {
                if (_localSettings.Values.TryGetValue(EnableRoundedWebViewKey, out object? value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false; // 默认关闭
            }
            set
            {
                _localSettings.Values[EnableRoundedWebViewKey] = value;
            }
        }

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
                    if (value is int intValue && Enum.IsDefined(typeof(FrameAnimationType), intValue))
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
        /// 入场动画（默认）
        /// </summary>
        EntranceTransition = 1,

        /// <summary>
        /// 从右侧滑入
        /// </summary>
        SlideFromRight = 2,

        /// <summary>
        /// 从左侧滑入
        /// </summary>
        SlideFromLeft = 3,

        /// <summary>
        /// 从底部滑入
        /// </summary>
        SlideFromBottom = 4,

        /// <summary>
        /// 钻取动画（向前导航）
        /// </summary>
        DrillIn = 5
    }
}
