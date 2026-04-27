namespace Docked_AI.Features.MainWindowContent.ContentArea
{
    /// <summary>
    /// 页面导航感知接口，用于支持缓存页面的导航事件
    /// </summary>
    public interface INavigationAware
    {
        /// <summary>
        /// 当页面被导航到时调用（包括从缓存恢复）
        /// </summary>
        void OnNavigatedTo(object? parameter);

        /// <summary>
        /// 当页面被导航离开时调用
        /// </summary>
        void OnNavigatedFrom();
    }

    /// <summary>
    /// 页面返回接管接口：实现此接口的页面可拦截顶栏返回按钮点击。
    /// 返回 true 表示页面已处理，框架不再执行 GoBack；返回 false 则框架正常返回。
    /// </summary>
    public interface IBackHandler
    {
        /// <summary>
        /// 当用户点击返回按钮时调用
        /// </summary>
        /// <returns>true 表示页面已处理返回逻辑，框架不再执行 GoBack；false 表示框架继续执行默认返回</returns>
        bool OnBackRequested();
    }
}
