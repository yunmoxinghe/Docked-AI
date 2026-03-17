# GestureNavigation

触摸板双指左右滑动前进/后退手势导航，适用于 WinUI 3 项目。

## 文件说明

| 文件 | 说明 |
|------|------|
| `NavigationInteractionTracker.cs` | 核心手势跟踪逻辑（InteractionTracker + Composition 动画） |
| `GestureNavigationContainer.xaml` | 包含手势指示器 UI 的容器控件 |
| `GestureNavigationContainer.xaml.cs` | 容器控件 code-behind |

## 接入步骤

### 1. 复制文件夹到目标项目，修改 namespace

将三个文件中的 `namespace GestureNavigation` 改为你的项目 namespace。

### 2. 在 XAML 中使用容器控件

```xml
xmlns:gesture="using:YourNamespace"

<!-- 用容器包裹你的 Frame -->
<gesture:GestureNavigationContainer
    x:Name="GestureContainer"
    NavigationRequested="GestureContainer_NavigationRequested">
    <Frame x:Name="ContentFrame"/>
</gesture:GestureNavigationContainer>
```

### 3. 在 code-behind 中处理导航

```csharp
private void GestureContainer_NavigationRequested(object sender, OverscrollNavigationDirection direction)
{
    if (direction == OverscrollNavigationDirection.Back && ContentFrame.CanGoBack)
        ContentFrame.GoBack();
    else if (direction == OverscrollNavigationDirection.Forward && ContentFrame.CanGoForward)
        ContentFrame.GoForward();
}

// 每次导航后同步状态
private void UpdateGestureState()
{
    GestureContainer.CanGoBack = ContentFrame.CanGoBack;
    GestureContainer.CanGoForward = ContentFrame.CanGoForward;
}
```

### 4. 可选：设置指示器图标

```csharp
// 显示目标页面的图标（Segoe Fluent Icons glyph）
GestureContainer.BackIcon = "\uE80F";    // 后退目标页图标
GestureContainer.ForwardIcon = "\uE965"; // 前进目标页图标
```

## 不使用 UserControl 的方式（直接集成）

如果不想用 UserControl，可以直接在 Window/Page 的 XAML 里放指示器 Border，
然后手动实例化 `NavigationInteractionTracker`：

```csharp
var tracker = new NavigationInteractionTracker(ContentFrameContainer, BackIcon, ForwardIcon);
tracker.NavigationRequested += (_, dir) => { /* 执行导航 */ };
tracker.CanNavigateBackward = true;
tracker.CanNavigateForward = false;

// 页面关闭时
tracker.Dispose();
```

## 依赖

- WinUI 3（`Microsoft.WindowsAppSDK`）
- 无需额外 NuGet 包，`Microsoft.UI.Composition.Interactions` 已包含在 WinUI 3 中
