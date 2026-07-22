---
name: winui3-development
description: Expert guidance for WinUI 3 desktop application development with .NET, XAML, DevWinUI, and Windows App SDK. Covers MVVM patterns, custom controls, theming, navigation, and modern Windows UI best practices.
keywords: [winui3, winui, xaml, windows app sdk, devwinui, uwp, windows desktop, c#, dotnet, mvvm, windows 11, windows 10]
compatibility: [.NET 8+, Windows App SDK 1.5+, WinUI 3, DevWinUI 9.x]
---

# WinUI 3 Development Skill

Expert knowledge for building modern Windows desktop applications with WinUI 3, .NET, and XAML.

## USE FOR

- Building WinUI 3 desktop applications with .NET 8+
- XAML UI design and layout with WinUI 3 controls
- DevWinUI component library integration (9.x)
- MVVM pattern implementation in WinUI 3
- Custom control development
- Windows App SDK features (notifications, file pickers, system tray)
- Theme and styling (Fluent Design, dark/light modes)
- Navigation patterns (NavigationView, Frame)
- Data binding and INotifyPropertyChanged
- Window management and lifecycle
- Packaging and deployment (MSIX)

## DO NOT USE FOR

- UWP applications (use legacy UWP guidance instead)
- WPF applications (different framework)
- Web applications or Blazor
- Cross-platform mobile apps (use MAUI instead)
- Windows Forms applications

## Core Concepts

### Project Structure
```
YourApp/
├── App.xaml / App.xaml.cs          # Application entry point
├── MainWindow.xaml / .xaml.cs      # Main window
├── Views/                          # XAML pages
├── ViewModels/                     # MVVM view models
├── Models/                         # Data models
├── Services/                       # Business logic
├── Controls/                       # Custom controls
├── Assets/                         # Images, icons
└── Package.appxmanifest            # App manifest
```

### Essential NuGet Packages
- `Microsoft.WindowsAppSDK` - Windows App SDK
- `Microsoft.Windows.SDK.BuildTools` - Build tools
- `CommunityToolkit.Mvvm` - MVVM helpers
- `DevWinUI` - Modern UI component library

### XAML Namespace Declarations
```xml
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:controls="using:Microsoft.UI.Xaml.Controls"
    xmlns:dev="using:DevWinUI"
    mc:Ignorable="d">
```

## Common Patterns

### 1. MVVM with CommunityToolkit.Mvvm

**ViewModel:**
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "Hello WinUI 3";
    
    [ObservableProperty]
    private bool isLoading;
    
    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            // Load data
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

**View (XAML):**
```xml
<Page x:Class="YourApp.Views.MainPage"
      DataContext="{Binding MainViewModel, Source={StaticResource ViewModelLocator}}">
    <StackPanel Spacing="8">
        <TextBlock Text="{x:Bind ViewModel.Title, Mode=OneWay}" 
                   Style="{StaticResource TitleTextBlockStyle}"/>
        <Button Content="Load Data" 
                Command="{x:Bind ViewModel.LoadDataCommand}"/>
        <ProgressRing IsActive="{x:Bind ViewModel.IsLoading, Mode=OneWay}"/>
    </StackPanel>
</Page>
```

### 2. Navigation with NavigationView

```xml
<NavigationView x:Name="NavView"
                IsBackButtonVisible="Visible"
                SelectionChanged="NavView_SelectionChanged">
    <NavigationView.MenuItems>
        <NavigationViewItem Icon="Home" Content="主页" Tag="HomePage"/>
        <NavigationViewItem Icon="Setting" Content="设置" Tag="SettingsPage"/>
    </NavigationView.MenuItems>
    
    <Frame x:Name="ContentFrame"/>
</NavigationView>
```

```csharp
private void NavView_SelectionChanged(NavigationView sender, 
    NavigationViewSelectionChangedEventArgs args)
{
    if (args.SelectedItem is NavigationViewItem item)
    {
        var pageType = item.Tag switch
        {
            "HomePage" => typeof(HomePage),
            "SettingsPage" => typeof(SettingsPage),
            _ => null
        };
        
        if (pageType != null)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
```

### 3. DevWinUI Components

```xml
<!-- DevWinUI Card -->
<dev:Card Header="卡片标题" 
          Description="卡片描述">
    <StackPanel>
        <TextBlock Text="内容"/>
    </StackPanel>
</dev:Card>

<!-- DevWinUI Settings Card -->
<dev:SettingsCard Header="设置项"
                  Description="设置描述"
                  Icon="{dev:FontIcon Glyph=&#xE713;}">
    <ToggleSwitch/>
</dev:SettingsCard>
```

### 4. Theme and Styling

```csharp
// App.xaml.cs - Set theme
public App()
{
    this.InitializeComponent();
    
    // Set theme
    if (Content is FrameworkElement rootElement)
    {
        rootElement.RequestedTheme = ElementTheme.Dark; // or Light, Default
    }
}
```

```xml
<!-- Custom styles in App.xaml -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls"/>
            <ResourceDictionary Source="/Styles/CustomStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
        
        <SolidColorBrush x:Key="CustomAccentBrush" Color="#0078D4"/>
    </ResourceDictionary>
</Application.Resources>
```

### 5. Window Management

```csharp
// MainWindow.xaml.cs
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        
        // Set window size
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        
        appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
        
        // Set title bar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
    }
}
```

### 6. System Tray Integration

```csharp
using Microsoft.UI.Xaml;
using H.NotifyIcon;

public sealed partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Create tray icon
        _trayIcon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/icon.ico")),
            ToolTipText = "My App"
        };
        
        _trayIcon.LeftClickCommand = new RelayCommand(ShowMainWindow);
    }
}
```

### 7. File Picker

```csharp
using Windows.Storage.Pickers;
using WinRT.Interop;

private async Task<StorageFile?> PickFileAsync()
{
    var picker = new FileOpenPicker();
    picker.FileTypeFilter.Add(".txt");
    picker.FileTypeFilter.Add(".json");
    
    // Initialize with window handle
    var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
    InitializeWithWindow.Initialize(picker, hWnd);
    
    return await picker.PickSingleFileAsync();
}
```

## DevWinUI 9.9.3 Specific Features

### Common DevWinUI Controls
- `dev:Card` - Modern card container
- `dev:SettingsCard` - Settings UI pattern
- `dev:SettingsExpander` - Expandable settings group
- `dev:InfoBar` - Notification banner
- `dev:Shield` - Badge/label component
- `dev:TitleBar` - Custom title bar
- `dev:NavigationView` - Enhanced navigation

### DevWinUI Theming
```csharp
// Apply DevWinUI theme
DevWinUI.ThemeManager.Current.ApplicationTheme = 
    DevWinUI.ApplicationTheme.Dark;
```

## Best Practices

1. **Use x:Bind over Binding** - Better performance with compile-time checking
2. **Implement INotifyPropertyChanged** - Use CommunityToolkit.Mvvm source generators
3. **Async/Await** - Always use async for I/O operations
4. **Resource Management** - Dispose of resources properly
5. **Accessibility** - Set AutomationProperties for screen readers
6. **Localization** - Use .resw files for multi-language support
7. **Performance** - Use virtualization for large lists (ListView, GridView)

## Common Gotchas

- **Window Handle Required** - Many Windows APIs need HWND (use WindowNative.GetWindowHandle)
- **Thread Affinity** - UI updates must be on UI thread (use DispatcherQueue)
- **Package Identity** - Some features require packaged app (MSIX)
- **Namespace Changes** - WinUI 3 uses Microsoft.UI.Xaml, not Windows.UI.Xaml

## Resources

- [WinUI 3 Documentation](https://learn.microsoft.com/windows/apps/winui/winui3/)
- [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
- [DevWinUI GitHub](https://github.com/ghost1372/DevWinUI)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
- [WinUI 3 Gallery](https://github.com/microsoft/WinUI-Gallery)

## Example: Complete MVVM Page

**ViewModel:**
```csharp
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isDarkMode = true;
    
    [ObservableProperty]
    private string appVersion = "1.0.0";
    
    partial void OnIsDarkModeChanged(bool value)
    {
        // Apply theme change
        if (App.Current.Content is FrameworkElement root)
        {
            root.RequestedTheme = value ? ElementTheme.Dark : ElementTheme.Light;
        }
    }
}
```

**View:**
```xml
<Page x:Class="YourApp.Views.SettingsPage"
      xmlns:dev="using:DevWinUI">
    <ScrollViewer>
        <StackPanel Spacing="4" Margin="20">
            <TextBlock Text="设置" 
                       Style="{StaticResource TitleTextBlockStyle}"/>
            
            <dev:SettingsCard Header="深色模式"
                              Description="切换应用主题"
                              Icon="{dev:FontIcon Glyph=&#xE771;}">
                <ToggleSwitch IsOn="{x:Bind ViewModel.IsDarkMode, Mode=TwoWay}"/>
            </dev:SettingsCard>
            
            <dev:SettingsCard Header="版本"
                              Description="{x:Bind ViewModel.AppVersion, Mode=OneWay}"
                              Icon="{dev:FontIcon Glyph=&#xE946;}"/>
        </StackPanel>
    </ScrollViewer>
</Page>
```

---

**Remember:** WinUI 3 is the modern UI framework for Windows desktop apps. Use Windows App SDK features, follow MVVM patterns, and leverage DevWinUI for enhanced UI components.
