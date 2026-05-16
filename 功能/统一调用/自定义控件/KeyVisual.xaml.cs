// 自定义 KeyVisual 控件 - 替代 DevWinUI.KeyVisual
// 用于显示键盘按键的可视化表示（如快捷键显示）
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.UnifiedCalls.CustomControls
{
    /// <summary>
    /// 键盘按键可视化控件
    /// 显示单个按键（如 Ctrl、Alt、Space 等）的视觉表示
    /// </summary>
    public sealed partial class KeyVisual : UserControl
    {
        /// <summary>
        /// Content 依赖属性 - 按键文本内容
        /// </summary>
        public new static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(
                nameof(Content),
                typeof(string),
                typeof(KeyVisual),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// 按键文本内容（如 "Ctrl", "Alt", "Space" 等）
        /// </summary>
        public new string Content
        {
            get => (string)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public KeyVisual()
        {
            this.InitializeComponent();
        }
    }
}
