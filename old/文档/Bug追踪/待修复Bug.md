# 待修复 Bug 清单

本文档记录**已确认且计划修复**的高优先级 bug。

> 💡 **使用说明**：  
> - 🔴 严重级别的 bug 应优先处理  
> - 使用 [Bug 模板](Bug模板.md) 报告新 bug  
> - 修复后移至 [已修复Bug.md](已修复Bug.md)

---

## 🔥 P0 级 - 紧急修复

### 暂无

---

## 🟡 P1 级 - 高优先级

### 🐛 [BUG-20260620-001] 导航系统问题

| 项目 | 内容 |
|------|------|
| **严重程度** | 🟡 中等 |
| **优先级** | P1-高 |
| **状态** | 🆕 新建 |
| **报告日期** | 2026-06-20 |
| **影响版本** | v0.3.0 |
| **目标修复版本** | v0.3.1 |
| **负责人** | 待分配 |

**问题描述**：  
导航系统存在潜在问题，可能表现为页面导航不响应或导航历史错乱。

**可能原因分析**：

1. **缓存页面和导航历史不同步** (`链接器.xaml.cs`)
   ```csharp
   // Line 163-176: 关闭页面时的导航逻辑
   ContentHost.RemoveCachedPage(shortcutId);
   if (ContentHost.CanGoBack) {
       ContentHost.GoBack();  // ⚠️ 可能导航到已删除的缓存页面
   }
   ```
   - **问题**：删除缓存页面后立即 `GoBack()`，可能导航到已删除的页面
   - **风险**：导致空白页或崩溃

2. **异步导航竞态条件** (`网页浏览页面.xaml.cs`)
   ```csharp
   // Line 597-611: 多个异步操作同时操作导航
   _ = EnsureWebViewInitializedAsync().ContinueWith(...)
   TryNavigatePendingUri();
   ```
   - **问题**：异步初始化和导航可能同时执行
   - **风险**：导航请求被覆盖或重复执行

3. **导航状态标志未正确维护**
   ```csharp
   private bool _isNavigatingBack = false;  // Line 30
   // ⚠️ 设置后从未读取，可能导致状态不一致
   ```

**复现步骤**：
1. 打开多个网页标签
2. 快速切换页面
3. 关闭其中一个页面
4. 观察是否能正常返回

**临时解决方案**：  
避免快速连续点击导航按钮

**相关文件**：
- `功能/主窗口内容区/链接器/链接器.xaml.cs`
- `功能/主窗口内容区/内容区/ContentArea.cs`
- `功能/页面/网页应用/网页浏览/网页浏览页面.xaml.cs`

**修复建议**：
1. 在 `RemoveCachedPage` 后检查 BackStack 是否包含该页面
2. 添加导航锁，防止并发导航操作
3. 正确使用 `_isNavigatingBack` 标志

---

### 🐛 [BUG-20260620-002] 网页取色功能不稳定

| 项目 | 内容 |
|------|------|
| **严重程度** | 🟡 中等 |
| **优先级** | P1-高 |
| **状态** | 🔧 修复中 |
| **报告日期** | 2026-06-20 |
| **影响版本** | v0.3.0 |
| **目标修复版本** | v0.3.1 |
| **负责人** | 待分配 |

**问题描述**：  
网页取色功能在某些情况下无法正常工作，或取色不准确。

**可能原因分析**：

1. **取色状态管理混乱** (`BarThemeManager.cs`)
   ```csharp
   // Line 87-91: 重置取色状态
   public void ResetTintState() {
       _hasReceivedFirstTint = false;
       _hasAppliedThemeColor = false;
   }
   ```
   - **问题**：在导航开始时调用，但可能在取色脚本执行前就被重置
   - **风险**：导致第一次取色被忽略或错误的白色闪现

2. **取色消息处理竞态** (`BarThemeManager.cs`)
   ```csharp
   // Line 109: 处理取色消息
   if (messageType == "docked_ai_tint" && _hasAppliedThemeColor) {
       return false;  // ⚠️ 跳过采样颜色
   }
   ```
   - **问题**：如果 theme-color 被应用，后续的采样颜色会被忽略
   - **风险**：页面动态变化时颜色不更新

3. **脚本注入时机问题** (`网页浏览页面.xaml.cs`)
   ```csharp
   // Line 825-830: 延迟注入脚本
   await Task.Delay(100); // 让首次导航先开始
   await DispatcherQueue.EnqueueAsync(...)
   ```
   - **问题**：100ms 延迟可能不够，导致脚本注入时页面尚未加载
   - **风险**：取色脚本无法执行

4. **WebView 暂停/恢复时脚本状态丢失**
   ```csharp
   // Line 586-592: WebView 恢复
   WebView.CoreWebView2.Resume();
   // ⚠️ 恢复后取色脚本可能需要重新注入
   ```

**复现步骤**：
1. 打开一个有背景颜色的网页
2. 观察顶部栏和底部栏颜色是否正确
3. 快速切换到其他页面再切换回来
4. 观察颜色是否仍然正确

**复现频率**：经常复现（>50%）

**影响范围**：
- 所有使用 WebView 的页面
- 特别是深色主题网页

**临时解决方案**：  
刷新页面（F5）重新触发取色

**相关文件**：
- `功能/页面/网页应用/网页浏览/Managers/BarThemeManager.cs`
- `功能/页面/网页应用/网页浏览/网页浏览页面.xaml.cs`
- `功能/页面/网页应用/网页浏览/Services/WebViewTintScript.cs`

**修复建议**:
1. ✅ **已完成** - 改进取色状态机，在导航完成时重置而非导航开始时
2. ✅ **已完成** - 在 NavigationCompleted 后延迟 200ms 再取色，确保 DOM 完全加载
3. ✅ **已完成** - 如果没有 theme-color，主动触发采样取色（调用 `window.__dockedAiTint.updateNow()`）
4. ✅ **已完成** - WebView 恢复后重新注入取色脚本（`ReInjectTintScriptAsync`）
5. ✅ **已完成** - 页面恢复时重新取色（`RefreshPageTintAsync`）

**修复详情**：
```csharp
// 1. NavigationStarting 不再重置状态
private void CoreWebView2_NavigationStarting(...) {
    // ❌ 移除：_hasReceivedFirstTint = false;
    // ❌ 移除：_hasAppliedThemeColor = false;
}

// 2. NavigationCompleted 重置状态并延迟取色
private async void CoreWebView2_NavigationCompleted(...) {
    // ✅ 在这里重置
    _hasReceivedFirstTint = false;
    _hasAppliedThemeColor = false;
    
    // ✅ 延迟 200ms 等待 DOM 加载
    await Task.Delay(200);
    await TryApplyThemeColorAsync();
    
    // ✅ 新增：如果没有 theme-color，主动触发采样
    if (!_hasAppliedThemeColor) {
        await TriggerTintSamplingAsync();
    }
}

// 3. WebView 恢复后重新注入脚本
if (WebView.CoreWebView2 != null) {
    WebView.CoreWebView2.Resume();
    await ReInjectTintScriptAsync();  // ✅ 新增
}

// 4. 页面恢复时刷新取色
else {
    await RefreshPageTintAsync();  // ✅ 新增
}

// 5. 新增主动触发采样函数
private async Task TriggerTintSamplingAsync() {
    await WebView.CoreWebView2.ExecuteScriptAsync(@"
        window.__dockedAiTint?.updateNow();
    ");
}
```

**进度更新**：
- 2026-06-20 23:45：Bug 已确认，开始修复
- 2026-06-20 23:50：修复完成，等待测试验证
- 2026-06-20 23:58：修复已完成，等待哥哥测试
- 2026-06-21 00:15：发现首次加载不取色问题，添加主动触发采样逻辑
- 2026-06-21 00:30：修复黑屏闪现问题，禁用脚本自动触发，完全由 C# 控制首次采样
- 2026-06-21 00:35：修复初始颜色问题，改为透明色并使用淡入动画
- 2026-06-21 00:45：优化按钮颜色逻辑，采用 Material Design 最佳实践（Hover 8%, Pressed 12%, Disabled 38%）

---

### 🐛 [BUG-20260620-003] 长时间休眠后应用自动退出

| 项目 | 内容 |
|------|------|
| **严重程度** | 🔴 严重 |
| **优先级** | P1-高 |
| **状态** | 🆕 新建 |
| **报告日期** | 2026-06-20 |
| **影响版本** | v0.3.0 |
| **目标修复版本** | v0.3.1 |
| **负责人** | 待分配 |

**问题描述**：  
电脑长时间休眠（如过夜）后，应用会自动退出，需要重新启动。

**可能原因分析**：

1. **WebView 暂停后无法恢复** (`网页浏览页面.xaml.cs`)
   ```csharp
   // Line 640-648: 暂停 WebView
   if (ExperimentalSettings.SuspendInactiveWebView) {
       _ = WebView.CoreWebView2.TrySuspendAsync();
   }
   ```
   - **问题**：长时间暂停后，WebView 可能进入不可恢复状态
   - **风险**：导致崩溃或自动退出

2. **KeepAlive 窗口被系统回收** (`应用入口.cs`)
   ```csharp
   // Line 373-387: KeepAlive 窗口
   _keepAliveWindow = new Window {
       Content = new Grid()
   };
   keepAliveAppWindow.MoveAndResize(new RectInt32(-32000, -32000, 1, 1));
   ```
   - **问题**：隐藏窗口可能被系统休眠时回收
   - **风险**：应用失去窗口后被系统强制退出

3. **Mutex 在休眠后失效**
   ```csharp
   // Line 68: 单实例 Mutex
   _singleInstanceMutex = new Mutex(true, @"Local\DockedAI_SingleInstance_Mutex", ...)
   ```
   - **问题**：系统休眠可能导致 Mutex 失效
   - **风险**：恢复后可能被识别为多实例启动

4. **单实例通信监听器在休眠后停止** (`应用入口.cs`)
   ```csharp
   // Line 130-132: 启动监听器
   _singleInstanceCommunication = new SingleInstanceCommunication(OnShowWindowRequested);
   _singleInstanceCommunication.StartListening();
   ```
   - **问题**：EventWaitHandle 可能在休眠后失效
   - **风险**：无法响应唤醒请求，导致异常退出

5. **没有订阅系统电源事件**
   - **问题**：应用没有监听 `Suspend`/`Resume` 事件
   - **风险**：无法在休眠前保存状态，恢复后状态丢失

**复现步骤**：
1. 启动应用并最小化到托盘
2. 让电脑进入休眠（等待 8 小时以上）
3. 唤醒电脑
4. 检查应用是否还在运行

**复现频率**：经常复现（>80%，长时间休眠）

**影响范围**：
- 所有场景
- 特别是托盘模式运行时

**临时解决方案**：  
禁用"暂停不活跃 WebView"功能（设置 → 实验室 → WebView 性能 → 关闭）

**相关文件**：
- `功能/应用入口/应用入口.cs`
- `功能/页面/网页应用/网页浏览/网页浏览页面.xaml.cs`
- `功能/托盘/托盘图标管理器.cs`
- `功能/应用入口/SingleInstance/SingleInstanceCommunication.cs`

**修复建议**：
1. **订阅系统电源事件**：
   ```csharp
   // 添加 Windows.System.Power.PowerManager 事件监听
   PowerManager.EnergySaverStatusChanged += OnPowerStatusChanged;
   ```

2. **改进 KeepAlive 窗口**：
   ```csharp
   // 使用可见但透明的窗口，避免被回收
   _keepAliveWindow.AppWindow.SetPresenter(AppWindowPresenterKind.Default);
   ```

3. **WebView 暂停策略优化**：
   ```csharp
   // 只暂停短时间切换的页面，避免长时间暂停
   if (inactiveDuration < TimeSpan.FromMinutes(30)) {
       _ = WebView.CoreWebView2.TrySuspendAsync();
   }
   ```

4. **添加恢复机制**：
   ```csharp
   // 定期检查 WebView 状态，必要时重新初始化
   DispatcherTimer healthCheckTimer = new() { Interval = TimeSpan.FromMinutes(5) };
   healthCheckTimer.Tick += CheckWebViewHealth;
   ```

5. **Mutex 保活**：
   ```csharp
   // 定期重新获取 Mutex 所有权
   Task.Run(async () => {
       while (true) {
           await Task.Delay(TimeSpan.FromMinutes(10));
           _singleInstanceMutex?.ReleaseMutex();
           _singleInstanceMutex?.WaitOne();
       }
   });
   ```

**进度更新**：
- 2026-06-20：Bug 已确认，开始调查

---

## 🟢 P2 级 - 中优先级

### 暂无

---

## 📊 统计信息

| 优先级 | 数量 | 占比 |
|--------|------|------|
| P0-紧急 | 0 | 0% |
| P1-高 | 3 | 100% |
| P2-中 | 0 | 0% |
| P3-低 | 0 | 0% |
| **总计** | **3** | **100%** |

---

## 🔄 更新日志

| 日期 | 操作 | Bug ID | 说明 |
|------|------|--------|------|
| 2026-06-20 | 新增 | BUG-20260620-001 | 导航系统问题 |
| 2026-06-20 | 新增 | BUG-20260620-002 | 网页取色功能不稳定 |
| 2026-06-20 | 新增 | BUG-20260620-003 | 长时间休眠后自动退出 |
| 2026-06-20 | 修复 | BUG-20260620-002 | 网页取色功能修复完成，等待测试 |

---

## 📌 注意事项

1. **及时更新**：修复 bug 后立即更新状态并移至已修复清单
2. **优先级调整**：根据影响范围和紧急程度动态调整优先级
3. **关联引用**：为复杂 bug 创建独立的详细文档
4. **团队协作**：及时沟通 bug 状态和进展

## 🔗 相关链接

- [Bug 模板](Bug模板.md) - 报告新 bug
- [已知Bug](已知Bug.md) - 所有已发现的 bug
- [已修复Bug](已修复Bug.md) - 已解决的 bug
- [当前迭代](../项目进度/当前迭代.md) - 本迭代计划

---

*最后更新：2026-06-20*
