using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;

namespace Docked_AI.Features.AppEntry.ShareLaunch
{
    /// <summary>
    /// 处理从分享目标启动的逻辑
    /// </summary>
    public class ShareLaunchHandler
    {
        private readonly App _app;
        private string? _pendingSharedUrl;

        public ShareLaunchHandler(App app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        /// <summary>
        /// 处理分享目标激活
        /// </summary>
        public async Task HandleAsync(ShareTargetActivatedEventArgs? shareArgs, Window? window)
        {
            if (shareArgs == null)
            {
                return;
            }

            try
            {
                var shareOperation = shareArgs.ShareOperation;
                shareOperation.ReportStarted();

                string? sharedUrl = await ExtractSharedUrlAsync(shareOperation);

                shareOperation.ReportCompleted();

                if (!string.IsNullOrEmpty(sharedUrl))
                {
                    _pendingSharedUrl = sharedUrl;
                    await NavigateToSharedUrlAsync(window, sharedUrl);
                }
            }
            catch (Exception ex)
            {
                LogException("HandleShareTargetActivation", ex);
            }
        }

        private async Task<string?> ExtractSharedUrlAsync(ShareOperation shareOperation)
        {
            if (shareOperation.Data.Contains(StandardDataFormats.WebLink))
            {
                var webLink = await shareOperation.Data.GetWebLinkAsync();
                return webLink?.AbsoluteUri;
            }
            else if (shareOperation.Data.Contains(StandardDataFormats.Text))
            {
                var text = await shareOperation.Data.GetTextAsync();
                if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    return uri.AbsoluteUri;
                }
            }

            return null;
        }

        private async Task NavigateToSharedUrlAsync(Window? window, string sharedUrl)
        {
            if (window == null)
            {
                return;
            }

            window.Activate();

            // Wait for window to fully load before navigating
            await Task.Delay(500);

            System.Diagnostics.Debug.WriteLine($"ShareLaunchHandler: navigating with URL: {sharedUrl}");

            if (window is Docked_AI.MainWindow mainWindow)
            {
                mainWindow.NavigateToNewPage(sharedUrl);
                _pendingSharedUrl = null;
            }
        }

        private static void LogException(string source, Exception ex)
        {
            try
            {
                var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n";
                System.Diagnostics.Debug.WriteLine(text);
            }
            catch
            {
                // Suppress logging failures
            }
        }

        public string? PendingSharedUrl => _pendingSharedUrl;
    }
}
