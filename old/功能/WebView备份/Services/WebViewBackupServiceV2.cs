using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Docked_AI.功能.WebView备份.Services;

/// <summary>
/// WebView2 备份服务 V2 - 基于 UserDataFolder 的完整备份方案
/// 
/// 最佳实践：直接备份整个 UserDataFolder，包含所有浏览器数据
/// - Cookies
/// - LocalStorage
/// - SessionStorage  
/// - Cache
/// - IndexedDB
/// - 等等所有数据
/// 
/// 优点：
/// 1. 完整可靠 - 不会遗漏任何数据
/// 2. 简单高效 - 无需逐个处理 Cookie
/// 3. 原生支持 - 利用 Edge 内置机制
/// 
/// 信息来源：
/// https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder
/// </summary>
public class WebViewBackupServiceV2
{
    private readonly string _backupDirectory;

    public WebViewBackupServiceV2(string? backupDirectory = null)
    {
        _backupDirectory = backupDirectory 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                "WebView配置备份");

        Directory.CreateDirectory(_backupDirectory);
    }

    /// <summary>
    /// 备份 UserDataFolder 到压缩包
    /// </summary>
    /// <param name="userDataFolderPath">UserDataFolder 路径</param>
    /// <param name="backupName">备份名称</param>
    public async Task<string> BackupUserDataFolderAsync(string userDataFolderPath, string backupName)
    {
        if (!Directory.Exists(userDataFolderPath))
            throw new DirectoryNotFoundException($"UserDataFolder 不存在：{userDataFolderPath}");

        // 检查磁盘空间
        var driveInfo = new DriveInfo(Path.GetPathRoot(_backupDirectory)!);
        var estimatedSize = GetDirectorySize(userDataFolderPath);
        var requiredSpace = estimatedSize * 1.2; // 预留 20% 缓冲空间
        
        if (driveInfo.AvailableFreeSpace < requiredSpace)
        {
            throw new IOException($"磁盘空间不足！需要约 {requiredSpace / 1024 / 1024:F0} MB，可用 {driveInfo.AvailableFreeSpace / 1024 / 1024:F0} MB");
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var zipFileName = $"{SanitizeFileName(backupName)}_{timestamp}.网页状态备份";
        var zipPath = Path.Combine(_backupDirectory, zipFileName);

        System.Diagnostics.Debug.WriteLine($"[BackupV2] 开始备份：{userDataFolderPath}");
        System.Diagnostics.Debug.WriteLine($"[BackupV2] 目标文件：{zipPath}");
        System.Diagnostics.Debug.WriteLine($"[BackupV2] 预估大小：{estimatedSize / 1024 / 1024:F2} MB");

        // 🔥 关键：压缩整个 UserDataFolder
        await Task.Run(() =>
        {
            try
            {
                ZipFile.CreateFromDirectory(
                    userDataFolderPath,
                    zipPath,
                    CompressionLevel.Fastest,
                    includeBaseDirectory: false
                );
            }
            catch (IOException ex) when (ex.Message.Contains("being used"))
            {
                throw new IOException("某些文件正在被使用，请关闭所有网页应用窗口后重试", ex);
            }
        });

        var fileInfo = new FileInfo(zipPath);
        System.Diagnostics.Debug.WriteLine($"[BackupV2] ✅ 备份完成，大小：{fileInfo.Length / 1024 / 1024:F2} MB");

        return zipPath;
    }

    /// <summary>
    /// 计算目录大小（递归）
    /// </summary>
    private static long GetDirectorySize(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            return directory.EnumerateFiles("*", SearchOption.AllDirectories)
                           .Sum(file => file.Length);
        }
        catch
        {
            return 500 * 1024 * 1024; // 默认估计 500MB
        }
    }

    /// <summary>
    /// 从备份恢复 UserDataFolder
    /// </summary>
    /// <param name="zipFilePath">备份压缩包路径</param>
    /// <param name="targetUserDataFolder">目标 UserDataFolder 路径</param>
    public async Task RestoreUserDataFolderAsync(string zipFilePath, string targetUserDataFolder)
    {
        if (!File.Exists(zipFilePath))
            throw new FileNotFoundException("备份文件不存在", zipFilePath);

        System.Diagnostics.Debug.WriteLine($"[BackupV2] 开始恢复：{zipFilePath}");
        System.Diagnostics.Debug.WriteLine($"[BackupV2] 目标目录：{targetUserDataFolder}");

        // 验证 ZIP 文件完整性
        try
        {
            using var archive = ZipFile.OpenRead(zipFilePath);
            System.Diagnostics.Debug.WriteLine($"[BackupV2] ZIP 文件包含 {archive.Entries.Count} 个文件");
        }
        catch (Exception ex)
        {
            throw new IOException($"备份文件已损坏或格式不正确", ex);
        }

        // 清空目标目录（安全删除）
        if (Directory.Exists(targetUserDataFolder))
        {
            try
            {
                Directory.Delete(targetUserDataFolder, recursive: true);
            }
            catch (IOException ex) when (ex.Message.Contains("being used"))
            {
                throw new IOException("无法清空目标目录，请关闭所有网页应用窗口后重试", ex);
            }
        }

        Directory.CreateDirectory(targetUserDataFolder);

        // 解压到目标目录
        await Task.Run(() =>
        {
            ZipFile.ExtractToDirectory(zipFilePath, targetUserDataFolder, overwriteFiles: true);
        });

        System.Diagnostics.Debug.WriteLine($"[BackupV2] ✅ 恢复完成");
    }

    /// <summary>
    /// 列出所有备份
    /// </summary>
    public Task<string[]> ListBackupsAsync()
    {
        var zipFiles = Directory.GetFiles(_backupDirectory, "*.网页状态备份");
        Array.Sort(zipFiles);
        Array.Reverse(zipFiles); // 最新的在前面
        return Task.FromResult(zipFiles);
    }

    /// <summary>
    /// 删除备份
    /// </summary>
    public Task DeleteBackupAsync(string zipFilePath)
    {
        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取备份目录
    /// </summary>
    public string GetBackupDirectory() => _backupDirectory;

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}
