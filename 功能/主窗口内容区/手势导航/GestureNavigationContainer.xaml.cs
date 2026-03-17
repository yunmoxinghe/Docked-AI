using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GestureNavigation
{
    /// <summary>
    /// 手势导航容器控件
    /// 将此控件包裹在你的 Frame 外层，订阅 NavigationRequested 事件执行导航
    /// </summary>
    public sealed partial class GestureNavigationContainer : UserControl
    {
        private NavigationInteractionTracker? _tracker;

        /// <summary>
        /// 手势触发导航事件
        /// </summary>
        public event EventHandler<OverscrollNavigationDirection>? NavigationRequested;

        /// <summary>
        /// 是否允许后退手势
        /// </summary>
        public bool CanGoBack
        {
            get => _tracker?.CanNavigateBackward ?? false;
            set { if (_tracker != null) _tracker.CanNavigateBackward = value; }
        }

        /// <summary>
        /// 是否允许前进手势
        /// </summary>
        public bool CanGoForward
        {
            get => _tracker?.CanNavigateForward ?? false;
            set { if (_tracker != null) _tracker.CanNavigateForward = value; }
        }

        /// <summary>
        /// 后退指示器图标（Segoe Fluent Icons glyph 字符串，可选）
        /// </summary>
        public string? BackIcon
        {
            get => _tracker?.BackPageIcon;
            set { if (_tracker != null) _tracker.BackPageIcon = value; }
        }

        /// <summary>
        /// 前进指示器图标（Segoe Fluent Icons glyph 字符串，可选）
        /// </summary>
        public string? ForwardIcon
        {
            get => _tracker?.ForwardPageIcon;
            set { if (_tracker != null) _tracker.ForwardPageIcon = value; }
        }

        public GestureNavigationContainer()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _tracker = new NavigationInteractionTracker(RootContainer, BackIcon_Border, ForwardIcon_Border);
            _tracker.NavigationRequested += (s, direction) => NavigationRequested?.Invoke(this, direction);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _tracker?.Dispose();
            _tracker = null;
        }
    }
}
