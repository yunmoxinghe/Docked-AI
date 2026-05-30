using System;
using System.Runtime.CompilerServices;

namespace Docked_AI.Features.Shared.AotOptimization
{
    /// <summary>
    /// AOT 兼容的枚举验证扩展方法
    /// 替代 Enum.IsDefined() 以避免反射开销
    /// </summary>
    public static class EnumValidationExtensions
    {
        /// <summary>
        /// 验证 FrameAnimationType 枚举值是否有效
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidFrameAnimationType(int value)
        {
            return value >= 0 && value <= 7; // None=0 到 ScaleAnimation=7
        }

        /// <summary>
        /// 验证 WindowDockSide 枚举值是否有效
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidWindowDockSide(int value)
        {
            return value >= 0 && value <= 1; // Left=0, Right=1
        }

        /// <summary>
        /// 验证 TrayCloseWindowBehavior 枚举值是否有效
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidTrayCloseWindowBehavior(int value)
        {
            return value >= 0 && value <= 1; // DestroyWindow=0, RestartToTrayOnly=1
        }

        /// <summary>
        /// 验证 WebViewMemoryMode 枚举值是否有效
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidWebViewMemoryMode(int value)
        {
            return value >= 0 && value <= 1; // Normal=0, Low=1
        }
    }

    /// <summary>
    /// AOT 兼容的异常辅助方法
    /// 优化异常类型名称获取，减少反射开销
    /// </summary>
    public static class ExceptionHelper
    {
        /// <summary>
        /// 获取异常类型名称（AOT 优化版本）
        /// 在调试模式下使用 GetType().Name，在发布模式下使用简化版本
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetExceptionTypeName(Exception ex)
        {
#if DEBUG
            // 调试模式：保留完整类型信息以便调试
            return ex.GetType().Name;
#else
            // 发布模式：使用模式匹配避免反射（AOT 友好）
            return ex switch
            {
                ArgumentNullException => "ArgumentNullException",
                ArgumentException => "ArgumentException",
                InvalidOperationException => "InvalidOperationException",
                NotSupportedException => "NotSupportedException",
                NullReferenceException => "NullReferenceException",
                System.IO.IOException => "IOException",
                System.Net.Http.HttpRequestException => "HttpRequestException",
                System.Threading.Tasks.TaskCanceledException => "TaskCanceledException",
                _ => "Exception" // 通用异常类型
            };
#endif
        }
    }
}
