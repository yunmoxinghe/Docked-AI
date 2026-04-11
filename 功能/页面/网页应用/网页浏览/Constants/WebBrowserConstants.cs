namespace Docked_AI.Features.Pages.WebApp.Browser.Constants
{
    /// <summary>
    /// 网页浏览器常量配置
    /// </summary>
    public static class WebBrowserConstants
    {
        /// <summary>
        /// 消息类型
        /// </summary>
        public static class MessageTypes
        {
            public const string Tint = "docked_ai_tint";
            public const string ThemeColor = "docked_ai_theme_color";
        }

        /// <summary>
        /// WebView2 浏览器参数
        /// </summary>
        public static class BrowserArguments
        {
            public static readonly string[] OptimizedScrolling = new[]
            {
                "--enable-features=msEdgeFluentOverlayScrollbar",
                "--enable-smooth-scrolling",
                "--enable-gpu-rasterization",
                "--enable-zero-copy",
                "--disable-features=msExperimentalScrolling"
            };
        }

        /// <summary>
        /// 采样配置
        /// </summary>
        public static class Sampling
        {
            /// <summary>
            /// 截图采样的顶部行数
            /// </summary>
            public const int TopSampleRows = 10;

            /// <summary>
            /// 截图采样的底部行数
            /// </summary>
            public const int BottomSampleRows = 10;

            /// <summary>
            /// 采样区域的最小透明度阈值
            /// </summary>
            public const byte MinAlphaThreshold = 10;
        }
    }
}
