using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Docked_AI.Features.Pages.WebApp.Shared;
using SymbolIcon = Microsoft.UI.Xaml.Controls.SymbolIcon;
using Symbol = Microsoft.UI.Xaml.Controls.Symbol;
using Visibility = Microsoft.UI.Xaml.Visibility;
using HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using ImageIcon = Microsoft.UI.Xaml.Controls.ImageIcon;
using SettingsCard = CommunityToolkit.WinUI.Controls.SettingsCard;

namespace Docked_AI.Features.Pages.Home
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadWebAppsAsync();
            WebAppEventBus.ShortcutCreated += OnShortcutCreated;
        }

        private void OnShortcutCreated(object? sender, WebAppShortcut shortcut)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                await LoadWebAppsAsync();
            });
        }

        private async Task LoadWebAppsAsync()
        {
            var shortcuts = await WebAppShortcutStore.LoadAsync();
            WebAppsList.Children.Clear();

            if (shortcuts.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
                return;
            }

            EmptyText.Visibility = Visibility.Collapsed;

            foreach (var shortcut in shortcuts)
            {
                var card = new SettingsCard
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Header = shortcut.Name,
                    Description = shortcut.Url,
                    IsClickEnabled = true
                };

                if (shortcut.IconBytes != null && shortcut.IconBytes.Length > 0)
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        using var stream = new InMemoryRandomAccessStream();
                        await stream.WriteAsync(shortcut.IconBytes.AsBuffer());
                        stream.Seek(0);
                        await bitmap.SetSourceAsync(stream);
                        card.HeaderIcon = new ImageIcon { Source = bitmap };
                    }
                    catch
                    {
                        card.HeaderIcon = new SymbolIcon(Symbol.Globe);
                    }
                }
                else
                {
                    card.HeaderIcon = new SymbolIcon(Symbol.Globe);
                }

                WebAppsList.Children.Add(card);
            }
        }
    }
}
