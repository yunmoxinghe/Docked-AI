# 主窗口入口

此目录包含主窗口的创建和初始化逻辑。

## 文件说明

### 主窗口.xaml / 主窗口.xaml.cs
主窗口的 UI 定义和核心逻辑实现。

### 主窗口工厂.cs
统一管理主窗口的创建逻辑，提供以下功能：

- `CreateAndActivate()` - 创建并激活主窗口
- `Create()` - 创建主窗口但不激活
- `GetOrCreate(Window?)` - 获取或创建主窗口（智能判断现有窗口是否有效）
- `IsWindowValid(Window?)` - 检查窗口是否有效

## 使用示例

```csharp
// 创建并激活主窗口
var window = MainWindowFactory.CreateAndActivate();

// 获取或创建主窗口（如果现有窗口无效则创建新窗口）
_window = MainWindowFactory.GetOrCreate(_window);
_window.Activate();

// 检查窗口是否有效
if (MainWindowFactory.IsWindowValid(existingWindow))
{
    existingWindow.Activate();
}
```

## 设计目的

将主窗口的创建逻辑集中到一个工厂类中，便于：
- 统一管理窗口创建逻辑
- 避免代码重复
- 方便未来扩展（如添加窗口池、预创建等优化）
- 提高代码可维护性
