# EventPipe 遥测调试指南

## 📋 目录
- [什么是 EventPipe](#什么是-eventpipe)
- [启用 EventPipe 支持](#启用-eventpipe-支持)
- [安装诊断工具](#安装诊断工具)
- [收集性能跟踪](#收集性能跟踪)
- [分析跟踪数据](#分析跟踪数据)
- [常见场景](#常见场景)
- [Reactor 专属性能分析](#reactor-专属性能分析)

---

## 什么是 EventPipe

**EventPipe** 是 .NET 的跨平台诊断机制，用于收集运行时事件数据：

- ✅ **跨平台**：支持 Windows、Linux、macOS
- ✅ **轻量级**：低开销，可在生产环境使用
- ✅ **Native AOT 兼容**：支持 AOT 编译的应用
- ✅ **实时收集**：无需重启应用

**可收集的事件类型：**
- 🔥 CPU 使用情况（方法调用栈）
- 🗑️ 垃圾回收（GC）活动
- 📦 内存分配
- 🧵 线程活动
- ⚠️ 异常抛出
- 🔄 JIT 编译（非 AOT 模式）
- 📊 自定义 EventSource 事件

**信息来源：**
- [EventPipe Overview - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventpipe)
- [Native AOT Diagnostics - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/diagnostics)

---

## 启用 EventPipe 支持

### ✅ 已启用（项目配置）

本项目已在 `DockedTools.csproj` 中启用 EventPipe：

```xml
<!-- Debug 模式 -->
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <EventSourceSupport>true</EventSourceSupport>
</PropertyGroup>

<!-- Release 模式（含 Native AOT） -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <PublishAot>true</PublishAot>
  <EventSourceSupport>true</EventSourceSupport>
</PropertyGroup>
```

### 📦 体积影响

启用 EventPipe 会增加约 **300-500 KB** 的二进制大小，但换来完整的诊断能力。

---

## 安装诊断工具

### 1. 安装 dotnet-trace（推荐）

```bash
# 安装全局工具
dotnet tool install --global dotnet-trace

# 验证安装
dotnet-trace --version
```

### 2. 安装 PerfView（Windows 专用，可选）

从 [GitHub Releases](https://github.com/microsoft/perfview/releases) 下载最新版本。

---

## 收集性能跟踪

### 方式 1：使用 dotnet-trace（跨平台）

#### **场景 1：收集 CPU 性能分析**

```bash
# 1. 启动应用
dotnet run -c Release

# 2. 在另一个终端，获取进程 ID
dotnet-trace ps

# 3. 开始收集 CPU 采样（持续 60 秒）
dotnet-trace collect --process-id <PID> --profile cpu-sampling --duration 00:01:00

# 生成的文件：trace.nettrace
```

#### **场景 2：收集 GC 和内存分配**

```bash
dotnet-trace collect --process-id <PID> --profile gc-collect --duration 00:01:00
```

#### **场景 3：收集自定义事件**

```bash
# 收集 Reactor 框架事件（如果使用 Reactor）
dotnet-trace collect --process-id <PID> \
  --providers Microsoft-UI-Reactor:0xFFFFFFFF:5 \
  --duration 00:02:00
```

#### **场景 4：一站式诊断（CPU + GC + 异常）**

```bash
dotnet-trace collect --process-id <PID> \
  --providers Microsoft-Windows-DotNETRuntime:0x1F000080018:5 \
  --duration 00:02:00
```

**常用 Provider 关键字：**
- `0x1` - GC 事件
- `0x8000` - 方法调用事件
- `0x10000` - 类型加载事件
- `0x80000000` - 异常事件
- `0x1F000080018` - 完整运行时事件

**信息来源：**
- [dotnet-trace documentation - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)

### 方式 2：使用 PerfView（Windows）

```bash
# 1. 以管理员身份运行 PerfView.exe
# 2. Collect -> Run
# 3. 输入应用启动命令：dotnet run -c Release
# 4. 点击「Run Command」并进行操作
# 5. 点击「Stop Collection」
```

---

## 分析跟踪数据

### 方式 1：使用 speedscope.app（推荐，可视化）

1. 打开 [https://www.speedscope.app/](https://www.speedscope.app/)
2. 拖放 `trace.nettrace` 文件到浏览器
3. 查看火焰图（Flame Graph）

**火焰图说明：**
- 横轴：时间或采样百分比
- 纵轴：调用栈深度
- 宽度：函数消耗的时间
- 颜色：自动分配，方便区分

### 方式 2：使用 Visual Studio Profiler

1. 打开 Visual Studio
2. 菜单：`Analyze` → `Performance Profiler` → `Open File`
3. 选择 `trace.nettrace`
4. 查看 CPU Usage、GC 等视图

### 方式 3：使用 PerfView（高级分析）

1. 打开 PerfView.exe
2. `File` → `Open` → 选择 `.etl` 或 `.nettrace`
3. 双击 `Events` 查看事件流
4. 双击 `CPU Stacks` 查看 CPU 火焰图

**关键视图：**
- **CPU Stacks**：查看哪些方法消耗 CPU
- **GCStats**：垃圾回收统计
- **Memory**：内存分配堆栈
- **Events**：原始事件流（可过滤）

---

## 常见场景

### 🔍 场景 1：诊断启动缓慢

```bash
# 收集启动过程的 CPU 和类型加载事件
dotnet-trace collect --process-id <PID> \
  --providers Microsoft-Windows-DotNETRuntime:0x10008:5 \
  --duration 00:00:30
```

**分析重点：**
- 查找 `Type Load` 事件密集的时间段
- 检查是否有大量反射调用
- 查看静态构造函数耗时

### 🗑️ 场景 2：诊断内存泄漏

```bash
# 收集 GC 和堆分配
dotnet-trace collect --process-id <PID> --profile gc-verbose --duration 00:05:00
```

**分析步骤：**
1. 在 PerfView 中打开 `GCStats`
2. 查看 Gen 2 GC 频率（过高则可能泄漏）
3. 打开 `Memory` → `GC Heap Net Mem` 查看未回收对象
4. 按类型排序，找出数量异常的对象

### ⚠️ 场景 3：诊断异常频繁抛出

```bash
dotnet-trace collect --process-id <PID> \
  --providers Microsoft-Windows-DotNETRuntime:0x8000:5 \
  --duration 00:02:00
```

**在 PerfView 中：**
1. 打开 `Events` 视图
2. 过滤 `EventName == "Exception/Start"`
3. 按 `ExceptionType` 分组
4. 查看抛出最多的异常类型和调用栈

### 🧵 场景 4：诊断线程争用（Deadlock）

```bash
dotnet-trace collect --process-id <PID> \
  --providers Microsoft-Windows-DotNETRuntime:0x10000:5 \
  --duration 00:03:00
```

**分析线程活动：**
- 查看 `Thread Pool` 事件
- 检查 `Contention` 事件（锁等待）
- 使用 `ThreadTime` 视图查看线程时间分布

---

## Reactor 专属性能分析

本项目使用 **Microsoft.UI.Reactor** 框架，该框架内置了详细的性能遥测事件。

### 📊 收集 Reactor 事件

```bash
# 启动应用（Debug 模式）
dotnet run -c Debug

# 收集 Reactor 专属事件
dotnet-trace collect --process-id <PID> \
  --providers Microsoft-UI-Reactor:0xFFFFFFFF:5 \
  --duration 00:02:00
```

### 🔥 Reactor 事件类型

| 事件类型 | 说明 | 用途 |
|---------|------|------|
| `Reconciler.Mount` | 组件首次挂载 | 分析首屏渲染时间 |
| `Reconciler.Update` | 组件更新（diff + patch） | 找出重渲染瓶颈 |
| `Component.Render` | 组件 Render() 调用 | 检测渲染逻辑耗时 |
| `Effect.Flush` | UseEffect 回调执行 | 副作用性能分析 |
| `State.Write` | UseState 状态更新 | 跟踪状态变更频率 |
| `RoutedEvent.Hop` | WinUI 路由事件处理 | 事件处理性能 |

### 📈 分析 Reactor 跟踪

**使用 speedscope.app：**
1. 上传 `trace.nettrace` 到 https://www.speedscope.app/
2. 切换到 "Sandwich" 视图
3. 搜索 `Microsoft.UI.Reactor`
4. 查看哪些组件 Render() 最耗时

**常见优化点：**
- ⚠️ **过度渲染**：同一组件在短时间内多次 Render
  - **解决**：使用 `UseMemo` 缓存计算结果
  - **解决**：拆分大组件，减少 diff 范围
- ⚠️ **昂贵的 Effect**：Effect 回调执行时间过长
  - **解决**：异步化 Effect 逻辑
  - **解决**：添加依赖数组，减少执行频率
- ⚠️ **状态抖动**：频繁的 State.Write 事件
  - **解决**：使用 `UseReducer` 批量更新状态
  - **解决**：防抖/节流用户输入

**信息来源：**
- [Reactor Performance Guide](https://microsoft.github.io/microsoft-ui-reactor/performance/)

---

## 实战示例：调试 DockedTools

### 步骤 1：构建 Release 版本（含 EventPipe）

```bash
dotnet build -c Release /p:Platform=x64
```

### 步骤 2：运行应用

```bash
dotnet run -c Release
```

### 步骤 3：获取进程 ID

```bash
# 方式 1：使用 dotnet-trace
dotnet-trace ps | findstr "DockedTools"

# 方式 2：使用 tasklist
tasklist | findstr "DockedTools.exe"
```

### 步骤 4：收集跟踪（60 秒）

```bash
dotnet-trace collect --process-id <PID> --profile cpu-sampling --duration 00:01:00
```

### 步骤 5：分析结果

```bash
# 打开 speedscope.app 并上传 trace.nettrace
start https://www.speedscope.app/
```

---

## 📌 注意事项

### ⚠️ 体积增加

启用 `EventSourceSupport=true` 后，AOT 编译产物会增加约 **300-500 KB**。

如需极致体积优化，可以：
```xml
<!-- 仅在 Debug 启用 -->
<EventSourceSupport Condition="'$(Configuration)' == 'Debug'">true</EventSourceSupport>
```

### ⚠️ 性能开销

EventPipe 的运行时开销非常低（通常 < 5%），但在高频事件场景（如大量异常）下会增加。

**建议：**
- 生产环境按需开启
- 使用 `--duration` 限制收集时长
- 仅收集必要的 Provider

### ⚠️ AOT 限制

某些诊断功能在 Native AOT 中不可用：
- ❌ 附加调试器（无法使用 Visual Studio 调试器）
- ❌ 动态代码生成（如反射 Emit）
- ✅ EventPipe 跟踪（完全支持）
- ✅ 内存转储分析（支持）
- ✅ 性能计数器（部分支持）

**信息来源：**
- [Native AOT Diagnostics - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/diagnostics)

---

## 🛠️ 故障排除

### 问题 1：`dotnet-trace` 找不到进程

**原因**：应用可能已退出或权限不足

**解决**：
```bash
# 使用进程 ID 直接指定
dotnet-trace collect -p <PID>

# 或使用进程名称
dotnet-trace collect --name "DockedTools"
```

### 问题 2：生成的 `.nettrace` 文件为空

**原因**：未启用 `EventSourceSupport`

**解决**：确认项目文件中已添加：
```xml
<EventSourceSupport>true</EventSourceSupport>
```

然后重新构建：
```bash
dotnet build -c Release /p:Platform=x64
```

### 问题 3：speedscope.app 无法解析文件

**原因**：某些版本的 `.nettrace` 不兼容

**解决**：
```bash
# 转换为 Chromium 格式
dotnet-trace convert trace.nettrace --format Chromium

# 上传 trace.json 到 speedscope.app
```

---

## 📚 参考资料

**官方文档：**
- [EventPipe Overview](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventpipe)
- [dotnet-trace Tool](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)
- [Native AOT Diagnostics](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/diagnostics)

**性能分析工具：**
- [speedscope.app](https://www.speedscope.app/) - 在线火焰图可视化
- [PerfView](https://github.com/microsoft/perfview) - Windows ETW 分析工具

**Reactor 专属：**
- [Reactor Performance Guide](https://microsoft.github.io/microsoft-ui-reactor/performance/)
- [Reactor GitHub Repository](https://github.com/microsoft/microsoft-ui-reactor)

---

**最后更新**：2026年7月7日  
**作者**：DockedTools 开发团队 🐺
