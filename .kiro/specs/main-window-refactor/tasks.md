# 实施计划：主窗口重构

## 概述

本实施计划将主窗口模块从紧密耦合的架构重构为清晰的三层设计：服务层（Win32 抽象）、状态机层（状态管理）和表现层（窗口形态）。重构引入了声明式动画系统，具有统一的视觉状态插值，取代了命令式动画逻辑。

关键架构改进：
- 具有请求压缩和平滑动画中断的声明式状态机
- 支持 LERP 和弹簧物理的统一动画引擎
- 用于集中窗口引用管理的 WindowContext
- 用于可插拔窗口行为的 IWindowState 接口
- 用于一致用户体验的全局动画策略

## 任务

- [x] 1. 设置项目结构和核心类型
  - 创建文件夹结构：服务层/、状态机/、动画系统/、窗口形态/、初始化/、基础设施/
  - 定义 WindowState 枚举（Initializing、Hidden、Floating、Fullscreen、Sidebar）
  - 定义 WindowVisualState 类，包含 Bounds、CornerRadius、Opacity、IsTopmost、ExtendedStyle
  - 定义 AnimationSpec 类，包含 Duration、Easing、Spring 配置
  - 定义 IWindowState 接口，包含 GetTargetVisual、GetAnimationSpec、OnEnter、OnExit 方法
  - _需求：1.1、4.1、4.2、5.12_

- [x] 2. 实现 WindowService（Win32 抽象层）
  - [x] 2.1 创建 WindowService 静态类和 Win32 互操作文件
    - 创建 服务层/窗口服务.cs 作为静态类
    - 创建 Win32互操作/ 文件夹，包含 DWM互操作.cs、窗口样式互操作.cs、窗口位置互操作.cs、显示器管理器.cs、焦点管理互操作.cs
    - 实现 Win32 API 的 P/Invoke 声明
    - _需求：6.1、6.2、6.3、6.4、6.5、6.6、6.7、6.8、6.9_
  
  - [x] 2.2 实现窗口样式和外观方法
    - 实现 RemoveTitleBar、SetTransparentBackground、SetExtendedStyle
    - 实现 SetTopmost、ShowWindow、HideWindow
    - 实现 SetDwmAttribute 用于圆角和亚克力效果
    - _需求：6.2、6.3、6.4、6.5、6.6、6.8_
  
  - [x] 2.3 实现窗口定位和大小调整方法
    - 实现 MoveWindow、ResizeWindow
    - 实现 EnableResize、DisableResize
    - 实现 GetWindowBounds、GetCurrentScreen
    - _需求：6.7、6.9、7.1、7.6_
  
  - [x] 2.4 实现焦点管理方法
    - 实现 SetForegroundWindow、BringWindowToTop
    - 实现 FlashWindowEx，包含 FLASHW_* 常量
    - 实现 GetForegroundWindow、GetWindowThreadProcessId
    - 实现 TryBringToFront，采用多层策略（Activate → SetForegroundWindow → FlashWindowEx）
    - _需求：6.10、6.11、6.12、6.13、5.18、5.19、5.20_
  
  - [x] 2.5 实现 AppBar 管理方法
    - 使用 SHAppBarMessage(ABM_NEW, ABM_SETPOS) 实现 RegisterAppBar
    - 使用 SHAppBarMessage(ABM_REMOVE) 实现 UnregisterAppBar
    - 定义 AppBarEdge 枚举（Left、Top、Right、Bottom）
    - _需求：14.1、14.2、14.3_

- [x] 3. 实现 WindowContext
  - 创建 服务层/窗口上下文.cs
  - 实现接受 Window 实例的构造函数
  - 实现 GetWindow、GetHwnd（带延迟初始化和缓存）
  - 实现 GetCurrentVisual 以读取当前窗口视觉状态
  - 实现 UpdateCurrentVisual 供动画引擎更新缓存
  - _需求：6.1（上下文管理）_

- [x] 4. 实现 AnimationEngine
  - [x] 4.1 创建 AnimationEngine 核心结构
    - 创建 动画系统/引擎/动画引擎.cs
    - 实现 Animate 方法，包含 from、to、spec、onProgress、cancellationToken 参数
    - 使用 Stopwatch 实现基于时间的动画循环（非基于帧）
    - 实现 Lerp 方法用于 WindowVisualState 插值
    - _需求：3.1、3.4、3.6、3.7、16.1、16.2、16.3、16.4_
  
  - [x] 4.2 实现基于缓动的动画
    - 实现 AnimateWithEasing 方法
    - 支持 linear、EaseIn、EaseOut、EaseInOut 缓动函数
    - 基于经过时间（非帧数）计算进度
    - 使用插值后的视觉状态调用 onProgress 回调
    - _需求：3.1、3.4、3.5、3.7、16.1、16.2、16.3_
  
  - [x] 4.3 实现弹簧物理动画
    - 实现 AnimateWithSpring 方法
    - 基于 Stiffness 和 Damping 计算弹簧力和阻尼力
    - 使用物理模拟更新速度和位移
    - 检测稳定条件（速度和位移低于阈值）
    - 确保最终状态精确到达目标
    - _需求：3.5、9.2_

- [x] 5. 实现 WindowStateManager（状态机）
  - [x] 5.1 创建 WindowStateManager 核心结构
    - 创建 状态机/窗口状态管理器.cs
    - 定义字段：CurrentState、_lastStableState、TransitioningTo、_currentVisual、_latestTarget、_isRunning、_currentCts
    - 实现接受 DispatcherQueue、AnimationEngine、WindowContext、IAnimationPolicy 的构造函数
    - 将 CurrentState 初始化为 Initializing，_lastStableState 初始化为 Initializing
    - _需求：1.2、8.3、8.4、18.1、18.2_
  
  - [x] 5.2 实现 TransitionTo 方法
    - 检查线程访问，如果不在 UI 线程则使用 DispatcherQueue.TryEnqueue
    - 将 _latestTarget 更新为目标状态
    - 使用 Interlocked.Exchange 原子替换 _currentCts
    - 取消并释放旧的 CancellationTokenSource
    - 如果尚未运行，启动 RunStateMachineLoop
    - 立即返回，不等待动画
    - _需求：2.1、2.6、2.7、11.1、11.2_
  
  - [x] 5.3 实现 RunStateMachineLoop
    - 将 _isRunning 设置为 true
    - 当 _latestTarget != CurrentState 时循环
    - 设置 TransitioningTo 并触发 TransitionStarted 事件
    - 调用目标状态的 OnEnter 钩子
    - 获取目标视觉和动画规格（如果可用则使用 IAnimationPolicy，否则使用状态的 GetAnimationSpec）
    - 在动画前快照 _currentCts
    - 调用 AnimationEngine.Animate，onProgress 更新 _currentVisual 并应用到窗口
    - 处理 OperationCanceledException（动画中断）
    - 成功动画后调用旧状态的 OnExit 钩子
    - 更新 _lastStableState、CurrentState，清除 TransitioningTo
    - 触发 StateChanged 事件
    - 循环退出时将 _isRunning 设置为 false
    - _需求：2.2、2.3、2.5、8.1、8.2、8.6、26.1、26.2、26.3、30.4、30.5_
  
  - [x] 5.4 实现状态转换验证
    - 创建 ValidateTransition 方法检查合法转换
    - 从 Initializing：允许 Floating、Fullscreen、Sidebar、Hidden
    - 从 Hidden：允许 Floating、Fullscreen、Sidebar
    - 从 Floating/Fullscreen/Sidebar：允许任何其他状态
    - 禁止从任何状态转换到 Initializing
    - 对无效转换抛出异常
    - _需求：1.1、1.3、1.4、1.5、1.6_
  
  - [x] 5.5 实现事件定义
    - 定义 StateChanged 事件，包含（WindowState from、WindowState to）参数
    - 定义 TransitionStarted 事件，包含（WindowState from、WindowState to）参数
    - 定义 TransitionFailed 事件，包含（WindowState from、WindowState to、Exception ex）参数
    - _需求：8.1、8.2、22.4、22.5、27.1_
  
  - [x] 5.6 实现 ApplyVisualToWindow 方法
    - 接受 WindowVisualState 参数
    - 从 WindowContext 获取 HWND
    - 调用 WindowService 方法应用 Bounds、CornerRadius、Opacity、IsTopmost、ExtendedStyle
    - 更新 WindowContext 的当前视觉缓存
    - _需求：3.7、4.3_

- [x] 6. 检查点 - 确保核心基础设施正常工作
  - 确保所有测试通过，如有问题请询问用户。

- [x] 7. 实现窗口状态类
  - [x] 7.1 实现 FloatingWindow
    - 创建 窗口形态/浮窗.cs 实现 IWindowState
    - 在构造函数中注入 WindowContext 和 IWindowPositionService
    - 实现 GetTargetVisual：恢复上次位置/大小或使用默认值（宽度为工作区宽度的 1/3，高度为工作区高度减去边距，停靠工作区右侧）
    - 基于保存的距屏幕边缘的右/下距离计算位置
    - 设置 CornerRadius=12、Opacity=1.0、IsTopmost=true、ExtendedStyle=0（不使用工具窗口样式）
    - 实现 GetAnimationSpec：根据距离调整持续时间（<100px 则 150ms，否则 300ms）
    - 实现 OnEnter：设置 IsVisible=true、IsHitTestVisible=true、EnableResize，调用 TryBringToFront，设置 IsShownInSwitchers=false
    - 实现 OnExit：保存位置/大小/边缘距离，DisableResize
    - _需求：5.1、5.2、5.3、5.4、5.5、5.6、5.7、5.18、5.19、5.20、7.1、7.2、7.3、7.4、7.5、8.13、8.14_
  
  - [x] 7.2 实现 FullscreenWindow
    - 创建 窗口形态/全屏窗口.cs 实现 IWindowState
    - 在构造函数中注入 WindowContext
    - 实现 GetTargetVisual：获取当前屏幕边界，设置 CornerRadius=0、Opacity=1.0、IsTopmost=false
    - 实现 GetAnimationSpec：如果大小比率 >2.0 则使用 Spring，否则使用 EaseInOutCubic
    - 实现 OnEnter：设置 IsVisible=true、IsHitTestVisible=true、DisableResize，调用 TryBringToFront
    - 实现 OnExit：（可选清理）
    - _需求：5.8、5.18、5.19、5.20、12.1、12.2_
  
  - [x] 7.3 实现 SidebarWindow
    - 创建 窗口形态/边栏窗口.cs 实现 IWindowState
    - 在构造函数中注入 WindowContext
    - 实现 GetTargetVisual：定位在屏幕右边缘（宽度=400，高度=屏幕高度），CornerRadius=0、Opacity=1.0
    - 实现 GetAnimationSpec：250ms EaseOutCubic
    - 实现 OnEnter：设置 IsVisible=true、IsHitTestVisible=true、DisableResize，RegisterAppBar(Right, 400)
    - 实现 OnExit：UnregisterAppBar
    - _需求：5.9、12.3、14.1、14.2、14.3_
  
  - [x] 7.4 实现 HiddenWindow
    - 创建 窗口形态/隐藏窗口.cs 实现 IWindowState
    - 在构造函数中注入 WindowContext
    - 实现 GetTargetVisual：保持当前 Bounds/CornerRadius，设置 Opacity=0.0
    - 实现 GetAnimationSpec：200ms EaseOutCubic
    - 实现 OnEnter：（空 - 保持 IsVisible=true 以允许淡出动画）
    - 实现 OnExit：设置 IsVisible=false、IsHitTestVisible=false
    - _需求：5.10、5.11、14.1、14.2、14.3、29.3、29.4_
  
  - [x] 7.5 实现 InitializingWindow（占位符）
    - 创建 窗口形态/初始化窗口.cs 实现 IWindowState
    - 实现 GetTargetVisual：返回当前视觉状态（无变化）
    - 实现 GetAnimationSpec：返回即时动画（Duration=0）
    - 实现 OnEnter/OnExit：（空）
    - _需求：1.2_

- [x] 8. 实现 IWindowPositionService
  - 创建 服务层/窗口位置服务.cs 接口和实现
  - 实现 SaveFloatingPosition(width, height, rightDistance, bottomDistance)
  - 实现 GetLastFloatingPosition() 返回保存的值或 null
  - 使用持久化存储（例如应用程序设置或 JSON 文件）
  - _需求：7.7、7.8、5.7_

- [x] 9. 实现 IAnimationPolicy
  - [x] 9.1 创建 IAnimationPolicy 接口
    - 创建 动画系统/策略/动画策略接口.cs
    - 定义 Resolve 方法，接受 fromState、toState、currentVisual
    - 返回 AnimationSpec 或 null
    - _需求：10.1、10.2、10.3、10.4、28.1、28.2、28.3、28.5_
  
  - [x] 9.2 实现 DefaultAnimationPolicy
    - 创建 动画系统/策略/默认动画策略.cs
    - 实现防抖：如果距上次转换时间 <100ms，使用快速动画（120ms linear）
    - 定义 TransitionType 枚举（EnterFullscreen、ExitFullscreen、DockToSidebar、Float、Hide、Show、Default）
    - 实现 GetTransitionType 将（from、to）对映射到 TransitionType
    - 正确处理 Initializing 状态转换
    - 为每个 TransitionType 返回适当的 AnimationSpec
    - _需求：10.6、10.7、10.8、10.9、10.10、10.11、28.6、28.7_

- [x] 10. 实现窗口初始化流程
  - [x] 10.1 更新 MainWindowFactory
    - 确保 Create() 方法创建窗口但不调用 Activate()
    - 确保 CreateAndActivate() 方法创建并激活窗口
    - 确保 GetOrCreate() 检查窗口有效性，如果无效则创建新窗口
    - 确保 IsWindowValid() 检查 HWND 和 Content
    - _需求：8.8、8.9、8.10、8.11_
  
  - [x] 10.2 更新 MainWindow 构造函数
    - 在 Activate() 之前创建 WindowContext
    - 创建 AnimationEngine 和 DefaultAnimationPolicy
    - 使用所有依赖项创建 WindowStateManager
    - 订阅 StateChanged、TransitionStarted、TransitionFailed 事件
    - 调用 Activate() 显示窗口（状态变为 Initializing）
    - 使用 DispatcherQueue.TryEnqueue 在第一帧后转换到目标状态
    - _需求：8.1、8.2、8.3、8.4、8.5、8.6、8.7_
  
  - [x] 10.3 处理启动隐藏场景
    - 如果应用需要启动时隐藏，在 Activate() 之前设置 window.Opacity=0
    - Activate() 后，转换到 Hidden 状态
    - 确保 HiddenWindow.GetTargetVisual 在从 Initializing 转换时返回 Opacity=0
    - _需求：8.4、8.5、8.6、29.1、29.2、29.3、29.4、29.5、29.6_

- [x] 11. 检查点 - 确保窗口初始化和状态转换正常工作
  - 确保所有测试通过，如有问题请询问用户。

- [x] 12. 实现错误处理和恢复
  - [x] 12.1 在 RunStateMachineLoop 中添加异常处理
    - 在 try-catch 中包装动画执行
    - 捕获非 OperationCanceledException 异常
    - 记录异常详情（类型、消息、堆栈跟踪）
    - 触发 TransitionFailed 事件，包含 from、to、exception
    - 将 CurrentState 恢复到 _lastStableState
    - _需求：22.1、22.2、22.4、22.5、27.1、27.2、27.3_
  
  - [x] 12.2 实现重试逻辑
    - 向 WindowStateManager 添加 _failureCount 字段
    - 在 TransitionFailed 时递增 _failureCount
    - 最多重试 3 次，重试之间延迟 100ms
    - 成功转换时将 _failureCount 重置为 0
    - 如果 3 次重试失败，恢复到 _lastStableState
    - _需求：22.6、22.7、27.6、27.7、27.8_
  
  - [x] 12.3 实现关键故障保护
    - 添加 _consecutiveFailures 字段
    - 每次失败转换时递增，成功时重置
    - 如果 _consecutiveFailures > 3，禁用自动转换并触发 CriticalFailure 事件
    - 实现 ResetToSafeState 方法手动重置到 Floating
    - _需求：22.8、22.11、27.9、27.10_
  
  - [x] 12.4 在 WindowService 中添加错误处理
    - 在 try-catch 中包装 Win32 API 调用
    - 返回错误代码而不是抛出异常
    - 记录失败原因及详细上下文
    - _需求：22.3_
  
  - [x] 12.5 为监视器信息失败添加回退
    - 在 GetCurrentScreen 中捕获异常
    - 返回主监视器边界作为回退
    - 记录关于回退使用的警告
    - _需求：22.9_
  
  - [x] 12.6 为动画引擎失败添加回退
    - 在 WindowStateManager 中捕获 AnimationEngine 初始化失败
    - 回退到无动画模式（即时状态转换）
    - 记录关于降级模式的警告
    - _需求：22.10_
  
  - [x] 12.7 在动画期间添加 HWND 验证
    - 在 ApplyVisualToWindow 中检查 HWND 是否有效
    - 如果无效，抛出异常以停止动画
    - 记录带上下文的错误
    - _需求：22.14_
  
  - [x] 12.8 确保失败时的资源清理
    - 在 RunStateMachineLoop finally 块中释放 CancellationTokenSource
    - 清除 _currentCts 引用
    - 确保异常路径上没有资源泄漏
    - _需求：22.13_

- [x] 13. 实现 UI 集成
  - [~] 13.1 更新全屏按钮处理程序
    - 在按钮点击处理程序中检查当前状态
    - 如果是 Fullscreen，调用 TransitionTo(Floating)
    - 否则，调用 TransitionTo(Fullscreen)
    - _需求：23.1_
  
  - [~] 13.2 更新边栏按钮处理程序
    - 在按钮点击处理程序中检查当前状态
    - 如果是 Sidebar，调用 TransitionTo(Floating)
    - 否则，调用 TransitionTo(Sidebar)
    - _需求：23.2_
  
  - [~] 13.3 更新托盘图标处理程序
    - 在托盘图标点击处理程序中检查当前状态
    - 如果是 Hidden，恢复到上次可见状态（隐藏前保存）
    - 否则，调用 TransitionTo(Hidden) 并保存当前状态
    - _需求：23.3、23.9_
  
  - [~] 13.4 更新键盘快捷键处理程序
    - 将现有快捷键映射到 TransitionTo 调用
    - 确保 F11 切换全屏，Ctrl+Shift+S 切换边栏等
    - _需求：23.4、23.5_
  
  - [~] 13.5 添加转换状态 UI 反馈
    - 订阅 TransitionStarted 事件
    - 当 TransitioningTo != null 时显示加载指示器或禁用按钮
    - 订阅 StateChanged 事件
    - 转换完成时隐藏加载指示器并更新按钮状态
    - _需求：21.1、21.6、23.6_
  
  - [~] 13.6 更新按钮视觉状态
    - 订阅 StateChanged 事件
    - 根据 CurrentState 更新按钮文本/图标（例如，在 Fullscreen 时显示"退出全屏"）
    - 根据 TransitioningTo 更新按钮启用状态
    - _需求：23.7_
  
  - [~] 13.7 添加工具提示更新
    - 更新工具提示以反映当前状态和可用操作
    - 当 TransitioningTo != null 时显示"请稍候，正在切换窗口模式"
    - _需求：21.5_
  
  - [~] 13.8 添加转换通知消息
    - 当用户在进行中的转换期间尝试触发转换时显示简短通知
    - 使用非侵入式 UI（例如 toast 通知）
    - _需求：21.4_

- [x] 14. 实现生命周期钩子行为
  - [x] 14.1 确保 OnEnter 幂等性
    - 审查所有 IWindowState 实现
    - 确保 OnEnter 可以安全地多次调用
    - 使用标志或检查防止重复资源注册
    - _需求：5.16、5.17、26.3、26.4、26.5_
  
  - [x] 14.2 记录中断时不调用 OnExit
    - 添加代码注释说明动画取消时不调用 OnExit
    - 确保 OnEnter 处理先前 OnEnter 资源的清理
    - _需求：26.1、26.2、26.6_

- [ ] 15. 添加日志和遥测
  - [~] 15.1 添加状态转换日志
    - 记录 TransitionStarted，包含 from 和 to 状态
    - 记录 StateChanged，包含 from 和 to 状态
    - 记录 TransitionFailed，包含 from、to 和异常详情
    - _需求：18.1、18.2、18.3_
  
  - [~] 15.2 添加动画性能日志
    - 记录动画开始时间、结束时间和实际持续时间
    - 记录动画期间的帧率
    - 记录动画是否被中断
    - _需求：18.3_
  
  - [~] 15.3 添加资源管理日志
    - 记录 CancellationTokenSource 的创建和释放
    - 记录旧 CTS 是否未正确释放
    - _需求：11.1、11.3_

- [ ] 16. 最终集成和测试
  - [~] 16.1 集成测试：快速状态切换
    - 快速触发 Floating → Fullscreen → Sidebar → Floating
    - 验证最终状态是 Floating
    - 验证没有中间状态变为稳定
    - 验证 StateChanged 事件显示正确的 from/to 状态
    - _需求：2.2、2.3、8.2_
  
  - [~] 16.2 集成测试：动画中断
    - 开始从 Hidden 到 Floating 的转换
    - 用到 Fullscreen 的转换中断
    - 验证动画从中间视觉状态继续
    - 验证没有视觉跳跃
    - _需求：3.2、3.3、15.1、15.2、15.3_
  
  - [~] 16.3 集成测试：启动场景
    - 测试正常启动（Initializing → Floating）
    - 测试启动到全屏（Initializing → Fullscreen）
    - 测试启动隐藏（Opacity=0，Initializing → Hidden）
    - 验证任何场景都没有闪烁
    - _需求：8.1、8.2、8.3、8.4、8.5、8.6、29.1、29.2、29.3、29.4、29.5_
  
  - [~] 16.4 集成测试：错误恢复
    - 模拟 Win32 API 失败
    - 验证触发 TransitionFailed 事件
    - 验证状态恢复到上次稳定状态
    - 验证重试逻辑工作
    - _需求：22.1、22.2、22.3、22.4、22.6、22.7_
  
  - [~] 16.5 集成测试：UI 控件
    - 点击全屏按钮，验证转换到 Fullscreen
    - 点击边栏按钮，验证转换到 Sidebar
    - 点击托盘图标，验证在 Hidden 和上次可见状态之间切换
    - 验证转换期间按钮被禁用
    - 验证转换后按钮状态更新
    - _需求：23.1、23.2、23.3、23.6、23.7、23.9_
  
  - [~] 16.6 集成测试：焦点管理
    - 从 Hidden 转换到 Floating
    - 验证窗口尝试获得焦点
    - 如果焦点失败，验证窗口闪烁
    - 在不同 Windows 版本上测试
    - _需求：5.18、5.19、5.20、6.10、6.11、6.12、6.13_
  
  - [~] 16.7 集成测试：多显示器支持
    - 在副显示器上测试全屏
    - 在副显示器上测试边栏
    - 测试跨显示器的浮窗位置恢复
    - _需求：12.1、12.2、12.3_
  
  - [~] 16.8 集成测试：AppBar 行为
    - 转换到 Sidebar
    - 验证其他窗口不与边栏重叠
    - 验证工作区已调整
    - 从 Sidebar 转换离开
    - 验证工作区已恢复
    - _需求：14.1、14.2、14.3_

- [~] 17. 文档和清理
  - 更新代码注释以反映新架构
  - 为公共 API 添加 XML 文档
  - 更新开发者文档，包含迁移指南
  - 删除过时的代码和注释
  - 验证所有需求已实现

- [~] 18. 最终检查点 - 确保所有测试通过
  - 确保所有测试通过，如有问题请询问用户。

## 注意事项

- 此重构使用 C# 和 WinUI3 框架
- 设计使用具有请求压缩的声明式状态机，而非传统锁定
- 动画中断通过从当前视觉状态继续处理，而非反转
- 所有 Win32 API 调用通过 WindowService 静态方法抽象
- WindowContext 集中管理窗口实例和 HWND
- IAnimationPolicy 提供全局动画一致性
- 焦点管理使用多层策略（Activate → SetForegroundWindow → FlashWindowEx）
- 错误处理包括重试逻辑和回退到安全状态
- UI 控件基于 StateChanged 和 TransitionStarted 事件更新
- 测试涵盖快速状态切换、动画中断、错误恢复和 UI 集成
