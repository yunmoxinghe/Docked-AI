using Docked_AI.Features.Pages.Home;
using Docked_AI.Features.Pages.New;
using Docked_AI.Features.Pages.AI;
using Docked_AI.Features.Pages.Settings;
using Docked_AI.Features.Pages.WebApp.Browser;
using Docked_AI.Features.Pages.WebApp.Shared;
using Docked_AI.Features.Localization;
using Docked_AI.Features.MainWindowContent.ContentArea;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

namespace Docked_AI.Features.MainWindowContent.NavigationBar
{
    public sealed partial class NavigationBar : UserControl
    {
        private readonly Dictionary<string, WebAppShortcut> _webShortcuts = new();
        private readonly Dictionary<string, NavigationViewItem> _webShortcutItems = new();
        private NavigationViewItemBase? _lastSelectedNavigationItem;
        private bool _suppressSelectionChanged;
        
        // 导航防抖器（使用 Stopwatch 实现线程安全）
        private readonly NavigationDebouncer _navigationDebouncer = new(300);

        public event EventHandler<NavigationRequest>? NavigationRequested;
        public event EventHandler? DockToggleRequested;
        public event EventHandler? WindowStateToggleRequested;
        public event EventHandler<string>? ShortcutRemoved; // 快捷方式被移除事件
        public event EventHandler<string>? WebAppRestartRequested; // 网页应用重启请求事件
        public event EventHandler? BackRequested; // 返回请求事件

        private bool _isNavigationBarOnLeft = false;

        public void UpdateDockToggleIcon(bool isPinned)
        {
            // 根据导航栏位置选择不同的图标
            if (_isNavigationBarOnLeft)
            {
                // 左侧模式：使用 Pin/Unpin 图标
                DockToggleIcon.Glyph = isPinned ? "\uEA5B" : "\uEA49";
            }
            else
            {
                // 右侧模式：使用原来的图标
                DockToggleIcon.Glyph = isPinned ? "\uE8A0" : "\uE89F";
            }
        }

        public void SetNavigationBarPlacement(bool isOnLeft)
        {
            _isNavigationBarOnLeft = isOnLeft;
            
            TopNavView.HorizontalAlignment = isOnLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            TopNavView.FlowDirection = isOnLeft ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            
            NavView.HorizontalAlignment = isOnLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            NavView.FlowDirection = isOnLeft ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        }

        public void SelectNewPageItem()
        {
            _suppressSelectionChanged = true;
            NavView.SelectedItem = CreateNavigationItem;
            TopNavView.SelectedItem = null;
            _suppressSelectionChanged = false;
        }

        public void SelectHomeItem()
        {
            _suppressSelectionChanged = true;
            NavView.SelectedItem = HomeNavigationItem;
            TopNavView.SelectedItem = null;
            _suppressSelectionChanged = false;
        }

        public void SelectWebAppItem(string shortcutId)
        {
            if (_webShortcutItems.TryGetValue(shortcutId, out NavigationViewItem? navItem))
            {
                _suppressSelectionChanged = true;
                NavView.SelectedItem = navItem;
                TopNavView.SelectedItem = null;
                _suppressSelectionChanged = false;
            }
        }

        public NavigationBar()
        {
            InitializeComponent();
            _lastSelectedNavigationItem = HomeNavigationItem;

            WebAppEventBus.ShortcutCreated += OnShortcutCreated;
            WebAppEventBus.ShortcutsRefreshRequested += OnShortcutsRefreshRequested;
            Unloaded += (_, _) =>
            {
                WebAppEventBus.ShortcutCreated -= OnShortcutCreated;
                WebAppEventBus.ShortcutsRefreshRequested -= OnShortcutsRefreshRequested;
            };
            Loaded += NavigationBar_Loaded;
            SizeChanged += NavigationBar_SizeChanged;
            
            // 添加双击空白区域触发固定按钮
            NavView.DoubleTapped += OnNavViewDoubleTapped;
            
            // 根据设置显示或隐藏 AI 导航项
            UpdateAINavigationItemVisibility();
            // 初始化返回按钮（默认隐藏）
            BackNavigationItem.Visibility = Visibility.Collapsed;
        }

        private void NavigationBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 强制刷新 NavView 布局以修复 FooterMenuItems 显示问题
            NavView.InvalidateMeasure();
            NavView.InvalidateArrange();
        }

        public void UpdateAINavigationItemVisibility()
        {
            AINavigationItem.Visibility = ExperimentalSettings.EnableAILab 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        public void UpdateBackButtonVisibility(bool canGoBack)
        {
            if (!ExperimentalSettings.EnableBackButton)
            {
                BackNavigationItem.Visibility = Visibility.Collapsed;
                return;
            }
            BackNavigationItem.Visibility = canGoBack ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnNavViewDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            // 检查是否双击了按钮或图标区域
            // 如果双击的是 NavigationViewItem 或其子元素，则不触发固定功能
            var originalSource = e.OriginalSource as DependencyObject;
            
            // 向上遍历可视树，检查是否点击了 NavigationViewItem
            while (originalSource != null)
            {
                if (originalSource is NavigationViewItem)
                {
                    // 双击了按钮区域，不触发固定功能
                    return;
                }
                
                if (originalSource == NavView)
                {
                    // 已经到达 NavView 根节点，说明是空白区域
                    break;
                }
                
                originalSource = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(originalSource);
            }
            
            // 双击侧边栏空白区域时触发固定按钮功能
            DockToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateWindowStateIcon(bool isMaximized)
        {
            // E73F: 还原窗口图标
            // E740: 最大化图标
            WindowStateIcon.Glyph = isMaximized ? "\uE73F" : "\uE740";
        }

        private async void NavigationBar_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= NavigationBar_Loaded;
            
            await RestorePersistedShortcutsAsync();
            
            // 延迟刷新布局以修复 FooterMenuItems 显示问题
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                NavView.InvalidateMeasure();
                NavView.InvalidateArrange();
                NavView.UpdateLayout();
            });
        }

        private void OnShortcutCreated(object? sender, WebAppShortcut shortcut)
        {
            AddOrUpdateShortcutNavigationItem(shortcut, selectItem: true);
            _ = PersistShortcutsAsync();
        }

        private async void OnShortcutsRefreshRequested(object? sender, EventArgs e)
        {
            // 重新加载所有快捷方式
            await RestorePersistedShortcutsAsync();
        }

        private async Task RestorePersistedShortcutsAsync()
        {
            IReadOnlyList<WebAppShortcut> shortcuts = await WebAppShortcutStore.LoadAsync();
            foreach (WebAppShortcut shortcut in shortcuts)
            {
                AddOrUpdateShortcutNavigationItem(shortcut, selectItem: false);
            }
        }

        private async Task PersistShortcutsAsync()
        {
            try
            {
                await WebAppShortcutStore.SaveAsync(_webShortcuts.Values);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to persist web shortcuts: {ex.Message}");
            }
        }

        private void AddOrUpdateShortcutNavigationItem(WebAppShortcut shortcut, bool selectItem)
        {
            _webShortcuts[shortcut.Id] = shortcut;

            if (_webShortcutItems.TryGetValue(shortcut.Id, out NavigationViewItem? existingItem))
            {
                existingItem.Content = shortcut.Name;
                existingItem.Icon = BuildShortcutIcon(shortcut);
                if (selectItem)
                {
                    NavView.SelectedItem = existingItem;
                }
                return;
            }

            var navItem = new NavigationViewItem
            {
                Content = shortcut.Name,
                Tag = "webapp:" + shortcut.Id,
                Icon = BuildShortcutIcon(shortcut)
            };

            var contextMenu = new MenuFlyout();
            var unpinItem = new MenuFlyoutItem
            {
                Text = LocalizationHelper.GetString("Nav_UnpinShortcut"),
                Tag = shortcut.Id,
                Icon = new FontIcon { Glyph = "\uE77A" }
            };
            unpinItem.Click += OnUnpinShortcutClick;
            contextMenu.Items.Add(unpinItem);
            navItem.ContextFlyout = contextMenu;

            int insertIndex = NavView.MenuItems.IndexOf(CreateNavigationItem);
            if (insertIndex < 0)
            {
                insertIndex = NavView.MenuItems.Count;
            }

            NavView.MenuItems.Insert(insertIndex, navItem);
            _webShortcutItems[shortcut.Id] = navItem;
            if (selectItem)
            {
                NavView.SelectedItem = navItem;
            }
        }

        private IconElement BuildShortcutIcon(WebAppShortcut shortcut)
        {
            string cacheDir = Path.Combine(
                Windows.Storage.ApplicationData.Current.LocalFolder.Path,
                "web-icons");
            Directory.CreateDirectory(cacheDir);
            string extension = DetectImageExtension(shortcut.IconBytes ?? Array.Empty<byte>());
            string iconPath = Path.Combine(cacheDir, $"{shortcut.Id}{extension}");

            // 尝试从 IconBytes 加载
            if (shortcut.IconBytes is { Length: > 0 })
            {
                try
                {
                    File.WriteAllBytes(iconPath, shortcut.IconBytes);
                    var icon = CreateImageIconWithFallback(new Uri(iconPath), shortcut.Id);
                    if (icon != null) return icon;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NavigationBar] 保存图标失败: {iconPath}, {ex.Message}");
                }
            }

            // 尝试从缓存加载
            if (File.Exists(iconPath))
            {
                try
                {
                    var icon = CreateImageIconWithFallback(new Uri(iconPath), shortcut.Id);
                    if (icon != null) return icon;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NavigationBar] 读取缓存图标失败: {iconPath}, {ex.Message}");
                }
            }

            // 尝试从网站 favicon 加载
            if (Uri.TryCreate(shortcut.Url, UriKind.Absolute, out Uri? websiteUri))
            {
                try
                {
                    Uri faviconUri = new Uri(websiteUri.GetLeftPart(UriPartial.Authority) + "/favicon.ico");
                    var icon = CreateImageIconWithFallback(faviconUri, shortcut.Id);
                    if (icon != null) return icon;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NavigationBar] 创建 Favicon URI 失败: {shortcut.Url}, {ex.Message}");
                }
            }

            // 所有方法都失败时，返回地球图标作为后备
            System.Diagnostics.Debug.WriteLine($"[NavigationBar] 所有图标加载方法失败，使用地球图标: {shortcut.Name}");
            return new FontIcon { Glyph = "\uE774" }; // Globe 地球图标
        }

        private ImageIcon? CreateImageIconWithFallback(Uri imageUri, string shortcutId)
        {
            try
            {
                var bitmapImage = new BitmapImage();
                var imageIcon = new ImageIcon { Source = bitmapImage };
                
                // 监听图片加载失败事件，失败时切换到地球图标
                bitmapImage.ImageFailed += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[NavigationBar] 图标加载失败: {imageUri}, 错误: {e.ErrorMessage}");
                    
                    // 在 UI 线程上切换到地球图标
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (_webShortcutItems.TryGetValue(shortcutId, out var navItem))
                        {
                            navItem.Icon = new FontIcon { Glyph = "\uE774" }; // Globe 地球图标
                            System.Diagnostics.Debug.WriteLine($"[NavigationBar] 已切换到地球图标: {shortcutId}");
                        }
                    });
                };
                
                // 开始加载图片
                bitmapImage.UriSource = imageUri;
                
                return imageIcon;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationBar] 创建 ImageIcon 失败: {imageUri}, {ex.Message}");
                return null;
            }
        }

        private static string DetectImageExtension(byte[] bytes)
        {
            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                return ".png";
            }

            if (bytes.Length >= 3 &&
                bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                return ".jpg";
            }

            if (bytes.Length >= 4 &&
                bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            {
                return ".gif";
            }

            if (bytes.Length >= 2 &&
                bytes[0] == 0x42 && bytes[1] == 0x4D)
            {
                return ".bmp";
            }

            if (bytes.Length >= 12 &&
                bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            {
                return ".webp";
            }

            if (bytes.Length >= 4 &&
                bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0x01 && bytes[3] == 0x00)
            {
                return ".ico";
            }

            return ".png";
        }

        // 顶部 NavigationView 的 SelectionChanged 处理
        private void TopNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (_suppressSelectionChanged)
            {
                return;
            }

            // 当 TopNavView 中的项被选中时，清除 NavView 的选中状态
            if (args.SelectedItemContainer != null && NavView.SelectedItem != null)
            {
                _suppressSelectionChanged = true;
                NavView.SelectedItem = null;
                _suppressSelectionChanged = false;
            }
        }

        // 顶部 NavigationView 的 ItemInvoked 处理
        private void TopNavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer?.Tag is not string tagText)
            {
                return;
            }

            if (tagText == "windowstate")
            {
                // ⭐ 窗口状态按钮不应该改变当前页面的选中状态
                // 触发事件即可，不需要操作选中项
                WindowStateToggleRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer?.Tag is not string tagText)
            {
                return;
            }

            // 处理停靠切换
            if (tagText == "dock")
            {
                // ⭐ 固定按钮不应该改变当前页面的选中状态
                // 触发固定事件即可，不需要恢复选中项
                DockToggleRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            // 处理返回按钮
            if (tagText == "back")
            {
                // ⭐ 返回按钮不应该改变当前页面的选中状态
                // 触发返回事件即可，选中状态由实际导航结果决定
                BackRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            // 处理设置页面
            if (tagText == "settings")
            {
                string navigationKey = "settings";
                
                if (_navigationDebouncer.ShouldDebounce(navigationKey))
                {
                    return;
                }
                
                NavigationRequested?.Invoke(this, new NavigationRequest(typeof(SettingsPage), null));
                return;
            }

            if (tagText.StartsWith("webapp:"))
            {
                string shortcutId = tagText["webapp:".Length..];
                if (_webShortcuts.TryGetValue(shortcutId, out WebAppShortcut? shortcut))
                {
                    // 检查是否点击的是当前已选中的项
                    bool isAlreadySelected = _lastSelectedNavigationItem == args.InvokedItemContainer;
                    
                    if (isAlreadySelected)
                    {
                        // 已选中，触发重启（不改变选中状态，不触发 SelectionChanged）
                        string navigationKey = $"restart:{shortcutId}";
                        
                        if (_navigationDebouncer.ShouldDebounce(navigationKey))
                        {
                            return;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[NavigationBar] 点击已选中的标签，触发重启: {shortcut.Name}");
                        WebAppRestartRequested?.Invoke(this, shortcutId);
                    }
                    else
                    {
                        // 切换到其他标签，设置选中状态并让 SelectionChanged 处理导航
                        NavView.SelectedItem = args.InvokedItemContainer;
                    }
                }
                return;
            }

            if (!int.TryParse(tagText, out int sectionIndex))
            {
                return;
            }

            NavView.SelectedItem = args.InvokedItemContainer;

            Type pageType = sectionIndex switch
            {
                0 => typeof(HomePage),
                1 => typeof(NewPage),
                2 => typeof(AIPage),
                _ => typeof(HomePage)
            };

            // 防抖检查
            string navKey = $"invoke:{sectionIndex}";
            
            if (_navigationDebouncer.ShouldDebounce(navKey))
            {
                return;
            }

            NavigationRequested?.Invoke(this, new NavigationRequest(pageType, null));
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (_suppressSelectionChanged)
            {
                return;
            }

            if (args.SelectedItemContainer?.Tag is not string tagText)
            {
                return;
            }

            // 当 NavView 中的项被选中时，清除 TopNavView 的选中状态
            if (TopNavView.SelectedItem != null)
            {
                _suppressSelectionChanged = true;
                TopNavView.SelectedItem = null;
                _suppressSelectionChanged = false;
            }

            if (tagText == "settings")
            {
                _lastSelectedNavigationItem = args.SelectedItemContainer;
                return;
            }

            if (tagText.StartsWith("webapp:"))
            {
                string shortcutId = tagText["webapp:".Length..];
                if (_webShortcuts.TryGetValue(shortcutId, out WebAppShortcut? shortcut))
                {
                    // 防抖检查：避免快速点击创建多个标签页
                    string navigationKey = $"webapp:{shortcutId}";
                    
                    if (_navigationDebouncer.ShouldDebounce(navigationKey))
                    {
                        // 恢复之前的选中状态
                        _suppressSelectionChanged = true;
                        NavView.SelectedItem = _lastSelectedNavigationItem;
                        _suppressSelectionChanged = false;
                        return;
                    }
                    
                    _lastSelectedNavigationItem = args.SelectedItemContainer;
                    
                    // 只在切换标签时触发导航（ItemInvoked 已经处理了重启逻辑）
                    NavigationRequested?.Invoke(this, new NavigationRequest(typeof(WebBrowserPage), shortcut));
                }
                return;
            }

            if (!int.TryParse(tagText, out int sectionIndex))
            {
                return;
            }

            // 普通页面导航由 ItemInvoked 处理，SelectionChanged 只更新选中记录
            _lastSelectedNavigationItem = args.SelectedItemContainer;
        }

        private void SettingsNavigationItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(SettingsAnimatedIcon, "PointerOver");
        }

        private void SettingsNavigationItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(SettingsAnimatedIcon, "Normal");
        }

        private void BackNavigationItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(BackAnimatedIcon, "PointerOver");
        }

        private void BackNavigationItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(BackAnimatedIcon, "Normal");
        }

        private void OnUnpinShortcutClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string shortcutId)
            {
                RemoveShortcut(shortcutId);
            }
        }

        private void RemoveShortcut(string shortcutId)
        {
            if (!_webShortcuts.Remove(shortcutId))
            {
                return;
            }

                if (_webShortcutItems.TryGetValue(shortcutId, out NavigationViewItem? navItem))
                {
                    NavView.MenuItems.Remove(navItem);
                    _webShortcutItems.Remove(shortcutId);

                    if (NavView.SelectedItem is NavigationViewItem selectedItem && selectedItem == navItem)
                    {
                        NavView.SelectedItem = HomeNavigationItem;
                        NavigationRequested?.Invoke(this, new NavigationRequest(typeof(HomePage), null));
                    }
                }

            // 触发快捷方式移除事件，通知清除缓存
            ShortcutRemoved?.Invoke(this, shortcutId);

            // 取消链接 WebView 实例
            WebViewManager.Unlink(shortcutId);

            _ = PersistShortcutsAsync();
        }

    }

    public sealed class NavigationRequest
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type PageType { get; }
        public object? Parameter { get; }

        public NavigationRequest(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type pageType,
            object? parameter)
        {
            PageType = pageType;
            Parameter = parameter;
        }
    }
}
