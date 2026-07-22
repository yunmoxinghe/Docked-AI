using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Docked_AI.Features.Pages.WebApp.Shared
{
    /// <summary>
    /// JSON 源生成器上下文（用于 Native AOT 兼容性）
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    )]
    [JsonSerializable(typeof(WebAppDataExporter.ExportMetadata))]
    [JsonSerializable(typeof(List<WebAppShortcutStore.StoredWebAppShortcut>))]
    internal partial class WebAppJsonContext : JsonSerializerContext
    {
    }
}
