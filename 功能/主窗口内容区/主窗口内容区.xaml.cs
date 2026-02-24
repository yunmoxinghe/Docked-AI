using Docked_AI.Features.Pages.Home;
using Docked_AI.Features.Pages.New;
using Docked_AI.Features.Pages.Settings;
using Docked_AI.Features.Pages.WebApp.Browser;
using Docked_AI.Features.Pages.WebApp.Shared;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Docked_AI.Features.MainWindowContent
{
    public sealed partial class MainWindowContent : UserControl
    {
        private const float ContentCornerRadius = 4f;

        private readonly Dictionary<string, WebAppShortcut> _webShortcuts = new();
        private CompositionRoundedRectangleGeometry? _clipGeometry;

        public MainWindowContent()
        {
            InitializeComponent();
            ContentFrame.Navigate(typeof(HomePage));

            WebAppEventBus.ShortcutCreated += OnShortcutCreated;
            Unloaded += (_, _) => WebAppEventBus.ShortcutCreated -= OnShortcutCreated;
        }

        private void OnShortcutCreated(object? sender, WebAppShortcut shortcut)
        {
            _webShortcuts[shortcut.Id] = shortcut;

            var navItem = new NavigationViewItem
            {
                Content = shortcut.Name,
                Tag = "webapp:" + shortcut.Id
            };
            navItem.Icon = BuildShortcutIcon(shortcut);

            int insertIndex = RightNavigationView.MenuItems.IndexOf(CreateNavigationItem);
            if (insertIndex < 0)
            {
                insertIndex = RightNavigationView.MenuItems.Count;
            }

            RightNavigationView.MenuItems.Insert(insertIndex, navItem);
            RightNavigationView.SelectedItem = navItem;
        }

        private static IconElement BuildShortcutIcon(WebAppShortcut shortcut)
        {
            try
            {
                if (shortcut.IconBytes is { Length: > 0 })
                {
                    string cacheDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Docked AI",
                        "web-icons");
                    Directory.CreateDirectory(cacheDir);

                    string extension = DetectImageExtension(shortcut.IconBytes);
                    string iconPath = Path.Combine(cacheDir, $"{shortcut.Id}{extension}");
                    File.WriteAllBytes(iconPath, shortcut.IconBytes);

                    return new ImageIcon
                    {
                        Source = new BitmapImage(new Uri(iconPath))
                    };
                }

                if (Uri.TryCreate(shortcut.Url, UriKind.Absolute, out Uri? websiteUri))
                {
                    Uri faviconUri = new Uri(websiteUri.GetLeftPart(UriPartial.Authority) + "/favicon.ico");
                    return new ImageIcon
                    {
                        Source = new BitmapImage(faviconUri)
                    };
                }
            }
            catch
            {
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

        private void RightNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
                return;
            }

            if (args.SelectedItemContainer?.Tag is not string tagText)
            {
                return;
            }

            if (tagText.StartsWith("webapp:"))
            {
                string shortcutId = tagText["webapp:".Length..];
                if (_webShortcuts.TryGetValue(shortcutId, out WebAppShortcut? shortcut))
                {
                    ContentFrame.Navigate(typeof(WebBrowserPage), shortcut);
                }
                return;
            }

            if (!int.TryParse(tagText, out int sectionIndex))
            {
                return;
            }

            switch (sectionIndex)
            {
                case 0:
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
                case 1:
                    ContentFrame.Navigate(typeof(NewPage));
                    break;
                default:
                    ContentFrame.Navigate(typeof(HomePage));
                    break;
            }
        }

        private void ContentFrame_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
            {
                return;
            }

            var visual = ElementCompositionPreview.GetElementVisual(ContentFrame);
            if (_clipGeometry == null)
            {
                var compositor = visual.Compositor;
                _clipGeometry = compositor.CreateRoundedRectangleGeometry();
                _clipGeometry.CornerRadius = new Vector2(ContentCornerRadius, ContentCornerRadius);
                _clipGeometry.Offset = Vector2.Zero;
                visual.Clip = compositor.CreateGeometricClip(_clipGeometry);
            }

            _clipGeometry.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        }
    }
}
