// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DockedAI.功能.页面.设置.网页组设置;

/// <summary>
/// 图标数据源加载器（复制自 WinUI Gallery）
/// </summary>
internal sealed class IconsDataSource
{
    public static IconsDataSource Instance { get; } = new();

    public static List<IconData> Icons => Instance._icons;

    private List<IconData> _icons = new();
    private readonly object _lock = new();

    private IconsDataSource() { }

    /// <summary>
    /// 从 JSON 文件加载图标数据
    /// </summary>
    public async Task<List<IconData>> LoadIconsAsync()
    {
        lock (_lock)
        {
            if (_icons.Count != 0)
            {
                return _icons;
            }
        }

        try
        {
            // 读取嵌入的 JSON 文件
            var jsonPath = Path.Combine(
                Windows.ApplicationModel.Package.Current.InstalledLocation.Path,
                "功能", "页面", "设置", "网页组设置", "图标数据.json"
            );

            var jsonText = await File.ReadAllTextAsync(jsonPath);

            lock (_lock)
            {
                if (_icons.Count == 0)
                {
                    // ✅ 使用源生成器进行反序列化（Native AOT 兼容）
                    var loadedIcons = JsonSerializer.Deserialize(
                        jsonText, 
                        IconDataJsonContext.Default.ListIconData);
                    if (loadedIcons != null)
                    {
                        _icons = loadedIcons;
                    }
                }

                return _icons;
            }
        }
        catch
        {
            // 如果加载失败，返回空列表
            return new List<IconData>();
        }
    }

    /// <summary>
    /// 搜索图标（按名称或标签）
    /// </summary>
    public List<IconData> SearchIcons(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return _icons;
        }

        var lowerQuery = query.ToLower();
        return _icons.Where(icon =>
            icon.Name.ToLower().Contains(lowerQuery) ||
            (icon.Tags != null && icon.Tags.Any(tag => tag.ToLower().Contains(lowerQuery)))
        ).ToList();
    }
}
