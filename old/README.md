# 边栏助手 (Docked AI)

[![从 Microsoft 获取](https://get.microsoft.com/images/zh-CN%20dark.svg)](https://apps.microsoft.com/detail/9NX1DZB3WNWP?hl=zh-CN)

一个基于 WinUI 3 的 Windows 桌面应用程序，提供便捷的边栏助手功能。

## 项目简介

边栏助手是一款现代化的 Windows 应用，采用 WinUI 3 框架开发，为用户提供快速访问和管理各种功能的边栏界面。

## 技术栈

- .NET 10.0
- WinUI 3 (Windows App SDK 2.0)
- Windows 10/11 (最低版本 17763)
- DevWinUI 9.9.4
- CommunityToolkit.WinUI.Controls
- Windows App Development CLI 0.3+
- Native AOT 编译支持

## 系统要求

- Windows 10 版本 1809 (Build 17763) 或更高版本
- Windows 11 (推荐)
- 支持平台：x86、x64、ARM64

## 功能特性

- 🎯 边栏快速访问
- 🌐 网页应用集成
- ⚙️ 灵活的设置选项
- 🔔 系统托盘支持
- 🌍 多语言本地化支持
- 📤 Windows 分享目标集成

## 开发环境配置

### 前置要求

- Visual Studio 2022 或更高版本（可选，AOT编译需要）
- .NET 10.0 SDK
- Windows App SDK 2.0
- Windows 10 SDK (10.0.19041.0 或更高)
- Windows App Development CLI 0.3+ (推荐)
- Visual C++ 构建工具（Native AOT 编译需要）

### 构建项目

1. 克隆仓库
```bash
git clone <repository-url>
cd "Docked AI"
```

2. 安装 Windows App Development CLI（推荐）
```bash
# 通过 WinGet 安装
winget install Microsoft.WinAppCli

# 或通过 npm 安装
npm install -g @microsoft/winappcli
```

3. 还原 NuGet 包
```bash
dotnet restore
```

4. 构建并运行项目

**方式 1：使用 dotnet run（推荐）**
```bash
# 一键构建、打包、注册并启动应用
dotnet run
```

**方式 2：使用 Windows App CLI**
```bash
# 构建项目
dotnet build -c Debug /p:Platform=x64

# 运行打包应用
winapp run .\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64
```

**方式 3：使用 Visual Studio**
- 直接按 F5 运行

### 开发工作流

本项目已集成 `Microsoft.Windows.SDK.BuildTools.WinApp` NuGet 包，支持使用 `dotnet run` 直接启动打包应用。

**日常开发**：
```bash
dotnet run  # 自动完成：构建 → 打包 → 注册 → 启动
```

**调试模式**（捕获调试输出和异常）：
```bash
dotnet build "Docked AI.csproj" -c Debug /p:Platform=x64
winapp run .\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64 --debug-output
```

**清理已注册的包**：
```bash
winapp unregister "Docked AI"
```

**UI 自动化测试**：
```bash
# 列出应用窗口
winapp ui list-windows -app "Docked AI"

# 截图
winapp ui screenshot -app "Docked AI" -output screenshot.png

# 检查 UI 树
winapp ui inspect -app "Docked AI"
```

## 项目结构

```
Docked AI/
├── 功能/
│   ├── 应用入口/          # 应用程序入口点
│   ├── 主窗口/            # 主窗口实现
│   ├── 主窗口内容区/      # 主窗口内容区域
│   ├── 页面/              # 各功能页面
│   │   ├── 主页/
│   │   ├── 设置/
│   │   ├── 网页应用/
│   │   └── 新建/
│   ├── 托盘/              # 系统托盘功能
│   └── 本地化/            # 多语言支持
├── Assets/                # 应用资源文件
└── Properties/            # 项目属性

```

## 发布

项目支持多平台发布和 Native AOT 编译：

### 标准发布（Framework-Dependent）

```bash
# 发布 x64 版本（依赖系统 .NET 运行时，体积小）
dotnet publish -c Release -r win-x64

# 发布 x64 版本（依赖系统 .NET 运行时，体积小）
dotnet publish -c Release -r win-x64

# 发布 ARM64 版本
dotnet publish -c Release -r win-arm64
```

### Native AOT 发布（自包含，启动更快）

Native AOT 将应用编译为原生机器码，具有以下优势：
- ⚡ **启动速度更快**（无需 JIT 编译）
- 💾 **内存占用更小**（无 JIT 编译器开销）
- 📦 **自包含**（无需安装 .NET 运行时）

**前置要求**：
- Visual C++ 构建工具（可通过 Visual Studio Installer 安装）
- 足够的磁盘空间（AOT 编译需要更多时间和空间）

**发布命令**：

```bash
# 使用预定义的 AOT 发布配置（推荐）
dotnet publish -p:PublishProfile=win-x64-aot     # x64 平台
dotnet publish -p:PublishProfile=win-arm64-aot   # ARM64 平台

# 或手动指定 AOT 参数
dotnet publish -c Release -r win-x64 /p:PublishAot=true

# 发布 ARM64 版本
dotnet publish -c Release -r win-arm64 /p:PublishAot=true
```

**发布输出路径**：
- 标准发布：`bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\publish\`
- AOT 发布：`bin\publish\win-x64-aot\`（使用配置文件时）

**性能对比**：

| 模式 | 启动时间 | 内存占用 | 体积 | .NET 运行时依赖 |
|------|---------|---------|------|----------------|
| Framework-Dependent | 中等 | 中等 | 小 | ✅ 需要 |
| Native AOT | 快 | 小 | 大 | ❌ 不需要 |

**信息来源**：
- [Native AOT 部署概述](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [WinUI 3 Native AOT 支持](https://github.com/microsoft/WindowsAppSDK/issues/4971)

### MSIX 打包

使用 Windows App CLI 打包为 MSIX：

```bash
# 生成 MSIX 安装包
winapp package .\bin\Release\net10.0-windows10.0.26100.0\win-x64

# 或使用 Visual Studio 的「打包」功能
```

## AOT 兼容性注意事项

本项目已启用 Native AOT 编译，所有代码均已验证 AOT 兼容性：

✅ **已处理的 AOT 问题**：
- JSON 序列化使用源生成器（`WebAppJsonContext`）
- 避免使用反射和动态代码生成
- 所有 NuGet 包均兼容 AOT

⚠️ **添加新代码时的注意事项**：
1. **避免反射**：不要使用 `Type.GetType()`, `Assembly.Load()`, `Activator.CreateInstance()` 等
2. **JSON 序列化**：使用 `WebAppJsonContext` 源生成上下文，而非 `JsonSerializer.Serialize<T>()`
3. **构建时验证**：运行 `dotnet build -c Release` 检查是否有 IL2XXX 或 IL3XXX 警告

**示例（正确的 JSON 序列化）**：

```csharp
// ❌ 错误：不兼容 AOT
var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

// ✅ 正确：使用源生成上下文
var json = JsonSerializer.Serialize(data, WebAppJsonContext.Default.ExportMetadata);
```

**信息来源**：
- [如何使库兼容 Native AOT](https://devblogs.microsoft.com/dotnet/creating-aot-compatible-libraries/)
- [AOT 警告参考](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/warnings/il3050)



# 发布 ARM64 版本
dotnet publish -c Release -r win-arm64
```

### 使用 Windows App CLI 打包

```bash
# 生成 MSIX 包
winapp package .\bin\Release\net10.0-windows10.0.19041.0\win-x64

# 添加命令行别名（可通过名称启动应用）
winapp manifest add-alias
```

## 贡献

欢迎提交 Issue 和 Pull Request！

## 相关资源

- [Windows App Development CLI 文档](https://github.com/microsoft/winappCli)
- [WinUI 3 文档](https://learn.microsoft.com/windows/apps/winui/winui3/)
- [Windows App SDK 文档](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
- [DevWinUI 文档](https://github.com/ghost1372/DevWinUI)

## 本地化

本应用支持多语言界面，包括简体中文、繁體中文、English、日本語、한국어、Français、Deutsch 和 Español。

如需了解如何添加或修改本地化资源，请查看 [本地化指南](功能/本地化/README.md)。

## 许可证

本项目采用 MIT 许可证。详见 [LICENSE](LICENSE) 文件。

## 作者

云漠星

---

⭐ 如果这个项目对你有帮助，欢迎给个 Star！
