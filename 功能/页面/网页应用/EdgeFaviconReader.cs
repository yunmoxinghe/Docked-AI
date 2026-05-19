using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Docked_AI.Features.Pages.WebApp.EdgeSync
{
    /// <summary>
    /// Edge Favicon 数据库读取服务
    /// </summary>
    public class EdgeFaviconReader : IDisposable
    {
        private readonly string _faviconsDbPath;
        private SqliteConnection? _connection;
        private bool _disposed;

        public EdgeFaviconReader()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _faviconsDbPath = Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Favicons");
        }

        /// <summary>
        /// 检查 Favicons 数据库是否存在（静态方法）
        /// </summary>
        public static bool IsFaviconsDbAvailable()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var faviconsDbPath = Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Favicons");
            return File.Exists(faviconsDbPath);
        }

        /// <summary>
        /// 检查当前实例的 Favicons 数据库是否存在
        /// </summary>
        private bool IsFaviconsDbAvailableForInstance()
        {
            return File.Exists(_faviconsDbPath);
        }

        /// <summary>
        /// 打开数据库连接
        /// </summary>
        private void OpenConnection()
        {
            if (_connection != null)
                return;

            if (!IsFaviconsDbAvailableForInstance())
                throw new FileNotFoundException("Edge Favicons 数据库文件不存在", _faviconsDbPath);

            try
            {
                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = _faviconsDbPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Cache = SqliteCacheMode.Shared
                }.ToString();

                _connection = new SqliteConnection(connectionString);
                
                // 设置繁忙超时为 1 秒，避免长时间阻塞
                _connection.Open();
                using var command = _connection.CreateCommand();
                command.CommandText = "PRAGMA busy_timeout = 1000";
                command.ExecuteNonQuery();
                
                System.Diagnostics.Debug.WriteLine("[EdgeFaviconReader] Database connection opened successfully");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
            {
                System.Diagnostics.Debug.WriteLine("[EdgeFaviconReader] Database is locked by Edge browser, skipping favicon loading");
                _connection?.Dispose();
                _connection = null;
                throw; // 重新抛出，让调用者知道连接失败
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EdgeFaviconReader] Failed to open database: {ex.Message}");
                _connection?.Dispose();
                _connection = null;
                throw;
            }
        }

        /// <summary>
        /// 根据页面 URL 获取 Favicon 图标数据
        /// </summary>
        /// <param name="pageUrl">页面 URL</param>
        /// <returns>图标的字节数组，如果未找到则返回 null</returns>
        public byte[]? GetFaviconForUrl(string pageUrl)
        {
            if (string.IsNullOrWhiteSpace(pageUrl))
                return null;

            try
            {
                OpenConnection();

                if (_connection == null)
                    return null;

                // Edge/Chrome 的 Favicons 数据库结构：
                // icon_mapping 表：page_url -> icon_id
                // favicons 表：id -> url
                // favicon_bitmaps 表：icon_id -> image_data
                
                // 查询语句：通过 page_url 找到对应的图标数据
                const string query = @"
                    SELECT fb.image_data
                    FROM icon_mapping im
                    INNER JOIN favicon_bitmaps fb ON im.icon_id = fb.icon_id
                    WHERE im.page_url = @pageUrl
                    ORDER BY fb.last_updated DESC
                    LIMIT 1";

                using var command = _connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.AddWithValue("@pageUrl", pageUrl);

                using var reader = command.ExecuteReader();
                if (reader.Read() && !reader.IsDBNull(0))
                {
                    var imageData = (byte[])reader.GetValue(0);
                    System.Diagnostics.Debug.WriteLine($"[EdgeFaviconReader] Found favicon for {pageUrl}, size: {imageData.Length} bytes");
                    return imageData;
                }

                System.Diagnostics.Debug.WriteLine($"[EdgeFaviconReader] No favicon found for {pageUrl}");
                return null;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
            {
                System.Diagnostics.Debug.WriteLine($"[EdgeFaviconReader] Database busy for {pageUrl}, Edge may be running. Skipping favicon.");
                return null;
            }
            catch (SqliteException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EdgeFaviconReader] SQLite Error {ex.SqliteErrorCode} for {pageUrl}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EdgeFaviconReader] Error getting favicon for {pageUrl}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 批量获取多个 URL 的 Favicon
        /// </summary>
        /// <param name="pageUrls">页面 URL 列表</param>
        /// <returns>URL 到图标数据的字典</returns>
        public Dictionary<string, byte[]> GetFaviconsForUrls(IEnumerable<string> pageUrls)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            if (pageUrls == null)
                return result;

            try
            {
                OpenConnection();

                if (_connection == null)
                    return result;

                foreach (var url in pageUrls)
                {
                    if (string.IsNullOrWhiteSpace(url))
                        continue;

                    var iconData = GetFaviconForUrl(url);
                    if (iconData != null && iconData.Length > 0)
                    {
                        result[url] = iconData;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EdgeFaviconReader] Error in batch favicon retrieval: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 尝试通过域名获取 Favicon（当精确 URL 匹配失败时使用）
        /// </summary>
        /// <param name="pageUrl">页面 URL</param>
        /// <returns>图标的字节数组，如果未找到则返回 null</returns>
        public byte[]? GetFaviconByDomain(string pageUrl)
        {
            if (string.IsNullOrWhiteSpace(pageUrl))
                return null;

            try
            {
                // 先尝试精确匹配
                var exactMatch = GetFaviconForUrl(pageUrl);
                if (exactMatch != null)
                    return exactMatch;

                // 提取域名进行模糊匹配
                if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri))
                    return null;

                var domain = uri.Host;
                
                OpenConnection();

                if (_connection == null)
                    return null;

                // 使用 LIKE 查询匹配同域名下的任何页面
                const string query = @"
                    SELECT fb.image_data
                    FROM icon_mapping im
                    INNER JOIN favicon_bitmaps fb ON im.icon_id = fb.icon_id
                    WHERE im.page_url LIKE @domainPattern
                    ORDER BY fb.last_updated DESC
                    LIMIT 1";

                using var command = _connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.AddWithValue("@domainPattern", $"%{domain}%");

                using var reader = command.ExecuteReader();
                if (reader.Read() && !reader.IsDBNull(0))
                {
                    var imageData = (byte[])reader.GetValue(0);
                    System.Diagnostics.Debug.WriteLine($"[EdgeFaviconReader] Found favicon by domain for {pageUrl}, size: {imageData.Length} bytes");
                    return imageData;
                }

                return null;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
            {
                System.Diagnostics.Debug.WriteLine($"[EdgeFaviconReader] Database busy for {pageUrl}, Edge may be running. Skipping favicon.");
                return null;
            }
            catch (SqliteException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EdgeFaviconReader] SQLite Error {ex.SqliteErrorCode} for {pageUrl}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EdgeFaviconReader] Error getting favicon by domain for {pageUrl}: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _connection?.Close();
            _connection?.Dispose();
            _connection = null;

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
