using System;
using System.Diagnostics;

namespace Docked_AI.Features.MainWindowContent.ContentArea
{
    /// <summary>
    /// 导航防抖辅助类 - 使用 Stopwatch 实现线程安全的防抖机制
    /// </summary>
    public class NavigationDebouncer
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly int _debounceMilliseconds;
        private string? _lastKey;
        private readonly object _lock = new();

        /// <summary>
        /// 创建导航防抖器
        /// </summary>
        /// <param name="debounceMilliseconds">防抖时间（毫秒）</param>
        public NavigationDebouncer(int debounceMilliseconds = 300)
        {
            _debounceMilliseconds = debounceMilliseconds;
        }

        /// <summary>
        /// 检查是否应该防抖（阻止操作）
        /// </summary>
        /// <param name="key">操作键（用于区分不同的导航操作）</param>
        /// <returns>true 表示应该防抖（忽略操作），false 表示可以执行</returns>
        public bool ShouldDebounce(string key)
        {
            lock (_lock)
            {
                var elapsed = _stopwatch.ElapsedMilliseconds;

                // 如果是相同的操作且在防抖时间内，返回 true（阻止）
                if (_lastKey == key && elapsed < _debounceMilliseconds)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NavigationDebouncer] 防抖触发: {key} ({elapsed}ms < {_debounceMilliseconds}ms)");
                    return true;
                }

                // 更新状态并允许操作
                _lastKey = key;
                _stopwatch.Restart();
                return false;
            }
        }

        /// <summary>
        /// 重置防抖状态
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _lastKey = null;
                _stopwatch.Restart();
            }
        }

        /// <summary>
        /// 获取距离上次操作的时间（毫秒）
        /// </summary>
        public long ElapsedMilliseconds
        {
            get
            {
                lock (_lock)
                {
                    return _stopwatch.ElapsedMilliseconds;
                }
            }
        }
    }
}
