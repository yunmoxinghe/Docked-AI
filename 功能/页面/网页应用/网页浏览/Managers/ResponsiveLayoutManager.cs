using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Docked_AI.Features.Pages.WebApp.Browser.Managers
{
    /// <summary>
    /// 响应式布局管理器
    /// 负责根据窗口大小调整UI元素的间距和布局
    /// </summary>
    public class ResponsiveLayoutManager
    {
        private const double MinButtonWidth = 40.0;
        private const double MaxButtonWidth = 68.0;
        private const double MinSpacing = 2.0;
        private const double MaxSpacing = 16.0;
        private const int ButtonCount = 5;

        /// <summary>
        /// 应用顶部栏的响应式间距
        /// </summary>
        public static void ApplyTopBarSpacing(Grid topBarGrid, StackPanel titleStackPanel, double actualWidth)
        {
            double horizontalPadding = Math.Max(8, Math.Min(16, actualWidth * 0.02));
            double verticalPadding = 8;
            double stackPanelMargin = Math.Max(8, Math.Min(16, actualWidth * 0.015));

            topBarGrid.Padding = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding);
            titleStackPanel.Margin = new Thickness(stackPanelMargin, 0, stackPanelMargin, 0);
        }

        /// <summary>
        /// 应用底部栏的响应式布局
        /// </summary>
        public static void ApplyBottomBarLayout(
            double availableWidth,
            StackPanel buttonPanel,
            params AppBarButton[] buttons)
        {
            if (availableWidth <= 0 || buttons.Length == 0)
            {
                return;
            }

            var (buttonWidth, spacing) = CalculateButtonLayout(availableWidth);

            // 应用按钮宽度
            foreach (var button in buttons)
            {
                button.Width = buttonWidth;
                button.MinWidth = MinButtonWidth;
            }

            // 设置按钮间距
            for (int i = 0; i < buttons.Length; i++)
            {
                if (i == buttons.Length - 1)
                {
                    buttons[i].Margin = new Thickness(0);
                }
                else
                {
                    buttons[i].Margin = new Thickness(0, 0, spacing, 0);
                }
            }

            // 设置面板边距
            buttonPanel.Padding = new Thickness(spacing, 0, spacing, 0);
        }

        /// <summary>
        /// 计算按钮宽度和间距
        /// </summary>
        private static (double buttonWidth, double spacing) CalculateButtonLayout(double availableWidth)
        {
            // 尝试使用最大按钮宽度
            double maxTotalButtonWidth = MaxButtonWidth * ButtonCount;
            double remainingWidth = availableWidth - maxTotalButtonWidth;
            double calculatedSpacing = remainingWidth / (ButtonCount + 1);

            if (calculatedSpacing >= MinSpacing)
            {
                // 空间充足
                return (MaxButtonWidth, Math.Min(MaxSpacing, calculatedSpacing));
            }

            // 空间不足，缩小按钮
            double totalSpacing = MinSpacing * (ButtonCount + 1);
            double widthForButtons = availableWidth - totalSpacing;
            double buttonWidth = widthForButtons / ButtonCount;

            if (buttonWidth >= MinButtonWidth)
            {
                return (buttonWidth, MinSpacing);
            }

            // 极端情况：使用最小按钮宽度
            double totalButtonWidth = MinButtonWidth * ButtonCount;
            double spacing = Math.Max(0, (availableWidth - totalButtonWidth) / (ButtonCount + 1));
            return (MinButtonWidth, spacing);
        }
    }
}
