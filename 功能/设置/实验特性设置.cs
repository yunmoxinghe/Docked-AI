using Windows.Storage;

namespace Docked_AI.Features.Settings
{
    /// <summary>
    /// 实验特性设置管理
    /// </summary>
    public static class ExperimentalSettings
    {
        private const string EnableRoundedWebViewKey = "ExperimentalFeature_EnableRoundedWebView";
        private const string EnableWinUIContextMenuKey = "ExperimentalFeature_EnableWinUIContextMenu";
        
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
    }
}
