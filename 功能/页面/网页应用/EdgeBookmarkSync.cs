using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Data.Sqlite;
using Docked_AI.Features.Pages.WebApp.Shared;

namespace Docked_AI.Features.Pages.WebApp.EdgeSync
{
    /// <summary>
    /// Edge 收藏夹同步服务
    /// </summary>
    public class EdgeBookmarkSyncService
    {
        private const string SettingsKey = "EdgeBookmarkSync_Enabled";
        private const string FolderPathKey = "EdgeBookmarkSync_FolderPath";
        private const string LastSyncTimeKey = "EdgeBookmarkSync_LastSyncTime";

        private static readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        /// <summary>
        /// 获取或设置是否启用 Edge 收藏夹同步
        /// </summary>
        public static bool IsEnabled
        {
            get => _localSettings.Values.TryGetValue(SettingsKey, out var value) && value is bool enabled && enabled;
            set => _localSettings.Values[SettingsKey] = value;
        }

        /// <summary>
        /// 获取或设置要同步的收藏夹文件夹路径（在 Edge 收藏夹中的路径）
        /// </summary>
        public static string SyncFolderPath
        {
            get => _localSettings.Values.TryGetValue(FolderPathKey, out var value) && value is string path ? path : "";
            set => _localSettings.Values[FolderPathKey] = value;
        }

        /// <summary>
        /// 获取上次同步时间
        /// </summary>
        public static DateTime? LastSyncTime
        {
            get
            {
                if (_localSettings.Values.TryGetValue(LastSyncTimeKey, out var value) && value is long ticks)
                {
                    return new DateTime(ticks);
                }
                return null;
            }
            private set
            {
                if (value.HasValue)
                {
                    _localSettings.Values[LastSyncTimeKey] = value.Value.Ticks;
                }
                else
                {
                    _localSettings.Values.Remove(LastSyncTimeKey);
                }
            }
        }

        /// <summary>
        /// 获取 Edge 收藏夹文件路径
        /// </summary>
        private static string GetEdgeBookmarksPath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Bookmarks");
        }

        /// <summary>
        /// 检查 Edge 收藏夹文件是否存在
        /// </summary>
        public static bool IsEdgeBookmarksAvailable()
        {
            var bookmarksPath = GetEdgeBookmarksPath();
            return File.Exists(bookmarksPath);
        }

        /// <summary>
        /// 从 Edge 收藏夹同步到应用
        /// </summary>
        public static async Task<SyncResult> SyncFromEdgeAsync()
        {
            var result = new SyncResult();

            try
            {
                System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] Starting sync...");
                
                if (!IsEdgeBookmarksAvailable())
                {
                    result.Success = false;
                    result.Message = "未找到 Edge 收藏夹文件";
                    System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] Bookmarks file not found");
                    return result;
                }

                var bookmarksPath = GetEdgeBookmarksPath();
                System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] Reading from: {bookmarksPath}");
                
                var jsonContent = await File.ReadAllTextAsync(bookmarksPath);
                
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    result.Success = false;
                    result.Message = "Edge 收藏夹文件为空";
                    System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] File is empty");
                    return result;
                }

                System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] JSON content length: {jsonContent.Length}");
                
                var bookmarksData = JsonDocument.Parse(jsonContent);

                var bookmarks = new List<EdgeBookmark>();
                
                // 解析收藏夹
                if (bookmarksData.RootElement.TryGetProperty("roots", out var roots))
                {
                    // 遍历所有根节点（书签栏、其他收藏夹等）
                    if (roots.TryGetProperty("bookmark_bar", out var bookmarkBar))
                    {
                        System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] Parsing bookmark_bar...");
                        ParseBookmarkNode(bookmarkBar, "", bookmarks);
                    }
                    if (roots.TryGetProperty("other", out var other))
                    {
                        System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] Parsing other...");
                        ParseBookmarkNode(other, "", bookmarks);
                    }
                    if (roots.TryGetProperty("synced", out var synced))
                    {
                        System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] Parsing synced...");
                        ParseBookmarkNode(synced, "", bookmarks);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] Total bookmarks parsed: {bookmarks.Count}");

                if (bookmarks.Count == 0)
                {
                    result.Success = true;
                    result.AddedCount = 0;
                    result.Message = "Edge 收藏夹中没有找到书签";
                    System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] No bookmarks found");
                    return result;
                }

                // 过滤指定文件夹
                var targetBookmarks = bookmarks;
                if (!string.IsNullOrWhiteSpace(SyncFolderPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] Filtering by folder: {SyncFolderPath}");
                    targetBookmarks = bookmarks.Where(b => 
                        b.FolderPath.StartsWith(SyncFolderPath, StringComparison.OrdinalIgnoreCase) ||
                        b.FolderPath.Equals(SyncFolderPath, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                    System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] Filtered bookmarks: {targetBookmarks.Count}");
                }

                // 获取现有的快捷方式
                System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] Loading existing shortcuts...");
                var existingShortcuts = await WebAppShortcutStore.LoadAsync();
                System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] Existing shortcuts: {existingShortcuts.Count}");
                
                var existingUrls = new HashSet<string>(existingShortcuts.Select(s => s.Url), StringComparer.OrdinalIgnoreCase);
                var allShortcuts = existingShortcuts.ToList();

                // 读取 Favicons（在后台线程执行，避免阻塞 UI）
                System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] Loading favicons from Edge database...");
                Dictionary<string, byte[]> favicons = new Dictionary<string, byte[]>();
                bool faviconLoadFailed = false;
                
                if (!EdgeFaviconReader.IsFaviconsDbAvailable())
                {
                    System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] Favicons database not available");
                }
                else
                {
                    var newBookmarks = targetBookmarks.Where(b => !existingUrls.Contains(b.Url)).ToList();
                    
                    if (newBookmarks.Count > 0)
                    {
                        // 在后台线程执行 favicon 加载，避免阻塞 UI
                        try
                        {
                            favicons = await Task.Run(() =>
                            {
                                var result = new Dictionary<string, byte[]>();
                                EdgeFaviconReader? faviconReader = null;
                                
                                try
                                {
                                    faviconReader = new EdgeFaviconReader();
                                    
                                    System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] Testing database connection with first bookmark...");
                                    
                                    // 测试连接 - 如果这里失败，就跳过所有 favicon 加载
                                    var testIcon = faviconReader.GetFaviconByDomain(newBookmarks[0].Url);
                                    
                                    // 连接成功，继续加载其余的
                                    if (testIcon != null && testIcon.Length > 0)
                                    {
                                        result[newBookmarks[0].Url] = testIcon;
                                    }
                                    
                                    // 加载剩余的 favicons
                                    for (int i = 1; i < newBookmarks.Count; i++)
                                    {
                                        var bookmark = newBookmarks[i];
                                        var iconData = faviconReader.GetFaviconByDomain(bookmark.Url);
                                        if (iconData != null && iconData.Length > 0)
                                        {
                                            result[bookmark.Url] = iconData;
                                        }
                                    }
                                    
                                    System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] Loaded {result.Count} favicons");
                                }
                                catch (SqliteException ex) when (ex.SqliteErrorCode == 5)
                                {
                                    System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] Database is busy (Edge is running), skipping all favicon loading");
                                    result.Clear();
                                    throw; // 重新抛出以设置 faviconLoadFailed
                                }
                                finally
                                {
                                    faviconReader?.Dispose();
                                }
                                
                                return result;
                            });
                        }
                        catch (SqliteException ex) when (ex.SqliteErrorCode == 5)
                        {
                            System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] Database is busy (Edge is running), skipping all favicon loading");
                            faviconLoadFailed = true;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] Failed to load favicons: {ex.Message}");
                            faviconLoadFailed = true;
                        }
                    }
                }

                // 添加新的书签
                int addedCount = 0;
                foreach (var bookmark in targetBookmarks)
                {
                    if (!existingUrls.Contains(bookmark.Url))
                    {
                        // 尝试获取图标，如果没有则为 null
                        favicons.TryGetValue(bookmark.Url, out var iconBytes);

                        var shortcut = new WebAppShortcut(
                            Guid.NewGuid().ToString(),
                            bookmark.Name,
                            bookmark.Url,
                            iconBytes
                        );

                        allShortcuts.Add(shortcut);
                        existingUrls.Add(bookmark.Url); // 防止重复添加
                        addedCount++;
                        
                        var iconStatus = iconBytes != null ? $"with icon ({iconBytes.Length} bytes)" : "without icon";
                        System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] Added: {bookmark.Name} - {bookmark.Url} {iconStatus}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] Total new bookmarks to add: {addedCount}");

                // 一次性保存所有新增的书签
                if (addedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] Saving shortcuts...");
                    await WebAppShortcutStore.SaveAsync(allShortcuts);
                    System.Diagnostics.Debug.WriteLine("[EdgeBookmarkSync] Shortcuts saved successfully");
                }

                LastSyncTime = DateTime.Now;
                result.Success = true;
                result.AddedCount = addedCount;
                
                // 根据图标加载情况生成消息
                if (faviconLoadFailed)
                {
                    result.Message = $"同步完成，新增 {addedCount} 个书签（图标加载失败，可能 Edge 正在运行）";
                }
                else if (addedCount > 0 && favicons.Count == 0)
                {
                    result.Message = $"同步完成，新增 {addedCount} 个书签（未找到图标）";
                }
                else
                {
                    result.Message = $"同步完成，新增 {addedCount} 个书签";
                }
                
                System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] Sync completed: {result.Message}");
            }
            catch (JsonException jsonEx)
            {
                result.Success = false;
                result.Message = $"解析 Edge 收藏夹文件失败: {jsonEx.Message}";
                System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] JSON Error: {jsonEx}");
            }
            catch (IOException ioEx)
            {
                result.Success = false;
                result.Message = $"读取 Edge 收藏夹文件失败: {ioEx.Message}";
                System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] IO Error: {ioEx}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"同步失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] Error: {ex}");
            }

            return result;
        }

        /// <summary>
        /// 递归解析书签节点
        /// </summary>
        private static void ParseBookmarkNode(JsonElement node, string parentPath, List<EdgeBookmark> bookmarks)
        {
            if (!node.TryGetProperty("type", out var typeElement))
                return;

            var type = typeElement.GetString();
            if (string.IsNullOrEmpty(type))
                return;

            var name = node.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";

            if (type == "folder")
            {
                // 构建当前路径，跳过空名称
                var currentPath = parentPath;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    currentPath = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";
                }
                
                if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in children.EnumerateArray())
                    {
                        ParseBookmarkNode(child, currentPath, bookmarks);
                    }
                }
            }
            else if (type == "url")
            {
                var url = node.TryGetProperty("url", out var urlElement) ? urlElement.GetString() ?? "" : "";
                
                if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(name))
                {
                    bookmarks.Add(new EdgeBookmark
                    {
                        Name = name,
                        Url = url,
                        FolderPath = parentPath
                    });
                }
            }
        }

        /// <summary>
        /// 获取 Edge 收藏夹文件夹列表
        /// </summary>
        public static async Task<List<string>> GetBookmarkFoldersAsync()
        {
            var folders = new List<string>();

            try
            {
                if (!IsEdgeBookmarksAvailable())
                    return folders;

                var bookmarksPath = GetEdgeBookmarksPath();
                var jsonContent = await File.ReadAllTextAsync(bookmarksPath);
                var bookmarksData = JsonDocument.Parse(jsonContent);

                if (bookmarksData.RootElement.TryGetProperty("roots", out var roots))
                {
                    if (roots.TryGetProperty("bookmark_bar", out var bookmarkBar))
                    {
                        CollectFolders(bookmarkBar, "", folders);
                    }
                    if (roots.TryGetProperty("other", out var other))
                    {
                        CollectFolders(other, "", folders);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EdgeBookmarkSync] GetBookmarkFolders Error: {ex}");
            }

            return folders;
        }

        private static void CollectFolders(JsonElement node, string parentPath, List<string> folders)
        {
            if (!node.TryGetProperty("type", out var typeElement))
                return;

            var type = typeElement.GetString();
            if (type != "folder")
                return;

            var name = node.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";
            
            // 构建当前路径，跳过空名称
            var currentPath = parentPath;
            if (!string.IsNullOrWhiteSpace(name))
            {
                currentPath = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";
                folders.Add(currentPath);
            }

            if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in children.EnumerateArray())
                {
                    CollectFolders(child, currentPath, folders);
                }
            }
        }
    }

    /// <summary>
    /// Edge 书签数据模型
    /// </summary>
    public class EdgeBookmark
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string FolderPath { get; set; } = "";
    }

    /// <summary>
    /// 同步结果
    /// </summary>
    public class SyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int AddedCount { get; set; }
    }
}
