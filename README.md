# 边栏助手 (Docked AI)

一个基于 WinUI 3 的 Windows 桌面应用程序，提供便捷的边栏助手功能。

## 项目简介

边栏助手是一款现代化的 Windows 应用，采用 WinUI 3 框架开发，为用户提供快速访问和管理各种功能的边栏界面。

## 技术栈

- .NET 8.0
- WinUI 3 (Windows App SDK 1.8)
- Windows 10/11 (最低版本 17763)
- DevWinUI 9.9.3
- CommunityToolkit.WinUI.Controls

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

- Visual Studio 2022 或更高版本
- .NET 8.0 SDK
- Windows App SDK 1.8
- Windows 10 SDK (10.0.19041.0 或更高)

### 构建项目

1. 克隆仓库
```bash
git clone <repository-url>
cd "Docked AI"
```

2. 还原 NuGet 包
```bash
dotnet restore
```

3. 构建项目
```bash
dotnet build
```

4. 运行应用
```bash
dotnet run
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

## 贡献

欢迎提交 Issue 和 Pull Request！

## 本地化

本应用支持多语言界面，包括简体中文、繁體中文、English、日本語、한국어、Français、Deutsch 和 Español。

如需了解如何添加或修改本地化资源，请查看 [本地化指南](功能/本地化/README.md)。

## 许可证

本项目采用 MIT 许可证。详见 [LICENSE](LICENSE) 文件。

## 作者

云漠星

---

⭐ 如果这个项目对你有帮助，欢迎给个 Star！
