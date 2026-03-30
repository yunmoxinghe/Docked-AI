using Windows.ApplicationModel.Resources;

namespace Docked_AI.Features.Localization
{
    /// <summary>
    /// 本地化辅助类，用于获取本地化字符串资源
    /// </summary>
    public static class LocalizationHelper
    {
        private static readonly ResourceLoader _resourceLoader = new();

        /// <summary>
        /// 获取本地化字符串
        /// </summary>
        /// <param name="key">资源键</param>
        /// <returns>本地化后的字符串</returns>
        public static string GetString(string key)
        {
            try
            {
                return _resourceLoader.GetString(key);
            }
            catch
            {
                return key;
            }
        }
    }
}
