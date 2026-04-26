using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.UnifiedCalls.TopAppBar;

/// <summary>
/// 智能标题：一行代码完成顶栏标题 + 大标题注册 + 滚动显隐联动
/// 用法：在 OnNavigatedTo 中调用 智能标题.Setup(...)，在 OnNavigatedFrom 中调用 智能标题.Cleanup()
/// </summary>
public sealed class 智能标题
{
    private ScrollViewer? _scrollViewer;
    private bool _titleVisible = true;

    /// <summary>
    /// 初始化智能标题，绑定滚动视图和大标题元素，顶栏标题自动从大标题读取
    /// </summary>
    /// <param name="scrollViewer">页面的 ScrollViewer</param>
    /// <param name="pageTitleElement">页面大标题 TextBlock（滚动时淡出，顶栏标题从其 Text 读取）</param>
    public void Setup(ScrollViewer scrollViewer, TextBlock pageTitleElement)
    {
        _scrollViewer = scrollViewer;
        _titleVisible = true;

        // 顶栏小标题直接读大标题的文字，保证两者永远一致
        // 用 Loaded 事件保险，确保本地化文字已绑定后再读取
        void ApplyCenterTitle()
        {
            var text = pageTitleElement.Text;
            if (!string.IsNullOrEmpty(text))
            {
                TopAppBarService.SetCenterContent(new TextBlock
                {
                    Text = text,
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }
        }

        if (!string.IsNullOrEmpty(pageTitleElement.Text))
        {
            ApplyCenterTitle();
        }
        else
        {
            pageTitleElement.Loaded += (_, _) => ApplyCenterTitle();
        }

        // 注册大标题，由服务统一控制淡入淡出
        TopAppBarService.SetPageTitle(pageTitleElement);

        // 订阅滚动事件
        _scrollViewer.ViewChanged += OnScrollViewerViewChanged;
    }

    /// <summary>
    /// 页面离开时清理，取消订阅并重置顶栏状态
    /// </summary>
    public void Cleanup()
    {
        if (_scrollViewer is not null)
        {
            _scrollViewer.ViewChanged -= OnScrollViewerViewChanged;
            _scrollViewer = null;
        }

        TopAppBarService.IsVisible = false;
        TopAppBarService.SetPageTitle(null);
        TopAppBarService.ClearAll();
        _titleVisible = true;
    }

    private void OnScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        bool scrolled = sv.VerticalOffset > 0;

        if (scrolled != TopAppBarService.IsVisible)
            TopAppBarService.IsVisible = scrolled;

        if (scrolled == _titleVisible)
        {
            _titleVisible = !scrolled;
            TopAppBarService.SetPageTitleVisible(!scrolled);
        }
    }
}
