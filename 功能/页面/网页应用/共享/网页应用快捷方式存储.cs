using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Docked_AI.Features.Pages.WebApp.Shared
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(List<WebAppShortcutStore.StoredWebAppShortcut>))]
    internal partial class WebAppShortcutJsonContext : JsonSerializerContext
    {
    }

    public static class WebAppShortcutStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolver = WebAppShortcutJsonContext.Default
        };

        private static string StorageDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Docked AI");

        private static string StorageFilePath => Path.Combine(StorageDirectory, "web-shortcuts.json");

        public static async Task<IReadOnlyList<WebAppShortcut>> LoadAsync()
        {
            try
            {
                if (!File.Exists(StorageFilePath))
                {
                    return Array.Empty<WebAppShortcut>();
                }

                string json = await File.ReadAllTextAsync(StorageFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return Array.Empty<WebAppShortcut>();
                }

                List<StoredWebAppShortcut>? stored = JsonSerializer.Deserialize(json, WebAppShortcutJsonContext.Default.ListStoredWebAppShortcut);
                if (stored is null || stored.Count == 0)
                {
                    return Array.Empty<WebAppShortcut>();
                }

                return stored
                    .Where(s => !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Url))
                    .Select(s => new WebAppShortcut(s.Id!, s.Name ?? string.Empty, s.Url!, s.IconBytes))
                    .ToList();
            }
            catch
            {
                return Array.Empty<WebAppShortcut>();
            }
        }

        public static async Task SaveAsync(IEnumerable<WebAppShortcut> shortcuts)
        {
            Directory.CreateDirectory(StorageDirectory);

            var data = shortcuts
                .Select(s => new StoredWebAppShortcut
                {
                    Id = s.Id,
                    Name = s.Name,
                    Url = s.Url,
                    IconBytes = s.IconBytes
                })
                .ToList();

            string json = JsonSerializer.Serialize(data, WebAppShortcutJsonContext.Default.ListStoredWebAppShortcut);
            await File.WriteAllTextAsync(StorageFilePath, json);
        }

        internal sealed class StoredWebAppShortcut
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
            public string? Url { get; set; }
            public byte[]? IconBytes { get; set; }
        }
    }
}
