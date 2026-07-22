using System;
using System.Runtime.InteropServices;

namespace DockedTools.Features.AppEntry
{
    /// <summary>
    /// 异常策略辅助类，用于判断 XAML 异常是否可以被安全处理（handled）
    /// </summary>
    /// <remarks>
    /// 设计原则：
    /// - 将常见的可恢复异常（NullReferenceException、ObjectDisposedException、InvalidOperationException、COMException）归为可处理候选
    /// - 将严重异常（OutOfMemoryException、StackOverflowException、AccessViolationException）排除，让应用崩溃以避免状态污染
    /// - 所有异常仍会被完整记录到日志中
    /// </remarks>
    internal static class ExceptionPolicy
    {
        /// <summary>
        /// 判断给定的 XAML 异常是否应该被标记为 handled
        /// </summary>
        /// <param name="exception">要判断的异常</param>
        /// <returns>
        /// 如果异常可以安全恢复返回 true（应设置 e.Handled = true）；
        /// 如果异常属于严重错误返回 false（让应用崩溃以保持状态一致性）
        /// </returns>
        public static bool ShouldHandleXamlException(Exception exception)
        {
            if (exception == null)
            {
                return false;
            }

            // 严重异常：不应被 handled，让应用崩溃
            // OutOfMemoryException: 内存耗尽，继续运行会导致不可预测的行为
            // StackOverflowException: 栈溢出，通常无法可靠捕获（.NET 限制）
            // AccessViolationException: 访问冲突，通常是 native 代码错误
            if (exception is OutOfMemoryException ||
                exception is StackOverflowException ||
                exception is AccessViolationException)
            {
                return false;
            }

            // 可恢复异常：可以被 handled
            // NullReferenceException: 空引用，通常是 UI 绑定或事件处理中的空值访问
            // ObjectDisposedException: 对象已释放，通常发生在窗口关闭或组件清理时
            // InvalidOperationException: 非法操作，通常是状态不一致或时序问题
            // COMException: COM 互操作异常，常见于 WinRT/XAML 边界
            if (exception is NullReferenceException ||
                exception is ObjectDisposedException ||
                exception is InvalidOperationException ||
                exception is COMException)
            {
                return true;
            }

            // 递归检查内部异常
            // 如果内部异常是严重异常，则不应 handled
            if (exception.InnerException != null)
            {
                if (!ShouldHandleXamlException(exception.InnerException))
                {
                    return false;
                }
            }

            // 对于未明确分类的异常，初期策略是 handled（记录并观察）
            // 后续可以根据日志数据细化策略
            return true;
        }
    }
}
