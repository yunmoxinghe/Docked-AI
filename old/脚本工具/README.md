# 脚本工具

本目录包含项目开发和维护过程中使用的独立脚本工具。这些脚本使用 .NET 10 的 **File-based apps** 特性，可以直接运行单个 `.cs` 文件，无需创建项目文件。

## 📋 可用工具

### 1. 图标调整工具 (`图标调整工具.cs`)

**功能**：批量调整 SVG 图标文件的尺寸

**用途**：将 `Assets/logos` 目录中的所有 SVG 图标统一调整为指定尺寸（默认 100×100 像素）

**运行方式**：

```bash
# 方式1：使用 --file 选项（推荐）
dotnet run --file 脚本工具\图标调整工具.cs

# 方式2：直接指定文件
dotnet run 脚本工具\图标调整工具.cs

# 方式3：简写形式
dotnet 脚本工具\图标调整工具.cs
```

**工作原理**：
- 扫描指定目录中的所有 `.svg` 文件
- 使用正则表达式修改 SVG 文件的 `width` 和 `height` 属性
- 如果 SVG 没有这些属性，会自动添加
- 保留原始的 `viewBox` 属性以保持图标比例

**处理结果**：
```
✅ 已处理: adsterra.svg
✅ 已处理: Alipay.svg
✅ 已处理: monetag.svg
✅ 已处理: WeChat.svg

批量调整完成！共处理 4 个文件。
```

---

## 🚀 关于 .NET 10 File-based Apps

### 什么是 File-based Apps？

File-based apps 是 .NET 10 Preview 4 引入的新特性，允许开发者直接运行单个 C# 文件，无需创建传统的 `.csproj` 项目文件。

### 核心优势

✅ **零配置运行** - 无需创建项目文件  
✅ **轻量级脚本** - 适合工具、脚本和小型应用  
✅ **包管理** - 通过 `#:package` 指令添加 NuGet 包  
✅ **Native AOT** - 默认支持原生编译，启动快速  

### 使用方法

#### 基本运行

```bash
# 运行单个 C# 文件
dotnet run --file script.cs

# 传递参数
dotnet run --file script.cs -- arg1 arg2

# 简写形式
dotnet script.cs
```

#### 添加 NuGet 包

在 `.cs` 文件顶部添加包引用：

```csharp
#:package Newtonsoft.Json@13.0.1
#:package Spectre.Console@*

using Newtonsoft.Json;
using Spectre.Console;

// 你的代码...
```

#### 设置属性

```csharp
#:property TargetFramework=net10.0
#:property PublishAot=false
```

#### 引用其他文件

```csharp
#:include helpers.cs
#:include models/**/*.cs
```

#### 指定 SDK

```csharp
#:sdk Microsoft.NET.Sdk.Web
```

### 其他命令

```bash
# 编译
dotnet build script.cs

# 发布为可执行文件
dotnet publish script.cs

# 打包为 .NET 工具
dotnet pack script.cs

# 转换为传统项目
dotnet project convert script.cs

# 清理构建输出
dotnet clean script.cs
```

### Shell 直接执行（Unix/Linux）

在文件顶部添加 shebang：

```csharp
#!/usr/bin/env -S dotnet --
#:package Spectre.Console

using Spectre.Console;
AnsiConsole.MarkupLine("[green]Hello![/]");
```

赋予执行权限并运行：

```bash
chmod +x script.cs
./script.cs
```

### 注意事项

⚠️ **项目文件优先级** - 如果当前目录包含 `.csproj` 文件，需要使用 `--file` 选项明确指定运行单文件应用

⚠️ **构建缓存** - SDK 会缓存构建输出以提高性能，如需清除缓存：
```bash
dotnet clean file-based-apps
```

⚠️ **并发运行** - 同时运行多个实例可能导致文件冲突，建议先构建：
```bash
dotnet build script.cs
dotnet run script.cs --no-build
```

---

## 📚 参考资源

- [Microsoft Learn - File-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps)
- [Microsoft DevBlogs - A simpler way to start with C#](https://devblogs.microsoft.com/dotnet/announcing-dotnet-run-app/)
- [Andrew Lock - Exploring dotnet run app.cs](https://andrewlock.net/exploring-dotnet-10-preview-features-1-exploring-the-dotnet-run-app.cs/)

---

## 🛠️ 创建新工具

如需创建新的脚本工具，请遵循以下步骤：

1. **创建 `.cs` 文件**
   ```csharp
   using System;
   
   Console.WriteLine("Hello from script!");
   ```

2. **添加必要的包引用**（可选）
   ```csharp
   #:package PackageName@version
   ```

3. **运行测试**
   ```bash
   dotnet run --file 脚本工具\your-script.cs
   ```

4. **更新本 README**，添加工具说明

---

**最后更新日期**：2026年7月5日
