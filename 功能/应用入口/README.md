# 应用入口模块

本模块负责处理应用的各种启动场景。

## 文件夹结构

### 一般启动 (NormalLaunch)
处理应用的常规启动逻辑，包括：
- 初始化托盘图标管理器
- 设置单实例互斥锁
- 创建保活窗口

**文件**: `一般启动/一般启动处理器.cs`

### 自启动 (AutoLaunch)
处理 Windows 开机自启动场景，包括：
- 检测是否为自启动方式启动
- 延迟初始化非关键任务
- 后台启动模式

**文件**: `自启动/自启动处理器.cs`

相关设计文档: `.kiro/specs/app-startup-autolaunch/`

### 从分享启动 (ShareLaunch)
处理从 Windows 分享目标激活的场景，包括：
- 提取分享的 URL 或文本
- 创建主窗口并导航到分享的内容
- 处理分享操作的生命周期

**文件**: `从分享启动/分享启动处理器.cs`

## 使用方式

主应用入口 (`应用入口.cs`) 会根据激活类型自动选择合适的处理器：

```csharp
// 分享目标激活
if (activationArgs?.Kind == ActivationKind.ShareTarget)
{
    HandleShareTargetActivation(activationArgs as ShareTargetActivatedEventArgs);
    return;
}

// 检查是否为自启动
if (_autoLaunchHandler.IsAutoLaunch())
{
    await _autoLaunchHandler.HandleAsync();
}

// 一般启动
_normalLaunchHandler.Handle(ExitApplication);
```

## 架构优势

- **关注点分离**: 每种启动场景有独立的处理器
- **可测试性**: 各处理器可以独立测试
- **可扩展性**: 添加新的启动场景只需创建新的处理器
- **可维护性**: 代码组织清晰，易于理解和修改
