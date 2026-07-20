// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DockedAI.功能.页面.设置.网页组设置;

/// <summary>
/// 图标数据模型（复制自 WinUI Gallery）
/// </summary>
public sealed class IconData
{
    /// <summary>
    /// 图标名称（例如：Accept）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unicode 字符（例如：\uE8FB）
    /// </summary>
    public string Character { get; set; } = string.Empty;

    /// <summary>
    /// 十六进制代码（例如：E8FB）
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 标签列表（用于搜索）
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// 是否仅在 Segoe Fluent Icons 中可用
    /// </summary>
    public bool IsSegoeFluentOnly { get; set; }

    /// <summary>
    /// 实际的 Unicode 字形（用于 XAML 绑定）
    /// 将十六进制 Code 转换为 Unicode 字符
    /// </summary>
    public string Glyph
    {
        get
        {
            if (string.IsNullOrEmpty(Code)) return string.Empty;
            
            try
            {
                var hexValue = Convert.ToInt32(Code, 16);
                return char.ConvertFromUtf32(hexValue);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}

/// <summary>
/// 图标数据 JSON 序列化上下文（用于 Native AOT 兼容）
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(List<IconData>))]
internal partial class IconDataJsonContext : JsonSerializerContext
{
}
