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
└── README.md                  # 本文档
```

## 使用方法

### 在 XAML 中使用

使用 `x:Uid` 属性绑定资源：

```xml
<TextBlock x:Uid="HomePage_Title" Text="主页" />
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
