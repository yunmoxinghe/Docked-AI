# 主窗口重构 v2 - 窗口管理架构

本文件夹包含主窗口模块的重构实现，采用清晰的三层架构：服务层、状态机层、表现层。

## 文件夹结构

```
功能/主窗口v2/
├── 服务层/                    # Win32 API 抽象层
│   └── Win32互操作/           # Win32 API P/Invoke 封装
├── 状态机/                    # 窗口状态管理
│   ├── 窗口状态.cs            # WindowState 枚举定义
│   └── 事件/                  # 状态转换事件
├── 动画系统/                  # 统一动画引擎
│   ├── 引擎/                  # 动画引擎核心
│   │   ├── 动画规格.cs        # AnimationSpec、SpringConfig、Easing
│   │   └── 插值器/            # 插值算法实现
│   ├── 视觉状态/              # 窗口视觉状态定义
│   │   └── 窗口视觉状态.cs    # WindowVisualState 类
│   ├── 策略/                  # 全局动画策略
│   └── 预设/                  # 预定义动画配置
├── 窗口形态/                  # 各窗口形态实现
│   └── 窗口形态接口.cs        # IWindowState 接口
├── 初始化/                    # 窗口初始化逻辑
└── 基础设施/                  # 横切关注点
    ├── 日志/                  # 日志系统
    ├── 错误处理/              # 错误恢复机制
    └── 性能/                  # 性能监控
```

## 核心类型

### 1. WindowState 枚举

定义窗口的五种互斥状态：
- **Initializing**: 首次创建，唯一起点，不可转入
- **Hidden**: 隐藏状态
- **Floating**: 浮窗模式
- **Fullscreen**: 全屏模式
- **Sidebar**: 边栏模式

### 2. WindowVisualState 类

窗口视觉状态快照，包含所有可动画的连续量属性：
- **Bounds**: 窗口位置和尺寸
- **CornerRadius**: 圆角半径
- **Opacity**: 不透明度（0.0 - 1.0）
- **IsTopmost**: 是否置顶
- **ExtendedStyle**: Win32 扩展样式

### 3. AnimationSpec 类

动画规格，定义动画参数：
- **Duration**: 动画时长
- **Easing**: 缓动函数（Linear、EaseIn、EaseOut、EaseInOut、EaseInCubic、EaseOutCubic、EaseInOutCubic）
- **BoundsEasing**: 可选，针对位置/尺寸的特定缓动
- **CornerRadiusEasing**: 可选，针对圆角的特定缓动
- **Spring**: 可选，Spring 物理模拟配置

### 4. SpringConfig 类

Spring 物理模拟配置：
- **Stiffness**: 刚度
- **Damping**: 阻尼

### 5. IWindowState 接口

窗口形态接口，定义各窗口形态的行为：
- **GetTargetVisual()**: 获取目标视觉状态
- **GetAnimationSpec(from, to)**: 获取动画规格
- **OnEnter()**: 进入状态时的生命周期钩子
- **OnExit()**: 离开状态时的生命周期钩子

## 设计原则

1. **声明式动画**: 状态只定义"目标样子"，动画系统统一插值
2. **请求压缩**: 快速连续的状态转换请求会被自动压缩到最后一个目标
3. **平滑打断**: 动画被打断时，从当前中间状态继续插值到新目标，无需反向动画
4. **时间驱动**: 使用 Stopwatch 确保动画时长准确，不受帧率波动影响
5. **职责分离**: 服务层、状态机层、表现层严格分离，易于测试和维护

## 下一步

- 实现 WindowService（Win32 抽象层）
- 实现 WindowContext（窗口上下文）
- 实现 AnimationEngine（统一动画引擎）
- 实现 WindowStateManager（状态机）
- 实现各窗口形态（FloatingWindow、FullscreenWindow、SidebarWindow、HiddenWindow）

## 参考文档

- 需求文档: `.kiro/specs/main-window-refactor/requirements.md`
- 设计文档: `.kiro/specs/main-window-refactor/design.md`
- 任务列表: `.kiro/specs/main-window-refactor/tasks.md`
