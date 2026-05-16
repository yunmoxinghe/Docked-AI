# 第三方源码集成说明

本目录用于存放从第三方开源项目集成的源代码，以减小应用程序包体积。

## 📦 当前集成状态

### ✅ 已完成集成

#### 1. KeyVisual (自定义实现)
- **来源**: 自定义实现（参考 DevWinUI）
- **位置**: `功能/统一调用/自定义控件/KeyVisual.xaml`
- **用途**: 显示键盘按键的可视化表示
- **体积节省**: 约 0.5-1 MB（移除 DevWinUI.Controls 包）

### ⏸️ 保留 NuGet 包

#### 2. CommunityToolkit.WinUI.Controls.SettingsControls
- **版本**: 8.2.251219
- **原因**: 依赖关系复杂（需要 ControlSizeTrigger、IsEqualStateTrigger 等组件）
- **体积**: 约 1-2 MB
- **决策**: 保持 NuGet 引用，避免过度复杂化

#### 3. DevWinUI (SystemTrayIcon)
- **版本**: 9.9.4
- **原因**: SystemTrayIcon 实现复杂，涉及 Win32 API 互操作
- **体积**: 约 2-4 MB
- **决策**: 暂时保持 NuGet 引用，未来可考虑集成

#### 4. NHotkey.WinUI
- **版本**: 3.0.1
- **原因**: 全局快捷键功能稳定，体积小
- **体积**: 约 0.5-1 MB
- **决策**: 保持 NuGet 引用

## 📊 体积优化结果

### 当前优化
- ✅ 移除 DevWinUI.Controls：节省约 1-2 MB
- ✅ 使用自定义 KeyVisual：代码更简洁

### 预期总体积
- 原始包体积：8-12 MB
- 优化后预期：7-10 MB
- **实际节省**：约 1-2 MB

## 🔄 未来优化计划

如果需要进一步减小体积，可以考虑：

1. **集成 SystemTrayIcon 源码**
   - 从 DevWinUI 提取 SystemTrayIcon 相关代码
   - 预计节省：2-4 MB
   - 难度：⭐⭐⭐⭐（涉及 Win32 API）

2. **集成 CommunityToolkit Triggers**
   - 提取 ControlSizeTrigger、IsEqualStateTrigger 等组件
   - 然后集成 SettingsControls 源码
   - 预计节省：1-2 MB
   - 难度：⭐⭐⭐（依赖关系复杂）

3. **自定义实现 NHotkey**
   - 使用 P/Invoke 直接调用 RegisterHotKey API
   - 预计节省：0.5-1 MB
   - 难度：⭐⭐⭐⭐⭐（需要处理消息循环）

## 📄 许可证

### DevWinUI (MIT License)

```
MIT License

Copyright (c) 2023 Mahdi Hosseini

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## ⚠️ 注意事项

1. **保留许可证**: 所有集成的源码都保留了原始许可证信息
2. **最小化修改**: 尽量不修改源码，只做必要的命名空间调整
3. **版本记录**: 记录了源码的来源版本，便于后续更新
4. **测试充分**: 每集成一个组件都进行了完整测试

## 🎯 实用建议

对于大多数项目，当前的优化策略（只移除 DevWinUI.Controls）已经足够：
- ✅ 实现简单，风险低
- ✅ 维护成本低
- ✅ 有一定的体积优化效果

如果你的项目对包体积有极致要求（如需要降到 5MB 以下），再考虑进一步集成其他组件的源码。
