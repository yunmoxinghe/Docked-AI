using System;
using Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace Docked_AI.Features.Localization
{
    /// <summary>
    /// 本地化辅助类，用于获取本地化字符串资源
    /// </summary>
public static class LocalizationHelper
    {
        private const string ResourceMapName = "Resources";
        private static ResourceLoader? _resourceLoader;
        private static string? _lastLanguage;

        public static string GetString(string key)
        {
            try
            {
                // 检查语言是否已更改，如果更改则重新创建 ResourceLoader
                var currentLanguage = ApplicationLanguages.PrimaryLanguageOverride;
                if (string.IsNullOrEmpty(currentLanguage))
                {
                    currentLanguage = ApplicationLanguages.Languages[0];
                }

                if (_resourceLoader == null || _lastLanguage != currentLanguage)
                {
                    _resourceLoader = new ResourceLoader("Resources");
                    _lastLanguage = currentLanguage;
                }

                var result = _resourceLoader.GetString(key);
                
                // 如果返回的是空字符串，说明没找到资源
                if (string.IsNullOrEmpty(result))
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalizationHelper] Resource not found for key: {key}");
                    return key;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalizationHelper] Error getting string for key '{key}': {ex.Message}");
                return key;
            }
        }

        /// <summary>
        /// 重置资源加载器，用于语言切换后强制重新加载
        /// </summary>
        public static void Reset()
        {
            _resourceLoader = null;
            _lastLanguage = null;
        }
    }
}
