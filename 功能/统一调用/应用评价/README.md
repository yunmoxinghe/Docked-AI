# 应用评价功能

## 概述
提供应用内评价和跳转商店评价功能，让用户可以方便地为应用评分和评论。

## 功能特性

### 1. 应用内评价（推荐）
- 使用 `StoreContext.RequestRateAndReviewAppAsync()` API
- 在应用内弹出原生评价对话框
- 用户无需离开应用即可完成评价
- 更流畅的用户体验

### 2. 跳转商店评价（备用）
- 使用 `Launcher.LaunchUriAsync()` API
- 打开 Microsoft Store 应用的评价页面
- 适用于应用内评价不可用的情况（如开发环境）

### 3. 智能评价
- 自动选择最佳评价方式
- 优先使用应用内评价
- 失败时自动降级到商店评价

## 使用方法

### 基本用法

```csharp
using Docked_AI.功能.统一调用.应用评价;

// 智能评价（推荐）
await StoreRatingService.RequestRatingAsync();

// 仅应用内评价
var status = await StoreRatingService.ShowInAppRatingDialogAsync();

// 仅跳转商店
await StoreRatingService.LaunchStoreReviewPageAsync();
```

### 在托盘菜单中使用

评价功能已集成到托盘右键菜单中：
- 鼠标模式菜单：`MouseRateApp`
- 触摸模式菜单：`TouchRateApp`

## 重要注意事项

### 1. 仅在已发布应用中有效
应用内评价 API 只在从 Microsoft Store 安装的应用中工作。在开发环境中：
- `RequestRateAndReviewAppAsync()` 会返回 `Error` 状态
- 自动降级到跳转商店评价

### 2. 频率限制
- 不要频繁调用评价 API
- 建议在用户使用应用一段时间后再提示
- 可以记录用户是否已评价，避免重复提示

### 3. 最佳实践
- ✅ 在用户完成重要操作后提示
- ✅ 在用户表现出满意时提示
- ❌ 不要在应用启动时立即提示
- ❌ 不要在用户遇到错误时提示

## 评价状态

`StoreRateAndReviewStatus` 枚举值：
- `Succeeded`: 用户成功提交评价
- `CanceledByUser`: 用户取消评价
- `NetworkError`: 网络错误
- `Error`: 其他错误（如开发环境、API 不可用等）

## 商店标识

当前应用的 Product ID: `9NX1DZB3WNWP`

## 相关链接

- [Microsoft Store 应用页面](https://www.microsoft.com/store/apps/9NX1DZB3WNWP)
- [StoreContext 文档](https://learn.microsoft.com/uwp/api/windows.services.store.storecontext)
- [Launcher 文档](https://learn.microsoft.com/uwp/api/windows.system.launcher)
