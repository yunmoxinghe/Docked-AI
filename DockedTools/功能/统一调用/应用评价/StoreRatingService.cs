using System;
using System.Threading.Tasks;
using Windows.Services.Store;
using Windows.System;

namespace DockedTools.功能.统一调用.应用评价;

/// <summary>
/// 应用商店评价服务
/// 提供应用内评价和跳转商店评价功能
/// </summary>
public static class StoreRatingService
{
    private const string ProductId = "9NX1DZB3WNWP";

    /// <summary>
    /// 显示应用内评价对话框（推荐方式）
    /// </summary>
    /// <returns>评价结果</returns>
    public static async Task<StoreRateAndReviewStatus> ShowInAppRatingDialogAsync()
    {
        try
        {
            StoreContext storeContext = StoreContext.GetDefault();
            StoreRateAndReviewResult result = await storeContext.RequestRateAndReviewAppAsync();

            LogRatingResult(result);
            return result.Status;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StoreRating] 显示应用内评价对话框失败: {ex.Message}");
            return StoreRateAndReviewStatus.Error;
        }
    }

    /// <summary>
    /// 跳转到商店评价页面（备用方式）
    /// </summary>
    /// <returns>是否成功跳转</returns>
    public static async Task<bool> LaunchStoreReviewPageAsync()
    {
        try
        {
            var uri = new Uri($"ms-windows-store://review/?ProductId={ProductId}");
            bool success = await Launcher.LaunchUriAsync(uri);
            
            System.Diagnostics.Debug.WriteLine($"[StoreRating] 跳转商店评价页面: {(success ? "成功" : "失败")}");
            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StoreRating] 跳转商店评价页面失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 跳转到商店应用详情页
    /// </summary>
    /// <returns>是否成功跳转</returns>
    public static async Task<bool> LaunchStoreProductPageAsync()
    {
        try
        {
            var uri = new Uri($"ms-windows-store://pdp/?ProductId={ProductId}");
            bool success = await Launcher.LaunchUriAsync(uri);
            
            System.Diagnostics.Debug.WriteLine($"[StoreRating] 跳转商店详情页: {(success ? "成功" : "失败")}");
            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StoreRating] 跳转商店详情页失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 智能评价：优先使用应用内评价，失败则跳转商店
    /// </summary>
    /// <returns>是否成功触发评价流程</returns>
    public static async Task<bool> RequestRatingAsync()
    {
        // 1. 优先尝试应用内评价
        var status = await ShowInAppRatingDialogAsync();

        // 2. 如果应用内评价不可用（开发环境或其他错误），则跳转商店
        if (status == StoreRateAndReviewStatus.Error)
        {
            System.Diagnostics.Debug.WriteLine("[StoreRating] 应用内评价不可用，尝试跳转商店");
            return await LaunchStoreReviewPageAsync();
        }

        // 3. 用户取消或成功都算作流程完成
        return status != StoreRateAndReviewStatus.Error;
    }

    /// <summary>
    /// 记录评价结果
    /// </summary>
    private static void LogRatingResult(StoreRateAndReviewResult result)
    {
        switch (result.Status)
        {
            case StoreRateAndReviewStatus.Succeeded:
                System.Diagnostics.Debug.WriteLine("[StoreRating] ✅ 用户成功提交评价");
                break;

            case StoreRateAndReviewStatus.CanceledByUser:
                System.Diagnostics.Debug.WriteLine("[StoreRating] ℹ️ 用户取消评价");
                break;

            case StoreRateAndReviewStatus.NetworkError:
                System.Diagnostics.Debug.WriteLine("[StoreRating] ❌ 网络错误");
                break;

            case StoreRateAndReviewStatus.Error:
                var errorMessage = result.ExtendedError?.Message ?? "未知错误";
                System.Diagnostics.Debug.WriteLine($"[StoreRating] ❌ 评价失败: {errorMessage}");
                break;
        }
    }
}
