using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Docked_AI.Features.Pages.WebApp.Shared
{
    public static class WebBrowserStateStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static string StorageDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Docked AI", "web-browser-states");

        private static string GetStateFilePath(string shortcutId) =>
            Path.Combine(StorageDirectory, $"{shortcutId}.json");

        public static async Task<WebBrowserState?> LoadAsync(string shortcutId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(shortcutId))
                {
                    return null;
                }

                string path = GetStateFilePath(shortcutId);
                if (!File.Exists(path))
                {
                    return null;
                }

                string json = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<WebBrowserState>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public static async Task SaveAsync(string shortcutId, WebBrowserState state)
        {
            if (string.IsNullOrWhiteSpace(shortcutId))
            {
                return;
            }

            Directory.CreateDirectory(StorageDirectory);

            string json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(GetStateFilePath(shortcutId), json);
        }
    }

    public sealed record WebBrowserState(
        string? LastUrl,
        string? LastTitle,
        double? ScrollX,
        double? ScrollY,
        DateTimeOffset UpdatedAt);
}

