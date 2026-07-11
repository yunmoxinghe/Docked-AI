---
inclusion: always
---

# WinUI 3 开发指南

这是一个基于 Windows App SDK 2.0 和 .NET 10.0 构建的 WinUI 3 桌面应用程序。在此代码库中工作时请遵循以下约定。

## ⚠️ 强制要求（必须遵守）

**在任务完全完成之前：**
1. 不删除已有代码
2. 不重构已经工作的部分
3. 不回滚已有修改
4. 如果遇到错误，优先修复当前实现
5. 只有我明确允许时才能大规模重写

你不能宣布任务完成。
只有满足所有验收条件后才允许结束。
如果失败，请继续分析和修改。
**在回答任何问题之前，必须执行以下步骤：**

1. **查询当前时间**
   - 使用系统提供的当前日期和时间信息
   - 确保时间上下文准确（用于判断技术是否过时）

2. **联网搜索最新信息**
   - 根据问题上下文，使用 `remote_web_search` 工具搜索最新技术文档
   - 优先搜索官方文档（Microsoft Learn、GitHub 官方仓库）
   - 验证 API、包版本、最佳实践是否有更新

3. **新增代码强制使用 Reactor 框架**
   - ❌ **禁止**：新增代码使用 XAML 编写 UI
   - ✅ **必须**：所有新增 UI 代码使用 Microsoft.UI.Reactor 声明式语法
   - ✅ **必须**：使用 `UseState`、`UseEffect` 等 Hooks 管理状态
   - ✅ **必须**：使用 `VStack`、`HStack`、`Grid` 等布局组件
   - ⚠️ **例外**：仅在修改现有 XAML 代码时可继续使用 XAML

4. **🚫 文档管理严格限制**
   - ❌ **绝对禁止**：在 `D:\fluentapps\repos\yunmoxinghe\Docked-AI\文档` 目录之外创建任何文档
   - ❌ **绝对禁止**：随意新增文档文件（除非用户明确要求）
   - ✅ **仅允许**：修改 `文档/` 目录下已有的文档文件
   - ✅ **仅允许**：在用户明确授权后，在 `文档/` 目录内创建新文档
   - ⚠️ **违规后果**：任何违反此规则的文档创建行为将被视为严重错误

**Reactor 代码模板（新增功能必须遵循）：**

```csharp
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

// 新功能组件示例
class NewFeatureComponent : Component
{
    public override Element Render()
    {
        var (state, setState) = UseState("initial");

        return VStack(16,
            TextBlock($"当前状态: {state}").FontSize(20).Bold(),
            Button("更新状态", () => setState("已更新"))
        ).Padding(24);
    }
}
```

**搜索关键词参考：**
- 涉及 WinUI 3 API → 搜索 "WinUI 3 [API名称] latest documentation"
- 涉及 Reactor → 搜索 "Microsoft.UI.Reactor [功能] getting started"
- 涉及 Windows App SDK → 搜索 "Windows App SDK [版本] release notes"
- 涉及 .NET 10 → 搜索 ".NET 10 [功能] best practices"

**执行流程：**
```
用户提问 
  ↓
1. 检查当前时间（判断技术时效性）
  ↓
2. 联网搜索相关最新信息
  ↓
3. 分析问题（是否需要新增代码？）
  ↓
4. 如需新增代码 → 使用 Reactor 框架
  ↓
5. 回答问题（引用搜索结果和官方文档）
```

## 技术栈

**核心框架：**
- Windows App SDK 2.0 (WinUI 3)
- .NET 10.0 目标框架
- C# 支持 Native AOT 编译

**UI 框架（混合模式）：**
- **Microsoft.UI.Reactor**（🔥 新增代码强制使用）
  - 版本：0.1.0-preview.4（公开预览版）
  - 声明式 UI，纯 C# 无 XAML
  - Hooks 模式状态管理
- **传统 WinUI 3 + XAML**（仅用于维护现有代码）
  - DevWinUI 9.9.4（现有组件库）
  - CommunityToolkit.WinUI.Controls（现有工具包）

**构建工具：**
- Windows App Development CLI (WinAppCLI) 0.3+ 用于构建/运行/打包/调试操作
- Microsoft.Windows.SDK.BuildTools.WinApp NuGet 包
- Reactor CLI (`mur`) 用于 Reactor 项目管理

## 构建和运行命令
**在构建之前：**
1. 检查


**日常开发工作流（推荐）：**

```bash
# 不要使用 快速开发：自动构建、打包、注册并启动应用（使用 WinApp CLI 集成）
dotnet run

# 应该使用调试模式：捕获详细输出、异常堆栈和 WinUI 诊断信息
dotnet run -c Debug /p:Platform=x64 --debug-output

# 热重载开发：保存文件后自动重新编译和启动（需要手动重启应用）
dotnet watch run
```

**包管理命令：**

```bash
# 清理已注册的应用包（解决包注册冲突）
winapp unregister "Docked AI"

# 列出已安装的包（验证是否注册成功）
winapp list
```

**构建命令：**

```bash
# 构建 Debug 版本（含调试符号）
dotnet build -c Debug /p:Platform=x64

# 构建 Release 版本（优化编译）
dotnet build -c Release /p:Platform=x64

# 发布特定平台（Framework-Dependent 模式，体积最小）
dotnet publish -c Release -r win-x64
dotnet publish -c Release -r win-arm64
```

**打包为 MSIX（应用商店分发）：**

```bash
# 生成 MSIX 安装包
winapp package .\bin\Release\net10.0-windows10.0.19041.0\win-x64

# 或使用 Visual Studio 的「打包」功能（右键项目 → Package and Publish）
```

**UI 自动化和调试工具：**

```bash
# 检查 UI 元素树（用于自动化测试和调试）
winapp ui inspect -app "Docked AI"

# 列出应用窗口（验证窗口状态）
winapp ui list-windows -app "Docked AI"

# 截取应用屏幕截图
winapp ui screenshot -app "Docked AI" -output screenshot.png
```

**信息来源：**
- [Windows App CLI 文档](https://learn.microsoft.com/windows/apps/dev-tools/winapp-cli/usage)
- [WinApp CLI v0.3 发布公告](https://devblogs.microsoft.com/ifdef-windows/windows-app-development-cli-v0-3-new-run-and-ui-commands-plus-dotnet-run-support-for-packaged-apps/)

## 项目结构约定

**目录组织（现有结构）：**
- `功能/` - 所有功能模块（使用中文目录名，符合项目约定）
- `功能/页面/` - 页面相关代码（现有 XAML 页面）
- `功能/本地化/` - 本地化资源
- `Assets/` - 所有资源文件（图像、图标等）

**新增功能目录结构（使用 Reactor）：**
- `功能/[新功能名称]/` - 新功能模块根目录
- `功能/[新功能名称]/Components/` - Reactor 组件（`.cs` 文件）
- `功能/[新功能名称]/Models/` - 数据模型和业务逻辑
- `功能/[新功能名称]/Services/` - 服务层（API 调用、数据访问）
- `功能/[新功能名称]/本地化/` - 本地化资源

**示例目录结构：**
```
功能/
├── 主窗口/                    # 现有功能（XAML）
│   ├── 页面/
│   │   └── MainWindow.xaml
│   └── 入口/
│       └── 调试通知助手.cs
├── AI对话/                    # 🆕 新功能（Reactor）
│   ├── Components/
│   │   ├── ChatWindow.cs      # 主聊天窗口组件
│   │   ├── MessageList.cs     # 消息列表组件
│   │   └── InputBox.cs        # 输入框组件
│   ├── Models/
│   │   └── ChatMessage.cs
│   ├── Services/
│   │   └── AIService.cs
│   └── 本地化/
│       └── Resources.resw
```

**文件命名约定：**
- Reactor 组件文件：使用 `.cs` 扩展名，类名与文件名一致
- 组件类必须继承 `Component` 或 `Component<TProps>`
- 每个组件文件仅包含一个主要组件类

## 代码风格和架构规则

### Reactor 组件开发规范（新增代码）

**组件结构：**
```csharp
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

// 简单组件（无 Props）
class MyComponent : Component
{
    public override Element Render()
    {
        var (state, setState) = UseState("initial");
        
        return VStack(16,
            TextBlock($"State: {state}").FontSize(20),
            Button("Update", () => setState("updated"))
        ).Padding(24);
    }
}

// 带 Props 的组件
record MyComponentProps(string Title, Action<string> OnChange);

class MyComponentWithProps : Component<MyComponentProps>
{
    public override Element Render()
    {
        return VStack(12,
            TextBlock(Props.Title).Bold(),
            Button("Click", () => Props.OnChange("clicked"))
        );
    }
}
```

**Hooks 使用规范：**
- `UseState` - 组件内部状态管理
- `UseReducer` - 复杂状态更新（基于前值计算新值）
- `UseEffect` - 副作用处理（API 调用、订阅、定时器）
- `UseMemo` - 缓存昂贵计算结果
- `UseRef` - 存储不触发重渲染的值

**布局规范：**
- `VStack(spacing, children)` - 垂直堆叠
- `HStack(spacing, children)` - 水平堆叠
- `Grid` - 行列网格布局
- `ScrollView` - 可滚动容器
- `Border` - 带边框和背景的容器

**状态提升原则：**
- 仅被一个组件使用的状态 → 放在该组件内
- 需要在兄弟组件间共享的状态 → 提升到父组件
- 全局状态 → 使用 Context（`UseContext`）

### 传统 WinUI 3 + XAML 规范（仅维护现有代码）

**线程处理：**
- 所有 UI 操作必须在 UI 线程上执行
- 使用 `DispatcherQueue` 进行线程调度
- 永远不要在 UI 线程上执行阻塞操作

**API 使用：**
- 避免使用已弃用的 UWP API
- 优先使用 Windows App SDK API 而非旧版 UWP 等效 API
- 对异步操作使用现代 async/await 模式

### 通用规范（Reactor 和 XAML 共同遵守）

**Native AOT 兼容性：**
- 避免基于反射的代码模式
- 避免动态代码生成
- 确保所有代码都兼容 AOT（无运行时代码生成）
- 尽可能使用源生成器代替反射

**本地化：**
- 所有用户可见的文本必须使用本地化资源
- 支持的语言：简体中文、繁體中文、English、日本語、한국어、Français、Deutsch、Español
- 将本地化文件存储在 `功能/本地化/` 目录中
- Reactor 组件使用 `UseLocalization` Hook 访问本地化字符串

## 平台支持

**目标平台：** x64、ARM64
**最低操作系统：** Windows 10 版本 1809 (Build 17763)
**推荐操作系统：** Windows 11

**注意：** 不再支持 x86 (32位) 平台。

构建时必须明确指定平台：`/p:Platform=x64`

## 调试和测试

**调试问题时：**
1. 使用 `dotnet run --debug-output` 捕获详细日志和异常
2. 使用 `winapp ui inspect -app "Docked AI"` 检查 UI 元素树
3. 使用 `winapp ui list-windows -app "Docked AI"` 列出应用程序窗口
4. 使用 `winapp ui screenshot -app "Docked AI" -output screenshot.png` 进行视觉验证
5. Reactor 组件调试：
   - 使用 `dotnet watch run` 启用热重载（保存即更新）
   - 按 `Ctrl+Shift+D` 打开 Reactor Dev Tools（Debug 模式）
   - 使用 `mur doctor` 验证 Reactor 开发环境配置

**运行前：**
- 确保已还原依赖项：`dotnet restore`
- 如使用 Reactor：确保已安装 `Microsoft.UI.Reactor` 包（0.1.0-preview.4 或更高）
- 验证项目运行无错误
- 检查是否已安装所需的 NuGet 包

## 关键约束

**必须做（新增代码 - Reactor）：**
- ✅ **强制使用 Reactor 框架**编写所有新增 UI 代码
- ✅ 使用 `UseState`/`UseReducer` 管理组件状态
- ✅ 使用 `VStack`/`HStack`/`Grid` 进行布局
- ✅ 组件类继承 `Component` 或 `Component<TProps>`
- ✅ 使用 `dotnet watch run` 开发以获得热重载体验
- ✅ 为需要复用的 UI 块创建独立组件
- ✅ 使用 Record 类型定义组件 Props

**必须做（维护现有代码 - XAML）：**
- ✅ 使用 `DispatcherQueue` 将 UI 代码保持在 UI 线程上
- ✅ 编写 AOT 兼容代码（不使用反射）
- ✅ 本地化所有面向用户的字符串
- ✅ 调查崩溃或错误时使用 `dotnet run --debug-output`

**必须做（通用）：**
- ✅ **回答前先联网搜索**最新技术信息和文档
- ✅ **回答前检查当前时间**，确保技术建议不过时
- ✅ 引用搜索结果时提供来源链接
- ✅ 开发期间使用 `dotnet run` 进行快速迭代
- ✅ 运行时指定 `/p:Platform=x64` 或 `/p:Platform=ARM64`

**必须做（文档管理）：**
- ✅ **仅修改** `D:\fluentapps\repos\yunmoxinghe\Docked-AI\文档` 目录内的现有文档
- ✅ **获得明确授权后**才能在 `文档/` 目录创建新文档
- ✅ 更新文档时保持结构一致性和格式规范
- ✅ 在文档末尾标注最后更新日期

**禁止做：**
- ❌ **禁止新增代码使用 XAML**（除非明确要求修改现有 XAML 文件）
- ❌ **禁止在 `文档/` 目录外创建任何文档文件**（包括项目根目录、功能目录等）
- ❌ **禁止随意新增文档**（必须先征得用户同意）
- ❌ 使用已有 Windows App SDK 替代品的 UWP API
- ❌ 使用反射或动态代码生成（会破坏 AOT）
- ❌ 在 UI 线程上执行阻塞操作
- ❌ 硬编码用户可见文本（必须使用本地化）
- ❌ 在 Reactor 组件中使用 `INotifyPropertyChanged`（使用 Hooks 代替）
- ❌ 在回答问题前跳过联网搜索步骤
- ❌ 提供未验证时效性的过时技术建议

## 关于 Microsoft.UI.Reactor（强制用于新增代码）

**Microsoft.UI.Reactor** 是一个声明式 UI 框架，用纯 C# 构建 WinUI 3 应用，无需 XAML：

- **声明式语法**：使用类似 React 的 `Render()` 方法描述 UI
- **Hooks 模式**：通过 `UseState`、`UseEffect` 等管理状态和副作用
- **热重载支持**：`dotnet watch` 可在不重启应用的情况下更新 UI
- **无 XAML**：完全使用 C# fluent API 构建界面
- **与 WinUI 3 互操作**：可在同一应用中混用 XAML 和 Reactor

**项目当前状态：**
- ⚠️ **混合模式**：现有代码使用 XAML，新增代码强制使用 Reactor
- 🔄 **逐步迁移**：通过新增功能逐步引入 Reactor
- ✅ **互操作性**：Reactor 组件可嵌入 XAML 窗口，XAML 控件可在 Reactor 组件中使用

**安装 Reactor（如尚未安装）：**

```bash
# 1. 克隆 Reactor 仓库（一次性操作）
git clone https://github.com/microsoft/microsoft-ui-reactor.git
cd microsoft-ui-reactor
./bootstrap.ps1

# 2. 在项目中添加 Reactor 包引用
dotnet add package Microsoft.UI.Reactor --version 0.1.0-preview.4

# 3. 验证安装
mur doctor
```

**Reactor 最小示例（新功能模板）：**

```csharp
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

// 新功能入口点（可在 XAML 窗口中托管）
class NewFeatureComponent : Component
{
    public override Element Render()
    {
        var (name, setName) = UseState("World");

        return VStack(16,
            TextBlock($"Hello, {name}!").FontSize(24).Bold(),
            TextBox(name, setName, placeholderText: "Enter your name").Width(250)
        ).Padding(24);
    }
}

// 在 XAML 窗口中托管 Reactor 组件
// MainWindow.xaml.cs
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        
        // 将 Reactor 组件挂载到 XAML 容器
        var host = new ReactorHostControl();
        host.Mount<NewFeatureComponent>();
        RootGrid.Children.Add(host); // RootGrid 是 XAML 中定义的 Grid
    }
}
```

**Reactor 完整功能示例（待办事项列表）：**

```csharp
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

record TodoItem(string Text, bool Done);

class TodoListComponent : Component
{
    public override Element Render()
    {
        var (items, updateItems) = UseReducer(new List<TodoItem>
        {
            new("学习 Reactor 基础", true),
            new("构建第一个 Reactor 组件", false),
        });
        var (newText, setNewText) = UseState("");

        var doneCount = items.Count(i => i.Done);

        return VStack(16,
            TextBlock("待办事项").FontSize(24).Bold(),
            TextBlock($"{doneCount}/{items.Count} 已完成").Opacity(0.6),

            // 输入框
            HStack(8,
                TextBox(newText, setNewText, placeholderText: "添加新任务...")
                    .Width(300),
                Button("添加", () =>
                {
                    if (!string.IsNullOrWhiteSpace(newText))
                    {
                        updateItems(list => [.. list, new TodoItem(newText.Trim(), false)]);
                        setNewText("");
                    }
                }).IsEnabled(!string.IsNullOrWhiteSpace(newText))
            ),

            // 任务列表
            VStack(4,
                items.Select((item, index) =>
                    HStack(8,
                        CheckBox(item.Done, done =>
                            updateItems(list =>
                            {
                                var copy = new List<TodoItem>(list);
                                copy[index] = item with { Done = done };
                                return copy;
                            }),
                            label: item.Text
                        ),
                        Button("删除", () =>
                            updateItems(list =>
                            {
                                var copy = new List<TodoItem>(list);
                                copy.RemoveAt(index);
                                return copy;
                            })
                        )
                    ).WithKey($"todo-{index}")
                ).ToArray()
            ),

            // 清除已完成
            When(doneCount > 0, () =>
                Button($"清除已完成 ({doneCount})", () =>
                    updateItems(list => list.Where(i => !i.Done).ToList())
                )
            )
        ).Padding(24);
    }
}
```

**开发工作流（Reactor 热重载）：**

```bash
# 启动热重载开发服务器
dotnet watch run

# 编辑组件代码并保存 → 自动更新 UI（无需重启应用）
# 状态在热重载中保持（UseState 数据不丢失）
```

**何时使用 Reactor vs XAML：**

| 场景 | 使用框架 | 原因 |
|------|---------|------|
| 🆕 新增功能 | **Reactor**（强制） | 开发效率高，热重载，类型安全 |
| 🔧 修改现有 XAML | **XAML** | 保持一致性，避免混合风格 |
| 📊 数据驱动 UI | **Reactor** | Hooks 更适合管理复杂状态 |
| 🎨 设计器可视化 | **XAML** | Visual Studio 设计器支持 |
| 🔥 快速原型 | **Reactor** | 无需 XAML 编译，迭代更快 |

**信息来源：**
- [Reactor 官方文档](https://microsoft.github.io/microsoft-ui-reactor/)
- [Reactor GitHub 仓库](https://github.com/microsoft/microsoft-ui-reactor)
- [Reactor 快速开始指南](https://microsoft.github.io/microsoft-ui-reactor/getting-started/)

---

## 参考文档

**核心框架：**
- [Windows App Development CLI](https://github.com/microsoft/winappCli)
- [WinUI 3 文档](https://learn.microsoft.com/zh-cn/windows/apps/winui/winui3/)
- [Windows App SDK 文档](https://learn.microsoft.com/zh-cn/windows/apps/windows-app-sdk/)
- [DevWinUI 文档](https://github.com/ghost1372/DevWinUI)

**可选框架（声明式 UI）：**
- [Microsoft.UI.Reactor 文档](https://microsoft.github.io/microsoft-ui-reactor/)
- [Reactor 快速开始](https://microsoft.github.io/microsoft-ui-reactor/getting-started/)
