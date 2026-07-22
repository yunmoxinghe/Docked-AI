using System;

namespace DockedTools.Features.Localization
{
    /// <summary>
    /// 本地化辅助类，用于获取本地化字符串资源
    /// 使用 Windows App SDK 的 ResourceLoader API
    /// </summary>
    public static class LocalizationHelper
    {
        // ⭐ 使用 Windows App SDK 的 ResourceLoader（而不是 UWP 的）
        private static Microsoft.Windows.ApplicationModel.Resources.ResourceLoader? _resourceLoader;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取本地化字符串
        /// </summary>
        /// <param name="key">资源键名</param>
        /// <returns>本地化字符串，如果未找到则返回键名本身</returns>
        public static string GetString(string key)
        {
            try
            {
                // ⭐ 延迟初始化 ResourceLoader（线程安全）
                if (_resourceLoader == null)
                {
                    lock (_lock)
                    {
                        if (_resourceLoader == null)
                        {
                            // 使用默认资源文件路径（功能/本地化/Strings/Resources.resw）
                            _resourceLoader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
                        }
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
        /// 重置资源加载器（当需要强制重新加载时使用）
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _resourceLoader = null;
            }
        }
    }
}
