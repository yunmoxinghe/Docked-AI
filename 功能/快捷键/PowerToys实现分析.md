# PowerToys Keyboard Manager 实现分析

## 核心发现

通过分析 PowerToys 0.98+ 的 WinUI 3 实现，我发现了他们的关键技术方案：

### 1. UI 层：使用 ToggleButton 而不是 TextBox

```xml
<ToggleButton
    x:Name="TriggerKeyToggleBtn"
    MinHeight="86"
    Padding="8,24,8,24"
    HorizontalAlignment="Stretch"
    HorizontalContentAlignment="Center"
    VerticalContentAlignment="Center"
    Checked="TriggerKeyToggleBtn_Checked"
    Style="{StaticResource CustomShortcutToggleButtonStyle}"
    Unchecked="TriggerKeyToggleBtn_Unchecked">
    <ToggleButton.Content>
        <ItemsControl x:Name="TriggerKeys">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <controls:WrapPanel
                        HorizontalSpacing="4"
                        Orientation="Horizontal"
                        VerticalSpacing="4" />
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <commoncontrols:KeyVisual
                        Padding="8"
                        Background="{ThemeResource ControlFillColorDefaultBrush}"
                        BorderThickness="1"
                        Content="{Binding}"
                        CornerRadius="{StaticResource OverlayCornerRadius}"
                        FontSize="16"
                        Style="{StaticResource DefaultKeyVisualStyle}" />
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </ToggleButton.Content>
</ToggleButton>
```

**关键点：**
- 使用 `ToggleButton` 作为容器
- 内部使用 `ItemsControl` 显示多个 `KeyVisual` 控件
- 当 ToggleButton 被选中时，激活键盘钩子

### 2. 键盘输入捕获：使用低级键盘钩子

PowerToys 不依赖 WinUI 3 的键盘事件，而是使用 **低级键盘钩子 (Low-Level Keyboard Hook)**：

```csharp
// KeyboardHookHelper.cs
public void ActivateHook(IKeyboardHookTarget target)
{
    CleanupHook();
    
    _activeTarget = target;
    _currentlyPressedKeys.Clear();
    _keyPressOrder.Clear();
    
    // 创建低级键盘钩子
    _keyboardHook = new HotkeySettingsControlHook(
        KeyDown,      // 按键按下回调
        KeyUp,        // 按键释放回调
        () => true,   // 是否激活
        (key, extraInfo) => true);  // 过滤器
}

private void KeyDown(int key)
{
    if (_activeTarget == null) return;
    
    VirtualKey virtualKey = (VirtualKey)key;
    
    if (_currentlyPressedKeys.Contains(virtualKey)) return;
    
    // 如果没有按键被按下，清空列表
    if (_currentlyPressedKeys.Count == 0)
    {
        _activeTarget.ClearKeys();
        _keyPressOrder.Clear();
    }
    
    // 限制：最多 4 个修饰键 + 1 个动作键
    int modifierCount = _currentlyPressedKeys.Count(k => RemappingHelper.IsModifierKey(k));
    
    if ((RemappingHelper.IsModifierKey(virtualKey) && modifierCount >= 4) ||
        (!RemappingHelper.IsModifierKey(virtualKey) && _currentlyPressedKeys.Count >= 5))
    {
        _activeTarget.OnInputLimitReached();
        return;
    }
    
    // 移除已存在的修饰键变体（例如左Ctrl替换右Ctrl）
    if (RemappingHelper.IsModifierKey(virtualKey))
    {
        RemoveExistingModifierVariant(virtualKey);
    }
    
    if (_currentlyPressedKeys.Add(virtualKey))
    {
        _keyPressOrder.Add(virtualKey);
        
        // 通知目标控件
        _activeTarget.OnKeyDown(virtualKey, GetFormattedKeyList());
    }
}

private List<string> GetFormattedKeyList()
{
    List<string> keyList = new List<string>();
    List<VirtualKey> modifierKeys = new List<VirtualKey>();
    VirtualKey? actionKey = null;
    VirtualKey? actionKeyChord = null;
    
    foreach (var key in _keyPressOrder)
    {
        if (!_currentlyPressedKeys.Contains(key)) continue;
        
        if (RemappingHelper.IsModifierKey(key))
        {
            if (!modifierKeys.Contains(key))
            {
                modifierKeys.Add(key);
            }
        }
        else if (actionKey.HasValue && _activeTarget.AllowChords)
        {
            actionKeyChord = key;
        }
        else
        {
            actionKey = key;
        }
    }
    
    // 按顺序添加：修饰键 -> 动作键 -> 和弦键
    foreach (var key in modifierKeys)
    {
        keyList.Add(_mappingService.GetKeyDisplayName((int)key));
    }
    
    if (actionKey.HasValue)
    {
        keyList.Add(_mappingService.GetKeyDisplayName((int)actionKey.Value));
    }
    
    if (actionKeyChord.HasValue && _activeTarget.AllowChords)
    {
        keyList.Add(_mappingService.GetKeyDisplayName((int)actionKeyChord.Value));
    }
    
    return keyList;
}
```

### 3. 控件实现：IKeyboardHookTarget 接口

```csharp
// UnifiedMappingControl.xaml.cs
public sealed partial class UnifiedMappingControl : UserControl, IKeyboardHookTarget
{
    private readonly ObservableCollection<string> _triggerKeys = new();
    private readonly ObservableCollection<string> _actionKeys = new();
    
    private KeyInputMode _currentInputMode = KeyInputMode.OriginalKeys;
    
    public bool AllowChords { get; set; } = true;
    
    // 当 ToggleButton 被选中时
    private void TriggerKeyToggleBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (TriggerKeyToggleBtn.IsChecked == true)
        {
            _currentInputMode = KeyInputMode.OriginalKeys;
            
            // 取消选中另一个 ToggleButton
            if (ActionKeyToggleBtn?.IsChecked == true)
            {
                ActionKeyToggleBtn.IsChecked = false;
            }
            
            // 激活键盘钩子
            KeyboardHookHelper.Instance.ActivateHook(this);
        }
    }
    
    // 实现 IKeyboardHookTarget 接口
    public void OnKeyDown(VirtualKey key, List<string> formattedKeys)
    {
        if (_currentInputMode == KeyInputMode.OriginalKeys)
        {
            _triggerKeys.Clear();
            foreach (var keyName in formattedKeys)
            {
                _triggerKeys.Add(keyName);
            }
            
            UpdateAppSpecificCheckBoxState();
        }
        else if (_currentInputMode == KeyInputMode.RemappedKeys)
        {
            _actionKeys.Clear();
            foreach (var keyName in formattedKeys)
            {
                _actionKeys.Add(keyName);
            }
        }
    }
    
    public void ClearKeys()
    {
        if (_currentInputMode == KeyInputMode.OriginalKeys)
        {
            _triggerKeys.Clear();
        }
        else
        {
            _actionKeys.Clear();
        }
    }
    
    public void OnInputLimitReached()
    {
        ShowNotificationTip("Shortcuts can only have up to 4 modifier keys");
    }
}
```

## PowerToys 方案的优势

### 1. 完全控制键盘输入
- 使用低级键盘钩子，不依赖 WinUI 3 的键盘事件
- 可以捕获所有按键，包括系统快捷键
- 不受控件焦点限制

### 2. 更好的用户体验
- ToggleButton 提供清晰的"录制"状态
- 实时显示多个按键（使用 ItemsControl + KeyVisual）
- 支持和弦键（Chord Keys）

### 3. 精确的按键处理
- 跟踪按键按下顺序
- 区分修饰键和动作键
- 处理修饰键变体（左/右 Ctrl、Alt、Shift）
- 限制最多 4 个修饰键 + 1 个动作键

## 与我们当前实现的对比

| 方面 | PowerToys | 我们的实现 |
|------|-----------|-----------|
| **UI 控件** | ToggleButton + ItemsControl | TextBox |
| **输入捕获** | 低级键盘钩子 | PreviewKeyDown 事件 |
| **按键显示** | 多个 KeyVisual 控件 | 单个文本字符串 |
| **实时更新** | 是（按键按下时） | 是（按键按下时） |
| **和弦支持** | 是 | 否 |
| **修饰键限制** | 4 个修饰键 + 1 动作键 | 无限制 |
| **复杂度** | 高 | 低 |

## 我们是否需要改用 PowerToys 的方案？

### 不需要的理由：

1. **功能需求不同**
   - PowerToys：复杂的按键重映射系统
   - 我们：简单的全局快捷键设置

2. **复杂度权衡**
   - PowerToys 方案需要实现低级键盘钩子
   - 需要处理线程安全、资源管理
   - 我们的 TextBox + PreviewKeyDown 方案已经足够

3. **维护成本**
   - 低级键盘钩子需要深入了解 Win32 API
   - 调试困难
   - 我们的方案简单易维护

4. **当前方案的问题**
   - 我们的 TextBox 方案在 WinUI 3 中可能不工作
   - 需要验证 PreviewKeyDown 是否能捕获输入

## 建议的改进方案

基于 PowerToys 的经验，我们可以采用折中方案：

### 方案 A：ToggleButton + PreviewKeyDown（推荐）

```xml
<ToggleButton
    x:Name="HotkeyToggleButton"
    MinHeight="60"
    HorizontalAlignment="Stretch"
    Checked="HotkeyToggleButton_Checked"
    Unchecked="HotkeyToggleButton_Unchecked"
    PreviewKeyDown="HotkeyToggleButton_PreviewKeyDown">
    <TextBlock x:Name="HotkeyDisplayText" Text="点击录制快捷键"/>
</ToggleButton>
```

```csharp
private HashSet<VirtualKey> _pressedKeys = new();

private void HotkeyToggleButton_Checked(object sender, RoutedEventArgs e)
{
    _pressedKeys.Clear();
    HotkeyDisplayText.Text = "按下快捷键...";
}

private void HotkeyToggleButton_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
{
    if (HotkeyToggleButton.IsChecked != true) return;
    
    e.Handled = true;
    
    var key = e.Key;
    
    // 获取修饰键状态
    var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
    bool ctrl = (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    // ... 其他修饰键
    
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
    
    // 自动取消选中
    HotkeyToggleButton.IsChecked = false;
}
```

**优势：**
- 更好的用户体验（明确的录制状态）
- 不需要低级键盘钩子
- 使用 WinUI 3 的标准事件
- 代码简单易维护

### 方案 B：保持 TextBox 但改进样式

如果 TextBox + PreviewKeyDown 确实有效，可以改进样式使其看起来像 PowerToys：

```xml
<TextBox
    x:Name="HotkeyInputBox"
    IsReadOnly="True"
    TextAlignment="Center"
    FontSize="16"
    MinHeight="60"
    Background="{ThemeResource ControlFillColorDefaultBrush}"
    BorderBrush="{ThemeResource ControlStrokeColorDefaultBrush}"
    PreviewKeyDown="OnHotkeyInputBoxPreviewKeyDown"
    GotFocus="OnHotkeyInputBoxGotFocus"
    LostFocus="OnHotkeyInputBoxLostFocus">
    <TextBox.Template>
        <ControlTemplate TargetType="TextBox">
            <Border
                Background="{TemplateBinding Background}"
                BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="1"
                CornerRadius="8"
                Padding="16">
                <TextBlock
                    Text="{TemplateBinding Text}"
                    TextAlignment="Center"
                    VerticalAlignment="Center"
                    Foreground="{TemplateBinding Foreground}"/>
            </Border>
        </ControlTemplate>
    </TextBox.Template>
</TextBox>
```

## 结论

PowerToys 使用低级键盘钩子是因为他们需要：
1. 捕获所有按键（包括系统快捷键）
2. 实现复杂的按键重映射
3. 支持和弦键
4. 不受控件焦点限制

对于我们的简单快捷键设置场景：
- **推荐使用 ToggleButton + PreviewKeyDown**
- 不需要低级键盘钩子
- 保持代码简单和可维护性
- 提供良好的用户体验

如果 PreviewKeyDown 在 WinUI 3 中不工作，再考虑使用低级键盘钩子。
