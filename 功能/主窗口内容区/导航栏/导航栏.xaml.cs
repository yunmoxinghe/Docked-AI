using Docked_AI.Features.Pages.Home;
using Docked_AI.Features.Pages.New;
using Docked_AI.Features.Pages.Settings;
using Docked_AI.Features.Pages.WebApp.Browser;
using Docked_AI.Features.Pages.WebApp.Shared;
using Docked_AI.Features.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
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

        public event EventHandler<NavigationRequest>? NavigationRequested;
        public event EventHandler? DockToggleRequested;
        public event EventHandler? WindowStateToggleRequested;

        public void UpdateDockToggleIcon(bool isPinned)
        {
            DockToggleIcon.Glyph = isPinned ? "\uE8A0" : "\uE89F";
        }

        public void SelectNewPageItem()
        {
            _suppressSelectionChanged = true;
            NavView.SelectedItem = CreateNavigationItem;
            _suppressSelectionChanged = false;
        }

        public void SelectHomeItem()
        {
            _suppressSelectionChanged = true;
            NavView.SelectedItem = HomeNavigationItem;
            _suppressSelectionChanged = false;
        }

        public void SelectWebAppItem(string shortcutId)
        {
            if (_webShortcutItems.TryGetValue(shortcutId, out NavigationViewItem? navItem))
            {
                _suppressSelectionChanged = true;
                NavView.SelectedItem = navItem;
                _suppressSelectionChanged = false;
            }
        }

        public NavigationBar()
        {
            InitializeComponent();
            _lastSelectedNavigationItem = HomeNavigationItem;

            WebAppEventBus.ShortcutCreated += OnShortcutCreated;
            Unloaded += (_, _) => WebAppEventBus.ShortcutCreated -= OnShortcutCreated;
            Loaded += NavigationBar_Loaded;
            
            // 添加双击空白区域触发固定按钮
            NavView.DoubleTapped += OnNavViewDoubleTapped;
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
        }

        private void OnShortcutCreated(object? sender, WebAppShortcut shortcut)
        {
            AddOrUpdateShortcutNavigationItem(shortcut, selectItem: true);
            _ = PersistShortcutsAsync();
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

        private static IconElement BuildShortcutIcon(WebAppShortcut shortcut)
        {
            string cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Docked AI",
                "web-icons");
            Directory.CreateDirectory(cacheDir);
            string extension = DetectImageExtension(shortcut.IconBytes ?? Array.Empty<byte>());
            string iconPath = Path.Combine(cacheDir, $"{shortcut.Id}{extension}");

            if (shortcut.IconBytes is { Length: > 0 })
            {
                try
                {
                    File.WriteAllBytes(iconPath, shortcut.IconBytes);
                    return new ImageIcon
                    {
                        Source = new BitmapImage(new Uri(iconPath))
                    };
                }
                catch
                {
                }
            }

            if (File.Exists(iconPath))
            {
                try
                {
                    return new ImageIcon
                    {
                        Source = new BitmapImage(new Uri(iconPath))
                    };
                }
                catch
                {
                }
            }

            if (Uri.TryCreate(shortcut.Url, UriKind.Absolute, out Uri? websiteUri))
            {
                try
                {
                    Uri faviconUri = new Uri(websiteUri.GetLeftPart(UriPartial.Authority) + "/favicon.ico");
                    return new ImageIcon
                    {
                        Source = new BitmapImage(faviconUri)
                    };
                }
                catch
                {
                }
            }

            return new FontIcon { Glyph = "\uE8A7" };
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

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer?.Tag is not string tagText)
            {
                return;
            }

            if (tagText == "dock")
            {
                DockToggleRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (tagText == "windowstate")
            {
                WindowStateToggleRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (tagText == "settings")
            {
                NavigationRequested?.Invoke(this, new NavigationRequest(typeof(SettingsPage), null));
                return;
            }

            if (tagText.StartsWith("webapp:"))
            {
                string shortcutId = tagText["webapp:".Length..];
                if (_webShortcuts.TryGetValue(shortcutId, out WebAppShortcut? shortcut))
                {
                    NavView.SelectedItem = args.InvokedItemContainer;
                    NavigationRequested?.Invoke(this, new NavigationRequest(typeof(WebBrowserPage), shortcut));
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
                1 => typeof(NewPage),
                _ => typeof(HomePage)
            };

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

            if (tagText == "settings")
            {
                _lastSelectedNavigationItem = args.SelectedItemContainer;
                return;
            }

            if (tagText.StartsWith("webapp:"))
            {
                _lastSelectedNavigationItem = args.SelectedItemContainer;
                string shortcutId = tagText["webapp:".Length..];
                if (_webShortcuts.TryGetValue(shortcutId, out WebAppShortcut? shortcut))
                {
                    NavigationRequested?.Invoke(this, new NavigationRequest(typeof(WebBrowserPage), shortcut));
                }
                return;
            }

            if (!int.TryParse(tagText, out int sectionIndex))
            {
                return;
            }

            _lastSelectedNavigationItem = args.SelectedItemContainer;

            Type pageType = sectionIndex switch
            {
                1 => typeof(NewPage),
                _ => typeof(HomePage)
            };

            NavigationRequested?.Invoke(this, new NavigationRequest(pageType, null));
        }

        private void SettingsNavigationItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(SettingsAnimatedIcon, "PointerOver");
        }

        private void SettingsNavigationItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(SettingsAnimatedIcon, "Normal");
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

            _ = PersistShortcutsAsync();
        }

    }

    public sealed class NavigationRequest
    {
        public Type PageType { get; }
        public object? Parameter { get; }

        public NavigationRequest(Type pageType, object? parameter)
        {
            PageType = pageType;
            Parameter = parameter;
        }
    }
}
