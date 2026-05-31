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

项目支持多平台发布：

```bash
# 发布 x64 版本
dotnet publish -c Release -r win-x64

# 发布 x86 版本
dotnet publish -c Release -r win-x86

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
