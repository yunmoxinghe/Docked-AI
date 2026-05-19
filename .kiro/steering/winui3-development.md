---
inclusion: always
---

# WinUI 3 开发指南

这是一个基于 Windows App SDK 2.0 和 .NET 10.0 构建的 WinUI 3 桌面应用程序。在此代码库中工作时请遵循以下约定。

## 技术栈

**核心框架：**
- Windows App SDK 2.0 (WinUI 3)
- .NET 10.0 目标框架
- C# 支持 Native AOT 编译

**UI 库：**
- DevWinUI 9.9.4（主要 UI 组件库）
- CommunityToolkit.WinUI.Controls（社区工具包控件）

**构建工具：**
- Windows App Development CLI (WinAppCLI) 用于构建/运行/打包/调试操作
- Microsoft.Windows.SDK.BuildTools.WinApp NuGet 包

## 构建和运行命令

**始终使用以下命令进行构建和运行：**

```bash
# 日常开发（推荐）- 自动完成构建、打包、注册和启动
dotnet run

# 调试模式（详细输出）
dotnet build "Docked AI.csproj" -c Debug /p:Platform=x64
winapp run .\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64 --debug-output

# 清理已注册的包
winapp unregister "Docked AI"

# 构建特定配置
dotnet build -c Debug /p:Platform=x64
dotnet build -c Release /p:Platform=x64

# 发布特定平台
dotnet publish -c Release -r win-x64
dotnet publish -c Release -r win-x86
dotnet publish -c Release -r win-arm64

# 打包为 MSIX
winapp package .\bin\Release\net10.0-windows10.0.19041.0\win-x64
```

## 项目结构约定

**必须遵循以下目录组织：**
- `功能/` - 所有功能模块（使用中文目录名，符合项目约定）
- `功能/页面/` - 页面相关代码
- `功能/本地化/` - 本地化资源
- `Assets/` - 所有资源文件（图像、图标等）

**每个功能模块必须：**
- 在 `功能/` 下有自己的专用文件夹
- 将相关页面保存在 `页面/` 子文件夹中
- 将本地化资源存储在 `本地化/` 子文件夹中

## 代码风格和架构规则

**线程处理：**
- 所有 UI 操作必须在 UI 线程上执行
- 使用 `DispatcherQueue` 进行线程调度
- 永远不要在 UI 线程上执行阻塞操作

**API 使用：**
- 避免使用已弃用的 UWP API
- 优先使用 Windows App SDK API 而非旧版 UWP 等效 API
- 对异步操作使用现代 async/await 模式

**Native AOT 兼容性：**
- 避免基于反射的代码模式
- 避免动态代码生成
- 确保所有代码都兼容 AOT（无运行时代码生成）
- 尽可能使用源生成器代替反射

**本地化：**
- 所有用户可见的文本必须使用本地化资源
- 支持的语言：简体中文、繁體中文、English、日本語、한국어、Français、Deutsch、Español
- 将本地化文件存储在 `功能/本地化/` 目录中

## 平台支持

**目标平台：** x86、x64、ARM64
**最低操作系统：** Windows 10 版本 1809 (Build 17763)
**推荐操作系统：** Windows 11

构建时必须明确指定平台：`/p:Platform=x64`

## 调试和测试

**调试问题时：**
1. 使用 `winapp run --debug-output` 捕获详细日志和异常
2. 使用 `winapp ui inspect -app "Docked AI"` 检查 UI 元素树
3. 使用 `winapp ui list-windows -app "Docked AI"` 列出应用程序窗口
4. 使用 `winapp ui screenshot -app "Docked AI" -output screenshot.png` 进行视觉验证

**运行前：**
- 确保已还原依赖项：`dotnet restore`
- 验证项目构建无错误
- 检查是否已安装所需的 NuGet 包

## 关键约束

**必须做：**
- 开发期间使用 `dotnet run` 进行快速迭代
- 调查崩溃或错误时使用 `winapp run --debug-output`
- 使用 `DispatcherQueue` 将 UI 代码保持在 UI 线程上
- 编写 AOT 兼容代码（不使用反射）
- 本地化所有面向用户的字符串

**禁止做：**
- 使用已有 Windows App SDK 替代品的 UWP API
- 使用反射或动态代码生成（会破坏 AOT）
- 在 UI 线程上执行阻塞操作
- 硬编码用户可见文本（必须使用本地化）
- 构建时忘记指定 `/p:Platform=x64`

## 参考文档

- [Windows App Development CLI](https://github.com/microsoft/winappCli)
- [WinUI 3 文档](https://learn.microsoft.com/zh-cn/windows/apps/winui/winui3/)
- [Windows App SDK 文档](https://learn.microsoft.com/zh-cn/windows/apps/windows-app-sdk/)
- [DevWinUI 文档](https://github.com/ghost1372/DevWinUI)
