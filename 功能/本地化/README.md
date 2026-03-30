# 多语言支持 (Localization)

本应用支持中文和英文两种语言。

## 文件结构

```
功能/本地化/
├── Strings/
│   ├── zh-CN/
│   │   └── Resources.resw    # 中文资源文件
│   └── en-US/
│       └── Resources.resw    # 英文资源文件
├── LocalizationHelper.cs      # 本地化辅助类
├── README.md                  # 本文档
└── 多语言支持使用指南.md      # 详细使用指南
```

## 使用方法

### 在 XAML 中使用

使用 `x:Uid` 属性绑定资源：

```xml
<!-- 简单文本 -->
<TextBlock x:Uid="HomePage_Title" Text="主页" />

<!-- 带多个属性的控件 -->
<controls:SettingsCard x:Uid="SettingsPage_LanguageHeader"
                      Header="语言"
                      Description="选择应用显示语言" />
```

资源文件中对应的定义：
```xml
<!-- 简单文本 -->
<data name="HomePage_Title" xml:space="preserve">
  <value>主页</value>
</data>

<!-- 多属性控件 -->
<data name="SettingsPage_LanguageHeader.Header" xml:space="preserve">
  <value>语言</value>
</data>
<data name="SettingsPage_LanguageHeader.Description" xml:space="preserve">
  <value>选择应用显示语言</value>
</data>
```

### 在 C# 代码中使用

使用 `LocalizationHelper` 类：

```csharp
using Docked_AI.Features.Localization;

string text = LocalizationHelper.GetString("TrayMenu_OpenWindow");
```

## 添加新的本地化字符串

1. 在 `功能/本地化/Strings/zh-CN/Resources.resw` 中添加中文资源
2. 在 `功能/本地化/Strings/en-US/Resources.resw` 中添加对应的英文资源
3. 使用相同的 `name` 属性作为资源键

示例：

```xml
<data name="MyNewString" xml:space="preserve">
  <value>我的新字符串</value>
</data>
```

## 切换语言

用户可以在设置页面中选择语言，更改后需要重启应用生效。

## 当前支持的语言

- 简体中文 (zh-CN)
- English (en-US)

---

## 常见问题与解决方案

### 1. XAML 语法错误：名称中不能包含":"字符

**错误信息：**
```
Xaml Xml Parsing Error: 名称中不能包含":"字符(十六进制值 0x3A)
```

**原因：** 使用了不支持的 `x:Uid:PropertyName` 语法

**错误示例：**
```xml
<controls:SettingsCard x:Uid="MyCard"
                      x:Uid:Description="MyDescription" />  <!-- ❌ 错误 -->
```

**正确做法：** 使用 `x:Uid值.属性名` 格式在资源文件中定义
```xml
<!-- XAML 中只需要一个 x:Uid -->
<controls:SettingsCard x:Uid="MyCard"
                      Header="默认标题"
                      Description="默认描述" />

<!-- 资源文件中 -->
<data name="MyCard.Header" xml:space="preserve">
  <value>本地化标题</value>
</data>
<data name="MyCard.Description" xml:space="preserve">
  <value>本地化描述</value>
</data>
```

### 2. 项目构建错误：重复的 PRIResource 项 (NETSDK1022)

**错误信息：**
```
NETSDK1022: 包含了重复的"PRIResource"项。.NET SDK 默认包含你项目目录中的"PRIResource"项。
```

**原因：** .NET SDK 会自动包含项目中的所有 `.resw` 文件，手动添加会导致重复

**错误示例：**
```xml
<!-- ❌ 不需要手动添加 -->
<ItemGroup>
  <PRIResource Include="功能\本地化\Strings\**\*.resw" />
</ItemGroup>
```

**正确做法：** 完全移除手动添加的 PRIResource 项，让 SDK 自动处理
```xml
<!-- ✅ 什么都不需要添加，SDK 会自动识别 -->
```

**注意：** 如果确实需要手动控制，可以设置：
```xml
<PropertyGroup>
  <EnableDefaultPriItems>false</EnableDefaultPriItems>
</PropertyGroup>
```

### 3. 运行时错误：XamlRoot 未初始化 (ArgumentException)

**错误信息：**
```
System.ArgumentException: This element does not have a XamlRoot.
```

**原因：** 
- 在页面初始化时触发了事件，但 `XamlRoot` 还未初始化
- `ContentDialog` 等控件需要 `XamlRoot` 才能显示

**问题场景：**
```csharp
public SettingsPage()
{
    InitializeComponent();
    LoadLanguageSettings();  // 这里会触发 SelectionChanged 事件
}

private void LoadLanguageSettings()
{
    LanguageComboBox.SelectedItem = item;  // 触发事件，但 XamlRoot 还未初始化
}

private async void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
{
    var dialog = new ContentDialog { XamlRoot = this.XamlRoot };  // ❌ XamlRoot 为 null
    await dialog.ShowAsync();
}
```

**解决方案 1：** 在初始化时暂时取消事件订阅
```csharp
private void LoadLanguageSettings()
{
    // 暂时取消事件订阅
    LanguageComboBox.SelectionChanged -= OnLanguageChanged;
    
    LanguageComboBox.SelectedItem = item;
    
    // 重新订阅事件
    LanguageComboBox.SelectionChanged += OnLanguageChanged;
}
```

**解决方案 2：** 在事件处理中检查 XamlRoot
```csharp
private async void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
{
    // 确保 XamlRoot 已初始化
    if (this.XamlRoot == null)
    {
        return;
    }
    
    var dialog = new ContentDialog { XamlRoot = this.XamlRoot };
    await dialog.ShowAsync();
}
```

**最佳实践：** 同时使用两种方案，确保万无一失

### 4. ResourceLoader 找不到资源

**问题：** 使用 `LocalizationHelper.GetString()` 返回的是键名而不是翻译文本

**原因：** ResourceLoader 的路径配置不正确

**检查清单：**
1. 确认资源文件路径是否正确
2. 确认 ResourceLoader 初始化时的路径参数

```csharp
// 如果资源文件在 功能/本地化/Strings/Resources.resw
private static readonly ResourceLoader _resourceLoader = new("功能/本地化/Strings/Resources");

// 如果资源文件在项目根目录的 Strings/Resources.resw
private static readonly ResourceLoader _resourceLoader = new();  // 使用默认路径
```

3. 确认资源文件的生成操作设置为 `PRIResource`（通常自动设置）

### 5. 语言切换后部分文本未更新

**原因：** 
- 动态创建的 UI 元素没有使用本地化资源
- 某些文本是硬编码的

**解决方案：**
1. 检查所有硬编码的文本，改用资源文件
2. 对于动态创建的 UI，使用 `LocalizationHelper.GetString()` 获取文本
3. 确保所有 XAML 中的文本都使用了 `x:Uid`

## 开发建议

1. **命名规范：** 使用 `区域_元素_属性` 格式，如 `SettingsPage_LanguageHeader.Description`
2. **保持同步：** 添加新字符串时，同时更新所有语言的资源文件
3. **测试：** 切换语言后测试所有页面，确保文本正确显示
4. **回退机制：** `LocalizationHelper` 已实现，找不到资源时返回键名
5. **避免硬编码：** 所有用户可见的文本都应该使用资源文件

## 参考资料

- [WinUI 3 本地化文档](https://learn.microsoft.com/windows/apps/windows-app-sdk/localize-strings)
- [.resw 文件格式说明](https://learn.microsoft.com/windows/uwp/app-resources/localize-strings-ui-manifest)
- 详细使用指南：`多语言支持使用指南.md`
