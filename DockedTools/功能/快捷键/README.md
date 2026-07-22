# 全局快捷键功能说明

## UI 更新 (2026-04-04)

### 使用 KeyVisual 风格显示快捷键

将快捷键显示从纯文本改为使用 ItemsControl + 自定义 DataTemplate 的方式，每个按键都有独立的视觉容器，类似于 Windows 11 和 PowerToys 的 KeyVisual 效果。

### 新的 UI 结构
```xml
<controls:SettingsCard x:Uid="SettingsPage_HotkeyConfigHeader"
                  IsClickEnabled="True"
                  Click="OnHotkeyButtonClick">
    <ItemsControl x:Name="HotkeyKeysDisplay" 
                HorizontalAlignment="Right">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <StackPanel Orientation="Horizontal" Spacing="4"/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Border Background="{ThemeResource ControlFillColorDefaultBrush}"
                       BorderBrush="{ThemeResource ControlStrokeColorDefaultBrush}"
                       BorderThickness="1"
                       CornerRadius="4"
                       Padding="8,4"
                       MinWidth="32">
                    <TextBlock Text="{Binding}"
                             FontSize="14"
                             FontWeight="SemiBold"
                             HorizontalAlignment="Center"
                             VerticalAlignment="Center"/>
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</controls:SettingsCard>
```

### 设计特点
1. 每个按键显示为独立的视觉元素（Border + TextBlock）
2. 使用主题资源确保与系统主题一致
3. 圆角边框（CornerRadius="4"）符合 Fluent Design
4. 按键之间有 4px 间距，清晰分隔
5. 使用 SemiBold 字重，提高可读性

### C# 代码更新
```csharp
private void UpdateHotkeyButtonText()
{
    var keys = new System.Collections.Generic.List<string>();
    
    if (_hotkeySettings.Ctrl) keys.Add("Ctrl");
    if (_hotkeySettings.Alt) keys.Add("Alt");
    if (_hotkeySettings.Shift) keys.Add("Shift");
    if (_hotkeySettings.Win) keys.Add("Win");
    
    if (_hotkeySettings.Key != VirtualKey.None)
    {
        keys.Add(GetKeyDisplayName(_hotkeySettings.Key));
    }
    
    // 使用 ItemsControl 显示按键，每个按键都有独立的视觉容器
    HotkeyKeysDisplay.ItemsSource = keys;
}
```

### 优势
1. 更好的视觉呈现 - 每个按键都有独立的视觉容器，类似键盘按键
2. 符合 Windows 11 设计规范 - 使用 Fluent Design 风格和主题资源
3. 与 PowerToys 一致 - 采用相同的 KeyVisual 概念
4. 更易识别 - 按键之间有明确的视觉分隔和间距
5. 主题适配 - 自动适应浅色/深色主题
6. 无外部依赖 - 使用 WinUI 3 原生控件，简单可维护

---

## UI 更新 (2026-04-03)

### 更改内容
将快捷键设置从单一的 SettingsCard 改为可展开的 SettingsExpander，提供更好的用户体验和视觉层次。

### 新的 UI 结构
```xml
<controls:SettingsExpander x:Name="HotkeyExpander"
                      x:Uid="SettingsPage_HotkeyHeader"
                      IsExpanded="False">
    <controls:SettingsExpander.HeaderIcon>
        <FontIcon Glyph="&#xE765;"/>
    </controls:SettingsExpander.HeaderIcon>
    
    <!-- 主内容：开关 -->
    <ToggleSwitch 
        x:Name="HotkeyToggle"
        Toggled="OnHotkeyToggled"/>
    
    <!-- 展开项：可点击的编辑卡片 -->
    <controls:SettingsExpander.Items>
        <controls:SettingsCard x:Uid="SettingsPage_HotkeyConfigHeader"
                          IsClickEnabled="True"
                          Click="OnHotkeyButtonClick">
            <controls:SettingsCard.HeaderIcon>
                <FontIcon Glyph="&#xE70F;"/>  <!-- 编辑图标 -->
            </controls:SettingsCard.HeaderIcon>
            <TextBlock x:Name="HotkeyText" 
                      Text="Ctrl + Alt + Space"
                      FontWeight="SemiBold"/>
        </controls:SettingsCard>
    </controls:SettingsExpander.Items>
</controls:SettingsExpander>
```

### 设计特点
1. 使用编辑图标 (&#xE70F;) 清晰表达功能意图
2. 整个 SettingsCard 可点击，提供更大的点击区域
3. 快捷键文本使用 SemiBold 字重，更加醒目
4. 移除了独立的 Button，简化了 UI 层次

### 优势
1. 更清晰的视觉层次 - 主要控制（开关）在顶层，详细配置在展开项中
2. 节省空间 - 默认折叠状态，只在需要时展开
3. 一致的设计 - 与"关于应用"部分使用相同的 SettingsExpander 模式
4. 更好的组织 - 可以在未来轻松添加更多快捷键相关设置
5. 更大的点击区域 - 整个卡片都可点击，提升可用性

### 本地化支持
已为所有支持的语言添加新的资源字符串：
- `SettingsPage_HotkeyConfigHeader.Header` - 快捷键配置标题
- `SettingsPage_HotkeyConfigHeader.Description` - 快捷键配置描述

支持的语言：
- 简体中文 (zh-CN)
- 繁体中文 (zh-TW)
- 英语 (en-US)
- 日语 (ja-JP)
- 韩语 (ko-KR)
- 法语 (fr-FR)
- 德语 (de-DE)
- 西班牙语 (es-ES)

---

## 最终实现方案：ToggleButton + PreviewKeyDown

### 问题历程
1. 最初使用 Border + KeyDown → 无法捕获输入（Border 不接收焦点）
2. 改用 TextBox + PreviewKeyDown → 可以工作，但用户体验不佳
3. 研究 PowerToys 源代码 → 发现他们使用 ToggleButton + 低级键盘钩子
4. 最终方案：ToggleButton + PreviewKeyDown（折中方案）

### PowerToys 的实现方式

PowerToys Keyboard Manager 使用：
- **UI 层：** ToggleButton 作为容器，内部使用 ItemsControl 显示多个 KeyVisual
- **输入捕获：** 低级键盘钩子 (Low-Level Keyboard Hook)
- **优势：** 完全控制键盘输入，不受控件焦点限制，支持和弦键
- **劣势：** 实现复杂，需要处理 Win32 API、线程安全、资源管理

### 我们的实现方式

基于 PowerToys 的经验，采用简化方案：
- **UI 层：** ToggleButton 提供清晰的"录制"状态
- **输入捕获：** PreviewKeyDown 事件（WinUI 3 标准事件）
- **优势：** 代码简单，易维护，用户体验好
- **劣势：** 依赖控件焦点，不支持和弦键（但我们不需要）

### 实现细节

#### XAML 部分
```xml
<TextBox
    x:Name="HotkeyInputBox"
    Text="等待输入..."
    IsReadOnly="True"
    TextAlignment="Center"
    FontSize="18"
    FontWeight="SemiBold"
    MinHeight="60"
    PreviewKeyDown="OnHotkeyInputBoxPreviewKeyDown"
    GotFocus="OnHotkeyInputBoxGotFocus"/>
```

#### C# 部分
```csharp
private void OnHotkeyInputBoxPreviewKeyDown(object sender, KeyRoutedEventArgs e)
{
    if (!_isCapturingHotkey) return;
    e.Handled = true;
    
    var key = e.Key;
    
    // 获取修饰键状态
    var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
    bool ctrl = (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    // ... 其他修饰键
    
    // 验证并显示
    if (!ctrl && !alt && !shift && !win)
    {
        HotkeyInputBox.Text = "需要至少一个修饰键";
        return;
    }
    
    HotkeyInputBox.Text = GetHotkeyDisplayText(key, ctrl, alt, shift, win);
}

private void OnHotkeyInputBoxGotFocus(object sender, RoutedEventArgs e)
{
    // 清空文本以便显示新输入
    if (_isCapturingHotkey && HotkeyInputBox.Text == "等待输入...")
    {
        HotkeyInputBox.Text = "";
    }
}
```

### 关键点

1. **使用 TextBox 而不是 Border**
   - TextBox 自动获得焦点
   - 可以接收键盘事件

2. **使用 PreviewKeyDown 而不是 KeyDown**
   - PreviewKeyDown 在 WinUI 3 中更可靠
   - 在事件冒泡之前捕获

3. **设置 IsReadOnly="True"**
   - 防止用户直接输入文本
   - 只允许通过快捷键设置

4. **使用 InputKeyboardSource 获取修饰键状态**
   - 可靠地检测 Ctrl、Alt、Shift、Win 键
   - 跨平台兼容

### 测试步骤

1. 打开设置页面
2. 点击快捷键按钮
3. 对话框弹出，TextBox 自动获得焦点
4. 按下快捷键组合（如 Ctrl + Alt + A）
5. TextBox 中实时显示快捷键
6. 点击确认保存

### 参考资料

- [PowerToys Keyboard Manager 实现](https://github.com/microsoft/PowerToys/tree/main/src/modules/keyboardmanager)
- [WinUI 3 键盘输入问题 #7330](https://github.com/microsoft/microsoft-ui-xaml/issues/7330)
- [PreviewKeyDown 事件文档](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.previewkeydown)

## 当前状态

✅ 快捷键输入捕获正常工作
✅ 使用 TextBox + PreviewKeyDown 方案
✅ 编译无错误和警告
✅ 基于 PowerToys 验证的方案


## PowerToys vs 我们的实现对比

| 方面 | PowerToys | 我们的实现 |
|------|-----------|-----------|
| **UI 框架** | WinUI 3 (v0.98+) | WinUI 3 |
| **输入捕获控件** | TextBox/Button | TextBox |
| **键盘事件** | PreviewKeyDown | PreviewKeyDown |
| **修饰键检测** | InputKeyboardSource | InputKeyboardSource |
| **全局快捷键** | 低级键盘钩子 | NHotkey.WinUI |
| **复杂度** | 高（自己实现钩子） | 低（使用成熟库） |

### 技术选择的权衡

**PowerToys 的方案：**
- ✅ 完全控制，功能强大
- ✅ 可以实现复杂的按键重映射
- ✅ 支持应用级别的规则
- ❌ 实现复杂，需要处理 Win32 API
- ❌ 需要处理线程安全和资源管理
- ❌ 维护成本高

**我们的方案（NHotkey.WinUI）：**
- ✅ 简单易用，10 行代码
- ✅ 成熟稳定，经过生产验证
- ✅ 自动处理资源管理
- ✅ 维护成本低
- ❌ 功能相对简单（但满足需求）
- ❌ 依赖第三方库

### 为什么我们不直接使用低级键盘钩子

1. **复杂性**
   - 需要处理 Win32 API
   - 需要管理钩子的生命周期
   - 需要处理线程安全问题

2. **风险**
   - 容易导致 ExecutionEngineException
   - 可能影响系统稳定性
   - 调试困难

3. **维护成本**
   - 需要深入了解 Windows 消息机制
   - 需要处理各种边界情况
   - 需要持续维护和更新

4. **NHotkey.WinUI 已经足够**
   - 封装了低级键盘钩子
   - 提供了简单的 API
   - 经过充分测试
   - 社区支持

## 关键技术点总结

### 1. 输入捕获的正确方式

```csharp
// ❌ 错误：Border 不接收焦点
<Border KeyDown="OnKeyDown">
    <TextBlock Text="按下快捷键"/>
</Border>

// ✅ 正确：TextBox 可以接收焦点
<TextBox 
    IsReadOnly="True"
    PreviewKeyDown="OnPreviewKeyDown"/>
```

### 2. 修饰键检测的正确方式

```csharp
// ✅ WinUI 3 推荐方式
var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
bool isCtrlPressed = (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

// ⚠️ Win32 方式（也可以，但不推荐）
[DllImport("user32.dll")]
static extern short GetKeyState(int nVirtKey);
bool isCtrlPressed = (GetKeyState((int)VirtualKey.Control) & 0x8000) != 0;
```

### 3. 全局快捷键的正确方式

```csharp
// ✅ 使用成熟库
HotkeyManager.Current.AddOrReplace(
    "MyHotkey",
    VirtualKey.Space,
    VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu,
    OnHotkeyPressed);

// ❌ 自己实现（不推荐，除非有特殊需求）
[DllImport("user32.dll")]
static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
```

## 实现细节

### XAML 部分
```xml
<ToggleButton
    x:Name="HotkeyToggleButton"
    MinHeight="80"
    Padding="16"
    HorizontalAlignment="Stretch"
    HorizontalContentAlignment="Center"
    VerticalContentAlignment="Center"
    Checked="HotkeyToggleButton_Checked"
    Unchecked="HotkeyToggleButton_Unchecked"
    PreviewKeyDown="HotkeyToggleButton_PreviewKeyDown"
    PreviewKeyUp="HotkeyToggleButton_PreviewKeyUp">
    <TextBlock
        x:Name="HotkeyDisplayText"
        Text="点击开始录制"
        FontSize="16"
        TextAlignment="Center"
        TextWrapping="Wrap"/>
</ToggleButton>
```

**关键属性：**
- `ToggleButton` - 提供明确的"录制"状态（选中/未选中）
- `PreviewKeyDown` - 捕获按键按下事件
- `PreviewKeyUp` - 捕获按键释放事件（用于自动结束录制）
- 内部 `TextBlock` - 显示当前状态或捕获的快捷键

### C# 部分
```csharp
private bool _isCapturingHotkey;
private VirtualKey _tempKey = VirtualKey.None;
private bool _tempCtrl, _tempAlt, _tempShift, _tempWin;

private void HotkeyToggleButton_Checked(object sender, RoutedEventArgs e)
{
    _isCapturingHotkey = true;
    _tempKey = VirtualKey.None;
    _tempCtrl = _tempAlt = _tempShift = _tempWin = false;
    HotkeyDisplayText.Text = "按下快捷键...";
}

private void HotkeyToggleButton_Unchecked(object sender, RoutedEventArgs e)
{
    _isCapturingHotkey = false;
}

private void HotkeyToggleButton_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
{
    if (!_isCapturingHotkey || HotkeyToggleButton.IsChecked != true) return;
    
    e.Handled = true;
    var key = e.Key;
    
    // 获取修饰键状态
    var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
    var altState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
    var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
    var winLeftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows);
    var winRightState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows);
    
    bool ctrl = (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    bool alt = (altState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    bool shift = (shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    bool win = (winLeftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
               (winRightState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    
    // 忽略单独的修饰键
    if (key == VirtualKey.Control || key == VirtualKey.Menu ||
        key == VirtualKey.Shift || key == VirtualKey.LeftWindows || 
        key == VirtualKey.RightWindows)
    {
        return;
    }
    
    // 验证至少有一个修饰键
    if (!ctrl && !alt && !shift && !win)
    {
        HotkeyDisplayText.Text = "需要至少一个修饰键";
        return;
    }
    
    // 保存并显示
    _tempKey = key;
    _tempCtrl = ctrl;
    _tempAlt = alt;
    _tempShift = shift;
    _tempWin = win;
    
    HotkeyDisplayText.Text = GetHotkeyDisplayText(key, ctrl, alt, shift, win);
}

private void HotkeyToggleButton_PreviewKeyUp(object sender, KeyRoutedEventArgs e)
{
    if (!_isCapturingHotkey || HotkeyToggleButton.IsChecked != true) return;
    
    e.Handled = true;
    
    // 当所有键都释放后，自动取消选中 ToggleButton
    var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
    var altState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
    var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
    var winLeftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows);
    var winRightState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows);
    
    bool anyModifierPressed = 
        (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
        (altState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
        (shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
        (winLeftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down ||
        (winRightState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    
    // 如果有有效的快捷键且所有键都释放了，自动取消选中
    if (_tempKey != VirtualKey.None && !anyModifierPressed)
    {
        HotkeyToggleButton.IsChecked = false;
    }
}
```

**关键点：**
1. `e.Handled = true` - 防止事件继续传播
2. 使用 `InputKeyboardSource.GetKeyStateForCurrentThread()` 检测修饰键
3. 忽略单独的修饰键按下
4. 验证至少有一个修饰键
5. 实时更新显示
6. PreviewKeyUp 实现自动结束录制（用户体验优化）

### 用户体验流程

1. 用户点击快捷键按钮 → 打开对话框
2. 对话框显示 ToggleButton，文本为"点击开始录制"
3. 用户点击 ToggleButton → 变为选中状态，文本变为"按下快捷键..."
4. 用户按下快捷键组合（如 Ctrl + Alt + K）
5. ToggleButton 实时显示 "Ctrl + Alt + K"
6. 用户释放所有按键 → ToggleButton 自动取消选中
7. 用户点击"确认"保存快捷键

### 与 PowerToys 的对比

| 方面 | PowerToys | 我们的实现 |
|------|-----------|-----------|
| **UI 控件** | ToggleButton + ItemsControl | ToggleButton + TextBlock |
| **输入捕获** | 低级键盘钩子 | PreviewKeyDown 事件 |
| **按键显示** | 多个 KeyVisual 控件 | 单个文本字符串 |
| **实时更新** | 是（按键按下时） | 是（按键按下时） |
| **自动结束** | 否（需要手动点击） | 是（释放按键后自动） |
| **和弦支持** | 是（4 修饰键 + 1 动作键） | 否（1 修饰键组合 + 1 动作键） |
| **复杂度** | 高（低级钩子） | 低（标准事件） |
| **维护成本** | 高 | 低 |

### 测试步骤

1. 打开设置页面
2. 点击快捷键按钮
3. 对话框弹出，点击 ToggleButton 开始录制
4. 按下快捷键组合（如 Ctrl + Alt + A）
5. ToggleButton 实时显示快捷键
6. 释放所有按键，ToggleButton 自动取消选中
7. 点击确认保存

### 参考资料

1. **PowerToys 相关：**
   - [PowerToys GitHub](https://github.com/microsoft/PowerToys)
   - [PowerToys 0.98 发布说明](https://devblogs.microsoft.com/commandline/powertoys-0-98-is-here-new-keyboard-manager-ux-the-command-palette-dock-and-better-cursorwrap/)
   - [Keyboard Manager Wiki](https://github.com/microsoft/PowerToys/wiki/Keyboard-Manager)

2. **WinUI 3 文档：**
   - [PreviewKeyDown 事件](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.previewkeydown)
   - [InputKeyboardSource](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.input.inputkeyboardsource)
   - [ToggleButton 控件](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.primitives.togglebutton)

3. **NHotkey：**
   - [NHotkey GitHub](https://github.com/thomaslevesque/NHotkey)
   - [NHotkey.WinUI NuGet](https://www.nuget.org/packages/NHotkey.WinUI)

## 当前状态

✅ 快捷键输入捕获正常工作  
✅ 使用 ToggleButton + PreviewKeyDown 方案  
✅ 基于 PowerToys 验证的技术  
✅ 使用 NHotkey.WinUI 处理全局快捷键  
✅ 编译无错误和警告  
✅ 用户体验优秀（自动结束录制）  
✅ 生产环境可用
