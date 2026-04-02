# 实现计划：应用开机自启动

## 概述

本实现计划将 Windows 商店应用（UWP/MSIX）开机自启动功能分解为离散的编码任务。该功能允许用户配置应用在 Windows 启动时自动运行，遵循 Windows 平台的安全和隐私要求。

## 任务

- [x] 1. 配置应用清单以支持启动任务
  - 在 Package.appxmanifest 中添加 StartupTask 扩展声明
  - 设置 TaskId 为 "AppStartupTask"
  - 配置 Arguments 为 "--autolaunch" 用于启动检测
  - 设置 Enabled 为 "false"（用户必须显式启用）
  - 添加 DisplayName 用于系统设置显示
  - 声明 "startupTask" 受限能力
  - _需求: 1.1, 1.2, 1.3, 1.4_

- [ ] 2. 实现 StartupTaskManager 类
  - [x] 2.1 创建 StartupTaskManager 类及核心方法
    - 在项目结构的适当位置创建类文件
    - 定义 TaskId 常量为 "AppStartupTask"
    - 实现 SemaphoreSlim 用于操作锁定
    - 实现 GetStateAsync() 方法查询当前状态
    - 实现 RequestEnableAsync() 方法并使用信号量保护
    - 实现 DisableAsync() 方法并使用信号量保护
    - 实现 CanModifyState() 方法检查状态是否可修改
    - _需求: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3_

  - [x] 2.2 为 StartupTaskManager 编写单元测试
    - 测试 GetStateAsync 返回正确状态
    - 测试 RequestEnableAsync 正确处理并发调用
    - 测试 DisableAsync 正确处理并发调用
    - 测试 CanModifyState 为每个状态返回正确值
    - 测试信号量防止竞态条件
    - _需求: 2.5, 4.4, 5.3_

- [ ] 3. 实现 AutoLaunchHandler 用于启动检测
  - [x] 3.1 在 AutoLaunchHandler 中实现 IsAutoLaunch() 方法
    - 使用 AppInstance.GetActivatedEventArgs() 获取激活参数
    - 检查参数是否包含 "--autolaunch" 参数
    - 如果检测到自启动参数返回 true，否则返回 false
    - 添加异常处理和日志记录
    - _需求: 6.2_

  - [x] 3.2 在 AutoLaunchHandler 中实现 HandleAsync() 方法
    - 创建 InitializeCoreServicesAsync() 用于最小化初始化
    - 实现 PerformBackgroundTasksAsync() 用于后台操作
    - 添加自启动事件的日志记录
    - 确保不激活主窗口（不调用 window.Activate()）
    - 添加异常处理以防止阻塞系统启动
    - _需求: 6.1, 6.3, 7.1, 7.2, 7.3, 10.1, 10.2, 10.3, 10.4_

  - [x] 3.3 为 AutoLaunchHandler 编写单元测试
    - 测试 IsAutoLaunch 正确识别自启动场景
    - 测试 IsAutoLaunch 对正常启动返回 false
    - 测试 HandleAsync 完成时不显示窗口
    - 测试异常处理不抛出异常
    - _需求: 6.2, 7.1, 7.2_

- [ ] 4. 将自启动检测集成到 App.OnLaunched
  - [x] 4.1 更新 App.OnLaunched 以检测和处理自启动
    - 初始化 AutoLaunchHandler 实例
    - 调用 IsAutoLaunch() 检测自启动场景
    - 如果检测到自启动，异步调用 HandleAsync()
    - 确保始终调用 NormalLaunchHandler.Handle() 以初始化托盘图标
    - 保持现有的单实例检查逻辑
    - 保持现有的分享目标激活处理
    - _需求: 6.1, 6.2, 6.4_

  - [x] 4.2 为 App.OnLaunched 编写集成测试
    - 测试自启动路径正确初始化
    - 测试正常启动路径仍然正常工作
    - 测试两种场景下托盘图标都被初始化
    - _需求: 6.1, 6.4_

- [ ] 5. 检查点 - 确保核心自启动逻辑正常工作
  - 验证清单配置正确
  - 手动测试 StartupTaskManager 方法
  - 使用 "--autolaunch" 参数测试自启动检测
  - 确保所有测试通过，如有问题请询问用户

- [x] 6. 实现 StartupSettingsViewModel
  - [x] 6.1 创建 StartupSettingsViewModel 类及属性
    - 实现 INotifyPropertyChanged 接口
    - 添加 StartupTaskManager 依赖
    - 定义 _currentState 和 _isOperationInProgress 字段
    - 实现 IsStartupEnabled 属性
    - 实现 CanToggle 属性（检查状态和操作标志）
    - 实现 CanNavigateToSettings 属性
    - 实现 ShowPolicyWarning 属性
    - 实现 ShowUserDisabledInfo 属性
    - _需求: 3.1, 3.2, 8.2, 8.5, 9.1, 9.2_

  - [x] 6.2 实现 InitializeAsync 方法
    - 调用 StartupTaskManager.GetStateAsync()
    - 存储当前状态
    - 调用 UpdateUIProperties() 刷新所有属性
    - 添加异常处理和日志记录
    - _需求: 3.1, 3.2, 3.3_

  - [x] 6.3 实现 HandleToggleAsync 方法
    - 检查 _isOperationInProgress 标志，如果为 true 则返回
    - 设置 _isOperationInProgress 为 true
    - 根据切换状态调用 RequestEnableAsync() 或 DisableAsync()
    - 使用结果更新 _currentState
    - 调用 UpdateUIProperties()
    - 在 finally 块中重置 _isOperationInProgress
    - _需求: 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 8.3, 8.4_

  - [x] 6.4 实现 NavigateToSystemSettingsAsync 方法
    - 使用 Windows.System.Launcher 打开系统设置
    - 导航到 "ms-settings:startupapps" URI
    - 添加异常处理和日志记录
    - _需求: 8.1, 8.5_

  - [x] 6.5 实现 UpdateUIProperties 辅助方法
    - 为 IsStartupEnabled 触发 PropertyChanged
    - 为 CanToggle 触发 PropertyChanged
    - 为 CanNavigateToSettings 触发 PropertyChanged
    - 为 ShowPolicyWarning 触发 PropertyChanged
    - 为 ShowUserDisabledInfo 触发 PropertyChanged
    - _需求: 8.2, 8.4_

  - [x] 6.6 为 StartupSettingsViewModel 编写单元测试
    - 测试 InitializeAsync 正确加载状态
    - 测试 HandleToggleAsync 防止并发操作
    - 测试 HandleToggleAsync 更新 UI 属性
    - 测试 NavigateToSystemSettingsAsync 打开正确的 URI
    - 测试每个 StartupTaskState 的属性值
    - _需求: 3.3, 4.4, 5.3, 8.4_

- [x] 7. 创建设置页面 UI
  - [x] 7.1 添加自启动切换的 SettingCard
    - 添加 SettingCard，Header 为 "开机自启动"
    - 添加 Description 解释该功能
    - 添加 FontIcon，glyph 为 "&#xE7E8;"
    - 将 IsClickEnabled 绑定到 ViewModel.CanNavigateToSettings
    - 添加 Click 事件处理程序用于导航
    - 在 SettingCard 内添加 ToggleSwitch
    - 将 ToggleSwitch.IsOn 绑定到 ViewModel.IsStartupEnabled（双向）
    - 将 ToggleSwitch.IsEnabled 绑定到 ViewModel.CanToggle
    - 添加 Toggled 事件处理程序
    - _需求: 8.1, 8.2, 8.3_

  - [x] 7.2 为 DisabledByUser 状态添加 InfoBar
    - 添加 InfoBar，Severity="Informational"
    - 将 IsOpen 绑定到 ViewModel.ShowUserDisabledInfo
    - 设置 Title 为 "需要在系统设置中启用"
    - 设置 Message 解释用户需要在系统设置中启用
    - 设置 IsClosable 为 False
    - _需求: 8.5_

  - [x] 7.3 为 DisabledByPolicy 状态添加 InfoBar
    - 添加 InfoBar，Severity="Warning"
    - 将 IsOpen 绑定到 ViewModel.ShowPolicyWarning
    - 设置 Title 为 "自启动功能已被组策略限制"
    - 设置 Message 解释管理员限制
    - 设置 IsClosable 为 False
    - _需求: 9.1, 9.2_

  - [x] 7.4 实现代码后置事件处理程序
    - 实现 OnToggleSwitched 调用 ViewModel.HandleToggleAsync
    - 实现 OnSettingCardClick 调用 ViewModel.NavigateToSystemSettingsAsync
    - 添加异常处理和用户反馈
    - _需求: 8.3, 8.4_

- [ ] 8. 实现错误处理和日志记录
  - [ ] 8.1 在 StartupTaskManager 中添加异常处理
    - 捕获 UnauthorizedAccessException 并记录上下文日志
    - 捕获 COMException 并记录错误代码
    - 捕获 InvalidOperationException 并刷新状态
    - 捕获 TaskCanceledException 并允许重试
    - 捕获一般异常并记录完整堆栈跟踪
    - _需求: 7.1, 7.2_

  - [ ] 8.2 在 AutoLaunchHandler 中添加异常处理
    - 在 try-catch 中包装 HandleAsync
    - 记录异常但不重新抛出
    - 确保系统启动不被阻塞
    - _需求: 7.1, 7.2, 7.3_

  - [ ] 8.3 为错误添加用户反馈
    - 为权限错误显示 ContentDialog 或 InfoBar
    - 为瞬态错误显示重试选项
    - 为 DisabledByUser 状态显示清晰指导
    - 为 DisabledByPolicy 状态显示管理员联系信息
    - _需求: 4.4, 5.3, 8.5, 9.2_

- [x] 9. 实现性能优化
  - [x] 9.1 为 InitializeCoreServicesAsync 添加超时控制
    - 创建 CancellationTokenSource，超时时间为 5 秒
    - 使用超时包装初始化任务
    - 捕获 OperationCanceledException 并记录警告
    - 超时时继续部分初始化
    - _需求: 10.1, 10.3_

  - [x] 9.2 优化自启动期间的资源使用
    - 延迟执行非关键初始化任务
    - 自启动期间避免加载 UI 资源
    - 所有初始化使用异步操作
    - 监控内存使用保持在正常启动的 120% 以内
    - _需求: 10.1, 10.2, 10.4_

- [ ] 10. 最终集成和测试
  - [ ] 10.1 连接所有组件
    - 确保 ViewModel 在设置页面中正确实例化
    - 验证数据绑定正常工作
    - 测试 UI 和系统之间的状态同步
    - 测试导航到系统设置
    - _需求: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [ ] 10.2 测试所有用户场景
    - 测试首次启用并获得用户授权
    - 测试首次启用但用户拒绝
    - 测试已授权时启用
    - 测试禁用功能
    - 测试 DisabledByUser 状态和导航
    - 测试 DisabledByPolicy 状态显示
    - 测试实际的系统启动自启动
    - _需求: 2.1, 2.2, 2.3, 2.4, 2.5, 4.1, 4.2, 4.3, 5.1, 5.2, 6.1, 6.2, 6.3, 6.4, 9.1, 9.2_

  - [ ] 10.3 为端到端流程编写集成测试
    - 测试从 UI 到系统的完整启用流程
    - 测试从 UI 到系统的完整禁用流程
    - 测试外部更改后的状态同步
    - 测试错误恢复场景
    - _需求: 2.5, 4.4, 5.3, 7.1, 7.2, 7.3_

- [ ] 11. 最终检查点 - 确保所有测试通过
  - 运行所有单元测试和集成测试
  - 验证已部署包中的清单配置
  - 在干净的 Windows 安装上测试
  - 使用组策略限制进行测试
  - 确保所有测试通过，如有问题请询问用户

## 注意事项

- 标记为 `*` 的任务是可选的，可以跳过以加快 MVP 开发
- 每个任务都引用了特定需求以便追溯
- 实现遵循现有的应用架构和启动处理器模式
- 信号量和操作标志防止快速切换导致的竞态条件
- 错误处理确保系统启动永远不会被阻塞
- 性能优化确保对系统启动时间的影响最小
