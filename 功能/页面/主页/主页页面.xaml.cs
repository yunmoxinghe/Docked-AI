using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Docked_AI.Features.Pages.WebApp.Shared;
using Docked_AI.Features.Pages.WebApp.Browser;
using Docked_AI.Features.Localization;
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
        private const double MinResponsiveWidth = 320;
        private const double MaxResponsiveWidth = 760;
        private const double MinHorizontalMargin = 16;
        private const double MaxHorizontalMargin = 36;
        private double _lastAppliedMargin = -1;
        private double _lastMeasuredWidth = -1;

        public HomePage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateVisualState();
            await LoadWebAppsAsync();
            WebAppEventBus.ShortcutCreated += OnShortcutCreated;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Math.Abs(e.NewSize.Width - _lastMeasuredWidth) < 1)
            {
                return;
            }
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            double width = RootGrid?.ActualWidth ?? 0;
            if (width <= 0 && RootGrid != null)
            {
                width = RootGrid.ActualWidth;
            }
            if (width <= 0)
            {
                width = ActualWidth;
            }

            double normalized = (width - MinResponsiveWidth) / (MaxResponsiveWidth - MinResponsiveWidth);
            normalized = Math.Clamp(normalized, 0, 1);
            double horizontalMargin = Math.Round(MinHorizontalMargin + ((MaxHorizontalMargin - MinHorizontalMargin) * normalized));

            if (Math.Abs(horizontalMargin - _lastAppliedMargin) > 0.01)
            {
                PageContentPanel.Margin = new Thickness(horizontalMargin, 0, horizontalMargin, 0);
                _lastAppliedMargin = horizontalMargin;
            }
            _lastMeasuredWidth = width;
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

                card.Click += (sender, e) => OnCardClick(shortcut);

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

        private void OnCardClick(WebAppShortcut shortcut)
        {
            Frame.Navigate(typeof(WebBrowserPage), shortcut);
        }
    }
}
