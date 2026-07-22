# 托盘右键菜单服务 (TrayContextMenuService)

统一管理托盘图标的右键菜单，提供标准菜单项和扩展能力。

## 功能特性

- ✅ 标准菜单项（打开窗口、退出）
- ✅ 开发工具子菜单（重启测试等）
- ✅ 支持自定义菜单项
- ✅ 自动本地化支持

## 快速集成

### 1. 在托盘管理器中使用

修改 `功能/托盘/托盘图标管理器.cs` 的 `CreateTrayMenu` 方法：

```csharp
using Docked_AI.功能.统一调用.托盘右键菜单;

private MenuFlyout CreateTrayMenu()
{
    // 使用统一服务创建菜单
    _trayMenu = TrayContextMenuService.CreateTrayMenu(
        onOpenWindow: () => ShowMainWindow(),
        onExit: () => ExitApplication()
    );
    
    return _trayMenu;
}
```

### 2. 完整替换示例

```csharp
/// <summary>
/// 托盘图标右键点击事件处理函数
/// </summary>
private void TrayIcon_RightClick(SystemTrayIcon sender, SystemTrayIconEventArgs args)
{
    // 使用统一服务创建菜单
    args.Flyout = TrayContextMenuService.CreateTrayMenu(
        onOpenWindow: ShowMainWindow,
        onExit: ExitApplication
    );
}
```

## 内置菜单项

### 标准菜单
- **打开主窗口** - 显示/激活主窗口
- **退出** - 退出应用程序

### 开发工具子菜单
- **🔄 测试重启** - 测试基础重启功能
- **🔄 测试重启（带参数）** - 测试带 `--restart-from=tray-test` 参数的重启
- **⏱️ 测试延迟重启（1秒）** - 测试延迟 1 秒后重启

## 添加自定义菜单项

### 方式 1：直接添加到现有菜单

```csharp
var menu = TrayContextMenuService.CreateTrayMenu(
    onOpenWindow: ShowMainWindow,
    onExit: ExitApplication
);

// 添加自定义菜单项
TrayContextMenuService.AddCustomMenuItem(
    menu,
    text: "⚙️ 设置",
    icon: "\uE713", // 设置图标
    onClick: () => OpenSettings()
);
```

### 方式 2：扩展服务类

创建自己的扩展方法：

```csharp
public static class TrayContextMenuExtensions
{
    public static void AddSettingsMenu(this MenuFlyout flyout, Action onOpenSettings)
    {
        var settingsItem = new MenuFlyoutItem
        {
            Text = "⚙️ 设置",
            Icon = new FontIcon { Glyph = "\uE713" }
        };
        settingsItem.Click += (s, e) => onOpenSettings?.Invoke();
        
        // 插入到退出按钮之前
        flyout.Items.Insert(flyout.Items.Count - 1, settingsItem);
    }
}

// 使用
var menu = TrayContextMenuService.CreateTrayMenu(...);
menu.AddSettingsMenu(() => OpenSettings());
```

## 测试重启功能

### 从托盘菜单测试

1. 右键点击托盘图标
2. 选择 **🔧 开发工具** → **🔄 测试重启**
3. 应用会立即重启

### 检查重启来源

在 `App.xaml.cs` 的 `OnLaunched` 中：

```csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    // 检查是否从托盘测试重启
    if (AppRestartService.GetRestartSource() == "tray-test")
    {
        System.Diagnostics.Debug.WriteLine("✅ 托盘重启测试成功！");
        // 可以显示通知
    }
}
```

## 常见图标字形

| 图标 | 字形代码 | 说明 |
|------|---------|------|
| 🪟 | `\uE78B` | 窗口 |
| ❌ | `\uF3B1` | 关闭 |
| 🔄 | `\uE72C` | 刷新/重启 |
| ⚙️ | `\uE713` | 设置 |
| 🔧 | `\uEC7A` | 工具 |
| ⏱️ | `\uE916` | 时钟 |
| 📋 | `\uE8C8` | 剪贴板 |
| 🔔 | `\uE7E7` | 通知 |
| ℹ️ | `\uE946` | 信息 |

完整图标列表：[Segoe Fluent Icons](https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-fluent-icons-font)

## 本地化支持

菜单文本自动从本地化资源读取：

```xml
<!-- Resources.resw -->
<data name="TrayMenu_OpenWindow">
  <value>打开主窗口</value>
</data>
<data name="TrayMenu_Exit">
  <value>退出</value>
</data>
```

切换语言后调用 `RefreshTrayMenu()` 刷新菜单。

## 架构说明

### 设计原则
- **统一入口**：所有托盘菜单通过服务创建，避免重复代码
- **可扩展**：支持添加自定义菜单项
- **解耦**：通过回调函数与具体实现解耦

### 依赖关系
```
TrayContextMenuService
  ├── AppRestartService (重启功能)
  ├── LocalizationHelper (本地化)
  └── MenuFlyout (WinUI 控件)
```

## 注意事项

⚠️ **开发工具菜单**：生产环境可以通过条件编译移除

```csharp
#if DEBUG
AddDevelopmentMenu(flyout);
#endif
```

⚠️ **菜单缓存**：如果需要动态更新菜单，不要缓存 `MenuFlyout` 对象

⚠️ **线程安全**：菜单创建必须在 UI 线程上执行

## 未来扩展

- [ ] 支持菜单项启用/禁用状态
- [ ] 支持菜单项可见性控制
- [ ] 支持子菜单嵌套
- [ ] 集成通知系统
- [ ] 支持菜单项快捷键
