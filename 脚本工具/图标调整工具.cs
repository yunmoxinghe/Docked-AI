using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DockedAI.功能.工具;

/// <summary>
/// SVG 图标尺寸批量调整工具
/// 用于将 logos 目录中的所有 SVG 图标统一调整为 100×100 像素
/// </summary>
public static class 图标调整工具
{
    /// <summary>
    /// 批量调整指定目录中所有 SVG 文件的尺寸
    /// </summary>
    /// <param name="directoryPath">目标目录路径</param>
    /// <param name="targetWidth">目标宽度（像素）</param>
    /// <param name="targetHeight">目标高度（像素）</param>
    public static void 批量调整SVG尺寸(string directoryPath, int targetWidth = 100, int targetHeight = 100)
    {
        // 获取目录中所有 SVG 文件
        var svgFiles = Directory.GetFiles(directoryPath, "*.svg");
        
        Console.WriteLine($"找到 {svgFiles.Length} 个 SVG 文件");
        
        foreach (var filePath in svgFiles)
        {
            try
            {
                调整单个SVG尺寸(filePath, targetWidth, targetHeight);
                Console.WriteLine($"✅ 已处理: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 处理失败 {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }
        
        Console.WriteLine($"\n批量调整完成！共处理 {svgFiles.Length} 个文件。");
    }
    
    /// <summary>
    /// 调整单个 SVG 文件的尺寸
    /// </summary>
    private static void 调整单个SVG尺寸(string filePath, int targetWidth, int targetHeight)
    {
        // 读取 SVG 文件内容
        string svgContent = File.ReadAllText(filePath);
        
        // 正则表达式：匹配 <svg> 标签中的 width 和 height 属性
        string widthPattern = @"width\s*=\s*""[^""]*""";
        string heightPattern = @"height\s*=\s*""[^""]*""";
        
        // 检查是否已有 width 和 height 属性
        bool hasWidth = Regex.IsMatch(svgContent, widthPattern);
        bool hasHeight = Regex.IsMatch(svgContent, heightPattern);
        
        if (hasWidth && hasHeight)
        {
            // 替换现有的 width 和 height
            svgContent = Regex.Replace(svgContent, widthPattern, $"width=\"{targetWidth}\"");
            svgContent = Regex.Replace(svgContent, heightPattern, $"height=\"{targetHeight}\"");
        }
        else
        {
            // 在 <svg> 标签中添加 width 和 height 属性
            svgContent = Regex.Replace(
                svgContent,
                @"<svg\s+",
                $"<svg width=\"{targetWidth}\" height=\"{targetHeight}\" ",
                RegexOptions.IgnoreCase
            );
        }
        
        // 保存修改后的内容
        File.WriteAllText(filePath, svgContent);
    }
    
    /// <summary>
    /// 命令行入口（用于独立运行）
    /// </summary>
    public static void Main(string[] args)
    {
        // 获取当前 .cs 文件所在的目录
        string currentDir = Directory.GetCurrentDirectory();
        
        // 构建 Assets/logos 的路径
        string logosPath = Path.Combine(currentDir, "Assets", "logos");
        
        logosPath = Path.GetFullPath(logosPath);
        
        Console.WriteLine("SVG 图标批量调整工具");
        Console.WriteLine("====================");
        Console.WriteLine($"目标目录: {logosPath}");
        Console.WriteLine($"目标尺寸: 100×100 像素\n");
        
        if (!Directory.Exists(logosPath))
        {
            Console.WriteLine($"❌ 错误：目录不存在 - {logosPath}");
            return;
        }
        
        批量调整SVG尺寸(logosPath, 100, 100);
        
        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }
}
