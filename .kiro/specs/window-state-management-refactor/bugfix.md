# Bugfix Requirements Document

## Introduction

当前主窗口的状态管理系统存在架构性缺陷，导致状态定义不清晰、转换逻辑分散、难以维护和扩展。系统仅使用两个布尔值（`IsWindowVisible` 和 `IsDockPinned`）来表示复杂的窗口状态，无法准确描述窗口的实际状态（未创建、隐藏中、窗口化、最大化、已固定），导致状态转换逻辑散落在多个方法中，缺少统一的状态定义和转换规则。

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN 窗口处于不同的显示模式时 THEN 系统只能通过 `IsWindowVisible` 和 `IsDockPinned` 两个布尔值的组合来推断状态，无法直接识别窗口是处于"窗口化"、"最大化"还是"隐藏中"状态

1.2 WHEN 需要执行状态转换时（如从最大化切换到固定模式）THEN 系统的转换逻辑分散在 `WindowHostController` 的多个方法中（`ToggleWindow`、`TogglePinnedDock`、`ShowWindow`、`HideWindow`、`ShowPinnedDock`、`RestoreStandardDock` 等），缺少统一的状态转换验证和约束

1.3 WHEN 窗口状态发生变化时 THEN 系统无法追踪完整的状态转换历史，难以调试状态转换问题和验证状态转换的合法性

1.4 WHEN 布局信息（`WindowLayoutState`）和 UI 状态（`MainWindowViewModel`）需要同步时 THEN 系统需要在两个分离的类之间手动协调，容易导致状态不一致

1.5 WHEN 需要判断某个操作是否允许执行时（如在隐藏状态下不允许最大化）THEN 系统缺少明确的状态转换规则和约束，导致可能出现非法的状态转换

### Expected Behavior (Correct)

2.1 WHEN 窗口处于不同的显示模式时 THEN 系统 SHALL 使用明确的枚举类型（如 `WindowState` 枚举）来表示五种状态：未创建（NotCreated）、隐藏中（Hidden）、窗口化（Windowed）、最大化（Maximized）、已固定（Pinned）

2.2 WHEN 需要执行状态转换时 THEN 系统 SHALL 通过统一的状态管理器（State Manager）来处理所有状态转换，并在转换前验证转换的合法性（如定义状态转换矩阵）

2.3 WHEN 窗口状态发生变化时 THEN 系统 SHALL 触发状态变化事件，允许订阅者追踪状态转换历史，并提供调试和日志记录功能

2.4 WHEN 布局信息和 UI 状态需要同步时 THEN 系统 SHALL 将布局信息整合到统一的状态对象中，确保状态的一致性和原子性

2.5 WHEN 需要判断某个操作是否允许执行时 THEN 系统 SHALL 提供明确的状态转换规则（如状态机或转换矩阵），在尝试非法转换时返回错误或拒绝操作

### Unchanged Behavior (Regression Prevention)

3.1 WHEN 用户点击托盘图标切换窗口显示/隐藏时 THEN 系统 SHALL CONTINUE TO 正确执行窗口的显示和隐藏动画

3.2 WHEN 用户点击固定按钮切换固定模式时 THEN 系统 SHALL CONTINUE TO 正确注册/注销 AppBar 并应用相应的窗口样式

3.3 WHEN 用户最大化或还原窗口时 THEN 系统 SHALL CONTINUE TO 正确更新窗口的 Presenter 状态和 UI 图标

3.4 WHEN 窗口失去焦点且未固定时 THEN 系统 SHALL CONTINUE TO 自动隐藏窗口

3.5 WHEN 窗口大小发生变化时 THEN 系统 SHALL CONTINUE TO 正确更新 `WindowLayoutState` 中的尺寸信息

3.6 WHEN 窗口在固定模式下时 THEN 系统 SHALL CONTINUE TO 正确处理 AppBar 消息和边界计算

3.7 WHEN 窗口关闭时 THEN 系统 SHALL CONTINUE TO 正确清理 AppBar 注册和其他资源
