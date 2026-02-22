using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

namespace Docked_AI.Features.MainWindow.Backdrop
{
    internal sealed class BackdropService
    {
        public void EnsureAcrylicBackdrop(Window window)
        {
            try
            {
                if (!IsAcrylicSupported())
                {
                    SetFallbackBackground(window);
                    return;
                }

                if (window.SystemBackdrop == null || window.SystemBackdrop is not DesktopAcrylicBackdrop)
                {
                    window.SystemBackdrop = new DesktopAcrylicBackdrop();
                    window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                    {
                        ValidateAcrylicEffect(window);
                    });
                }

                EnsureTransparentBackground(window);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set acrylic backdrop: {ex.Message}");
                SetFallbackBackground(window);
            }
        }

        private bool IsAcrylicSupported()
        {
            try
            {
                var version = Environment.OSVersion.Version;
                if (version.Major < 10 || (version.Major == 10 && version.Build < 18362))
                {
                    return false;
                }

                try
                {
                    _ = new DesktopAcrylicBackdrop();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to check acrylic support: {ex.Message}");
                return false;
            }
        }

        private void ValidateAcrylicEffect(Window window)
        {
            try
            {
                if (window.SystemBackdrop is not DesktopAcrylicBackdrop)
                {
                    SetFallbackBackground(window);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to validate acrylic effect: {ex.Message}");
                SetFallbackBackground(window);
            }
        }

        private void EnsureTransparentBackground(Window window)
        {
            if (window.Content is Grid rootGrid)
            {
                rootGrid.Background = new SolidColorBrush(Colors.Transparent);
            }
        }

        private void SetFallbackBackground(Window window)
        {
            try
            {
                try
                {
                    window.SystemBackdrop = new MicaBackdrop();
                    return;
                }
                catch (Exception micaEx)
                {
                    System.Diagnostics.Debug.WriteLine($"MicaBackdrop failed: {micaEx.Message}");
                }

                window.SystemBackdrop = null;
                if (window.Content is Grid rootGrid)
                {
                    var gradientBrush = new LinearGradientBrush
                    {
                        StartPoint = new Windows.Foundation.Point(0, 0),
                        EndPoint = new Windows.Foundation.Point(1, 1)
                    };
                    gradientBrush.GradientStops.Add(new GradientStop
                    {
                        Color = ColorHelper.FromArgb(180, 40, 40, 40),
                        Offset = 0
                    });
                    gradientBrush.GradientStops.Add(new GradientStop
                    {
                        Color = ColorHelper.FromArgb(160, 60, 60, 60),
                        Offset = 1
                    });

                    rootGrid.Background = gradientBrush;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fallback background failed: {ex.Message}");
            }
        }
    }
}
