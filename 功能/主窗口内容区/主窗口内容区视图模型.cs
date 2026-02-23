using Docked_AI.Features.MainWindow.State;
using Microsoft.UI.Xaml;

namespace Docked_AI.Features.MainWindowContent
{
    public sealed class MainWindowContentViewModel : ObservableObject
    {
        private string _title = "主页";
        private string _pageBody = "在这里显示主页内容。";
        private string _topOutsideContent = string.Empty;
        private string _bottomOutsideContent = string.Empty;
        private Visibility _topOutsideVisibility = Visibility.Collapsed;
        private Visibility _bottomOutsideVisibility = Visibility.Collapsed;
        private double _cardTargetHeight = 260;

        private static readonly PageSection[] Sections =
        {
            new("主页", "在这里显示主页内容。", "今日摘要: 3 条待处理事项", string.Empty, 250),
            new("工作区", "在这里显示工作区内容。支持更长的内容展示和操作入口。", "项目: Docked AI", "提示: 底部可以放分页、状态提示或辅助按钮。", 330),
            new("设置", "在这里显示设置内容。", string.Empty, "部分设置页会在卡片外显示说明和危险操作提示。", 220)
        };

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string PageBody
        {
            get => _pageBody;
            set => SetProperty(ref _pageBody, value);
        }

        public string TopOutsideContent
        {
            get => _topOutsideContent;
            set => SetProperty(ref _topOutsideContent, value);
        }

        public string BottomOutsideContent
        {
            get => _bottomOutsideContent;
            set => SetProperty(ref _bottomOutsideContent, value);
        }

        public Visibility TopOutsideVisibility
        {
            get => _topOutsideVisibility;
            set => SetProperty(ref _topOutsideVisibility, value);
        }

        public Visibility BottomOutsideVisibility
        {
            get => _bottomOutsideVisibility;
            set => SetProperty(ref _bottomOutsideVisibility, value);
        }

        public double CardTargetHeight
        {
            get => _cardTargetHeight;
            set => SetProperty(ref _cardTargetHeight, value);
        }

        public MainWindowContentViewModel()
        {
            SelectSection(0);
        }

        public void SelectSection(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= Sections.Length)
            {
                sectionIndex = 0;
            }

            PageSection section = Sections[sectionIndex];
            Title = section.Title;
            PageBody = section.Body;
            TopOutsideContent = section.TopOutsideContent;
            BottomOutsideContent = section.BottomOutsideContent;
            TopOutsideVisibility = string.IsNullOrWhiteSpace(section.TopOutsideContent) ? Visibility.Collapsed : Visibility.Visible;
            BottomOutsideVisibility = string.IsNullOrWhiteSpace(section.BottomOutsideContent) ? Visibility.Collapsed : Visibility.Visible;
            CardTargetHeight = section.CardHeight;
        }

        private readonly record struct PageSection(
            string Title,
            string Body,
            string TopOutsideContent,
            string BottomOutsideContent,
            double CardHeight);
    }
}
