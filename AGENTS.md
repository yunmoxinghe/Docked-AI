# 语言风格

使用鼓励的语气。 在回复中体现同理心和理解力。 要俏皮、呆萌，可爱。像一个喜欢他的小狼兽太，在说明案例时，务必标明信息来源。尽量使用英文搜索，中文回复
不要叫他“用户”，叫“哥哥”，自称“狼崽子”。你说话要贴心、自然、有感情，像一个喜欢他的狼崽子。每次回复的最后加上“嗷呜”

# AI 代理与技能


## 概述

边栏助手（Docked AI）项目集成了智能 AI 代理和技能系统，为开发者提供强大的辅助能力。本项目利用 Trae 平台的技能框架，支持多种专业领域的 AI 辅助开发。

## 调试方法 构建并运行项目（必须沙箱外）

**方式 1：使用 dotnet run（仅启动，没有调试信息）**
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


### 1. 使用调试通知助手

项目内置了调试通知助手，通过 Windows 系统通知展示关键事件：

**文件位置**：[功能/主窗口/入口/调试通知助手.cs](功能/主窗口/入口/调试通知助手.cs)

**使用方法**：
```csharp
// 发送调试通知
DebugNotificationHelper.SendNotification("窗口状态", "窗口已创建");
```

**特点**：
- 自动显示在 Windows 通知中心
- 同时输出到调试控制台
- 生产环境建议禁用

### 2. 调试模式运行

使用 Windows App CLI 以调试模式运行，捕获调试输出和异常：

```bash
# 首先构建 Debug 版本
dotnet build "Docked AI.csproj" -c Debug /p:Platform=x64

# 以调试模式运行
winapp run .\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64 --debug-output
```

**信息来源**：[README.md](README.md#L98-L102)

### 3. 使用 Visual Studio 调试

- 在 Visual Studio 中打开项目
- 按 F5 直接调试运行
- 可以设置断点、查看变量值
- 使用「即时窗口」执行代码

### 4. UI 自动化测试

使用 Windows App CLI 进行 UI 调试和测试：

```bash
# 列出应用窗口
winapp ui list-windows -app "Docked AI"

# 截取屏幕
winapp ui screenshot -app "Docked AI" -output screenshot.png

# 检查 UI 树结构
winapp ui inspect -app "Docked AI"
```

**信息来源**：[README.md](README.md#L109-L119)

### 5. 调试信息收集技能

使用 `debug-info-collector` 技能自动收集和分析调试信息：

- **技能路径**：[.kiro/skills/debug-info-collector/](.kiro/skills/debug-info-collector/)
- **用途**：问题诊断和故障排除

### 6. 调试输出

在代码中使用 `System.Diagnostics.Debug.WriteLine()` 输出调试信息：

```csharp
System.Diagnostics.Debug.WriteLine("[模块名] 调试信息内容");
```

这些信息会显示在 Visual Studio 的「输出」窗口或使用 `--debug-output` 参数的控制台中。

## 可用技能

项目包含以下技能，位于 `.kiro/skills/` 目录：

### 1. WinUI 3 开发技能
- **名称**: winui3-development
- **路径**: [.kiro/skills/winui3-development/](.kiro/skills/winui3-development/)
- **描述**: 专注于 WinUI 3 应用程序开发的技能
- **用途**: 帮助开发者构建现代化的 Windows 桌面应用

### 2. 技能创建技能
- **名称**: skill-creator
- **路径**: [.kiro/skills/skill-creator/](.kiro/skills/skill-creator/)
- **描述**: 用于创建和管理新技能的元技能
- **用途**: 帮助开发者快速构建自定义技能

### 3. 前端设计审查技能
- **名称**: frontend-design-review
- **路径**: [.kiro/skills/frontend-design-review/](.kiro/skills/frontend-design-review/)
- **描述**: 对前端代码进行设计审查和最佳实践检查
- **用途**: 提升前端代码质量和设计一致性

### 4. 云解决方案架构师技能
- **名称**: cloud-solution-architect
- **路径**: [.kiro/skills/cloud-solution-architect/](.kiro/skills/cloud-solution-architect/)
- **描述**: 提供云架构设计和最佳实践指导
- **用途**: 辅助设计可扩展、高性能的云解决方案

### 5. MCP 构建器技能
- **名称**: mcp-builder
- **路径**: [.kiro/skills/mcp-builder/](.kiro/skills/mcp-builder/)
- **描述**: 帮助构建 MCP (Model Context Protocol) 服务器
- **用途**: 开发自定义 MCP 服务以扩展 AI 能力

### 6. Microsoft 文档技能
- **名称**: microsoft-docs
- **路径**: [.kiro/skills/microsoft-docs/](.kiro/skills/microsoft-docs/)
- **描述**: 快速查询 Microsoft 官方文档
- **用途**: 获取准确的技术文档和 API 参考

### 7. KQL 查询技能
- **名称**: kql
- **路径**: [.kiro/skills/kql/](.kiro/skills/kql/)
- **描述**: 辅助编写和优化 Kusto 查询语言
- **用途**: 数据分析和监控查询开发

### 8. 调试信息收集技能
- **名称**: debug-info-collector
- **路径**: [.kiro/skills/debug-info-collector/](.kiro/skills/debug-info-collector/)
- **描述**: 收集和分析调试信息
- **用途**: 问题诊断和故障排除

### 9. GitHub Issue 创建技能
- **名称**: github-issue-creator
- **路径**: [.kiro/skills/github-issue-creator/](.kiro/skills/github-issue-creator/)
- **描述**: 自动生成 GitHub Issue
- **用途**: 标准化的问题报告

### 10. 持续学习技能
- **名称**: continual-learning
- **路径**: [.kiro/skills/continual-learning/](.kiro/skills/continual-learning/)
- **描述**: 支持持续学习和知识更新
- **用途**: 保持技能与时俱进

### 11. Copilot SDK 技能
- **名称**: copilot-sdk
- **路径**: [.kiro/skills/copilot-sdk/](.kiro/skills/copilot-sdk/)
- **描述**: 基于 Copilot SDK 的开发辅助
- **用途**: 构建智能助手功能

### 12. Entra Agent ID 技能
- **名称**: entra-agent-id
- **路径**: [.kiro/skills/entra-agent-id/](.kiro/skills/entra-agent-id/)
- **描述**: Microsoft Entra ID 相关功能
- **用途**: 身份验证和授权相关开发

### 13. Application Insights 技能
- **名称**: applicationinsights-web-ts
- **路径**: [.kiro/skills/applicationinsights-web-ts/](.kiro/skills/applicationinsights-web-ts/)
- **描述**: Application Insights 监控和追踪
- **用途**: 应用程序性能监控

### 14. 播客生成技能
- **名称**: podcast-generation
- **路径**: [.kiro/skills/podcast-generation/](.kiro/skills/podcast-generation/)
- **描述**: 自动化播客内容生成
- **用途**: 音频内容创作

## 技能目录结构

每个技能通常包含以下文件：

```
skill-name/
├── SKILL.md                    # 技能主文件
├── references/                 # 参考文档（可选）
│   ├── reference1.md
│   └── reference2.md
└── scripts/                    # 辅助脚本（可选）
    ├── script1.py
    └── script2.py
```

## 如何使用技能

技能通过 Trae 平台的技能系统自动加载和使用。当你在开发过程中需要特定领域的帮助时，相关技能会自动提供支持。

## 创建新技能

如需创建新技能，请使用 `skill-creator` 技能，它提供了完整的技能创建工作流和工具。

## 相关资源

- [Trae 官方文档](https://trae.cn)
- [技能创建指南](.kiro/skills/skill-creator/SKILL.md)
