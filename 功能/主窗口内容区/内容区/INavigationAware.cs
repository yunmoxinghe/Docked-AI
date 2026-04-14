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
}
