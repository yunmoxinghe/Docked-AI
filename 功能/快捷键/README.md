# 全局快捷键功能

## 功能说明

此功能允许用户设置全局快捷键来快速显示/隐藏应用主窗口，类似于点击托盘图标的效果。

## 文件结构

- `全局快捷键管理器.cs` - 负责注册和管理 Windows 系统级快捷键
- `快捷键设置.cs` - 负责快捷键配置的存储和读取
- `快捷键选择器.xaml/.cs` - 快捷键选择器控件（备用方案）

## 使用方法

### 在设置页面中配置

1. 打开应用设置页面
2. 找到"全局快捷键"设置卡片
3. 点击快捷键按钮
4. 在弹出的对话框中按下想要设置的快捷键组合
5. 点击确认保存

### 默认快捷键

默认快捷键为：`Ctrl + Alt + Space`

### 快捷键要求

- 必须包含至少一个修饰键（Ctrl、Alt、Shift 或 Win）
- 可以与字母、数字、功能键等组合

## 技术实现

### 全局快捷键注册

使用 Windows API `RegisterHotKey` 注册系统级快捷键：

```csharp
[DllImport("user32.dll", SetLastError = true)]
private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
```

### 窗口消息处理

通过 `SetWindowLongPtr` 拦截窗口消息，监听 `WM_HOTKEY` 消息：

```csharp
const uint WM_HOTKEY = 0x0312;
```

### 设置持久化

使用 `ApplicationData.Current.LocalSettings` 存储快捷键配置。

## 集成到托盘管理器

托盘图标管理器在初始化时会：

1. 创建一个隐藏窗口用于接收快捷键消息
2. 读取用户设置的快捷键配置
3. 注册全局快捷键
4. 监听快捷键设置变化事件，动态更新注册

当快捷键被按下时，会调用 `ShowMainWindow()` 方法，效果等同于点击托盘图标。

## 注意事项

1. 如果快捷键已被其他程序占用，注册会失败
2. 应用关闭时会自动卸载快捷键
3. 修改快捷键设置后会立即生效，无需重启应用
