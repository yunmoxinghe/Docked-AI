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
        private static ResourceLoader? _resourceLoader;
        private static string? _lastLanguage;

        /// <summary>
        /// 获取本地化字符串
        /// </summary>
        /// <param name="key">资源键</param>
        /// <returns>本地化后的字符串</returns>
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
                    // 尝试多种路径格式，找到能工作的那个
                    Exception? lastException = null;
                    string[] pathsToTry = new[]
                    {
                        "功能/本地化/Strings/Resources",
                        "Docked_AI/功能/本地化/Strings/Resources",
                        "Resources",
                        ""
                    };

                    foreach (var path in pathsToTry)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"[LocalizationHelper] Trying path: '{path}'");
                            _resourceLoader = string.IsNullOrEmpty(path) 
                                ? ResourceLoader.GetForViewIndependentUse()
                                : ResourceLoader.GetForViewIndependentUse(path);
                            
                            // 测试是否能加载一个已知的键
                            var testResult = _resourceLoader.GetString("TrayMenu_OpenWindow");
                            if (!string.IsNullOrEmpty(testResult) && testResult != "TrayMenu_OpenWindow")
                            {
                                System.Diagnostics.Debug.WriteLine($"[LocalizationHelper] ✓ Success with path: '{path}', test result: {testResult}");
                                _lastLanguage = currentLanguage;
                                break;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[LocalizationHelper] ✗ Path '{path}' didn't work, test returned: {testResult}");
                                _resourceLoader = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LocalizationHelper] ✗ Path '{path}' failed: {ex.Message}");
                            lastException = ex;
                            _resourceLoader = null;
                        }
                    }

                    if (_resourceLoader == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LocalizationHelper] ERROR: All paths failed! Last exception: {lastException?.Message}");
                        throw new InvalidOperationException("Failed to load ResourceLoader with any path", lastException);
                    }
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
