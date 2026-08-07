using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 进度条模块
    /// 包含加载进度条动画逻辑
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        private async Task HideLoadingProgressBarSmoothlyAsync()
        {
            if (LoadingProgressBar == null) return;

            await DispatcherQueue.EnqueueAsync(() =>
            {
                if (LoadingProgressBar == null) return;

                // 不停止 IsIndeterminate，让动画继续运行
                // 使用淡出动画隐藏，这样条纹会在淡出过程中继续滚动
                var fadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var storyboard = new Storyboard();
                storyboard.Children.Add(fadeOut);
                Storyboard.SetTarget(fadeOut, LoadingProgressBar);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");

                storyboard.Completed += (s, e) =>
                {
                    if (LoadingProgressBar != null)
                    {
                        LoadingProgressBar.Visibility = Visibility.Collapsed;
                        LoadingProgressBar.Opacity = 1.0; // 重置透明度
                        LoadingProgressBar.IsIndeterminate = false; // 停止动画以节省资源
                    }
                };

                storyboard.Begin();
            });
        }
    }
}
