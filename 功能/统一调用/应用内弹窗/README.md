# 应用内弹窗使用指南

## 概述

统一应用内弹窗提供了一个标准化的对话框解决方案，用于在应用内显示各种提示、确认和输入对话框。该功能基于 WinUI 3 的 `ContentDialog`，并提供了线程安全的显示机制。

## 核心组件

### 1. UnifiedInAppDialog
可配置的内容对话框控件，支持自定义标题、内容和按钮。

### 2. InAppDialogService
静态服务类，提供线程安全的对话框显示功能，确保同一时间只显示一个对话框。

## 基本使用

### 简单消息提示

```csharp
using Docked_AI.Features.UnifiedCalls.InAppDialog;

// 创建对话框实例
var dialog = new UnifiedInAppDialog();

// 配置对话框
dialog.Configure(
    title: "提示",
    content: "操作已成功完成！",
    closeButtonText: "确定"
);

// 显示对话框
await InAppDialogService.ShowAsync(dialog);
```

### 确认对话框

```csharp
var dialog = new UnifiedInAppDialog();

dialog.Configure(
    title: "确认操作",
    content: "您确定要删除这个项目吗？",
    primaryButtonText: "删除",
    closeButtonText: "取消",
    defaultButton: ContentDialogButton.Close
);

var result = await InAppDialogService.ShowAsync(dialog);

if (result == ContentDialogResult.Primary)
{
    // 用户点击了"删除"按钮
    // 执行删除操作
}
```

### 三按钮对话框

```csharp
var dialog = new UnifiedInAppDialog();

dialog.Configure(
    title: "保存更改",
    content: "文件已修改，是否保存更改？",
    primaryButtonText: "保存",
    secondaryButtonText: "不保存",
    closeButtonText: "取消",
    defaultButton: ContentDialogButton.Primary
);

var result = await InAppDialogService.ShowAsync(dialog);

switch (result)
{
    case ContentDialogResult.Primary:
        // 保存文件
        break;
    case ContentDialogResult.Secondary:
        // 不保存，直接关闭
        break;
    case ContentDialogResult.None:
        // 取消操作
        break;
}
```


### 自定义内容对话框

```csharp
// 创建自定义内容
var customContent = new StackPanel
{
    Spacing = 10,
    Children =
    {
        new TextBlock { Text = "请输入您的名字：" },
        new TextBox { PlaceholderText = "名字" }
    }
};

var dialog = new UnifiedInAppDialog();

dialog.Configure(
    title: "用户信息",
    content: customContent,
    primaryButtonText: "提交",
    closeButtonText: "取消"
);

var result = await InAppDialogService.ShowAsync(dialog);
```

## Configure 方法参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `title` | `string` | 是 | 对话框标题 |
| `content` | `object` | 是 | 对话框内容，可以是字符串或任何 UI 元素 |
| `primaryButtonText` | `string?` | 否 | 主按钮文本（通常用于确认操作） |
| `closeButtonText` | `string?` | 否 | 关闭按钮文本（通常用于取消操作） |
| `secondaryButtonText` | `string?` | 否 | 次要按钮文本（第三个选项） |
| `defaultButton` | `ContentDialogButton` | 否 | 默认聚焦的按钮，默认为 `Close` |

## ContentDialogButton 枚举值

- `ContentDialogButton.None` - 无默认按钮
- `ContentDialogButton.Primary` - 主按钮为默认
- `ContentDialogButton.Secondary` - 次要按钮为默认
- `ContentDialogButton.Close` - 关闭按钮为默认

## ContentDialogResult 返回值

- `ContentDialogResult.None` - 用户点击了关闭按钮或按下 ESC
- `ContentDialogResult.Primary` - 用户点击了主按钮
- `ContentDialogResult.Secondary` - 用户点击了次要按钮

## 高级用法

### 指定 XamlRoot 所有者

```csharp
// 在特定的 UI 元素上下文中显示对话框
var dialog = new UnifiedInAppDialog();
dialog.Configure(
    title: "提示",
    content: "这是一个对话框"
);

// 传入当前页面或控件作为所有者
await InAppDialogService.ShowAsync(dialog, owner: this);
```

### 处理对话框显示失败

```csharp
var dialog = new UnifiedInAppDialog();
dialog.Configure(
    title: "提示",
    content: "消息内容"
);

var result = await InAppDialogService.ShowAsync(dialog);

if (result is null)
{
    // XamlRoot 不可用，对话框未能显示
    Debug.WriteLine("无法显示对话框：XamlRoot 不可用");
}
```

## 注意事项

1. **线程安全**：`InAppDialogService` 使用信号量确保同一时间只显示一个对话框，避免对话框重叠。

2. **XamlRoot 要求**：对话框必须有有效的 `XamlRoot` 才能显示。服务会自动尝试从提供的 `owner` 或应用主窗口获取。

3. **异步操作**：所有对话框显示操作都是异步的，需要使用 `await` 关键字。

4. **按钮文本**：如果不需要某个按钮，将其文本参数设置为 `null` 或空字符串即可隐藏该按钮。

5. **内容灵活性**：`content` 参数接受 `object` 类型，可以传入字符串、UI 元素或自定义控件。

## 常见使用场景

### 错误提示

```csharp
var dialog = new UnifiedInAppDialog();
dialog.Configure(
    title: "错误",
    content: "无法连接到服务器，请检查网络连接。",
    closeButtonText: "确定"
);
await InAppDialogService.ShowAsync(dialog);
```

### 成功提示

```csharp
var dialog = new UnifiedInAppDialog();
dialog.Configure(
    title: "成功",
    content: "数据已成功保存！",
    closeButtonText: "好的"
);
await InAppDialogService.ShowAsync(dialog);
```

### 危险操作确认

```csharp
var dialog = new UnifiedInAppDialog();
dialog.Configure(
    title: "警告",
    content: "此操作不可撤销，确定要继续吗？",
    primaryButtonText: "继续",
    closeButtonText: "取消",
    defaultButton: ContentDialogButton.Close
);

var result = await InAppDialogService.ShowAsync(dialog);
if (result == ContentDialogResult.Primary)
{
    // 执行危险操作
}
```

## 最佳实践

1. **明确的按钮文本**：使用清晰、具体的按钮文本，如"删除"而不是"是"。

2. **合理的默认按钮**：对于危险操作，将 `defaultButton` 设置为取消按钮。

3. **简洁的内容**：保持对话框内容简洁明了，避免过长的文本。

4. **一致的样式**：在整个应用中保持对话框样式的一致性。

5. **错误处理**：始终检查 `ShowAsync` 的返回值，处理 `null` 的情况。
