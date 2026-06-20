using Docked_AI.Features.Pages.WebApp.Shared;
using Docked_AI.Features.AppEntry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace Docked_AI.Features.Pages.Settings.Experimental
{
    /// <summary>
    /// 数据管理页面（用于导出/导入网页应用数据）
    /// </summary>
    public sealed partial class DataManagementPage : Page
    {
        public DataManagementPage()
        {
            InitializeComponent();
            LoadDataSize();
        }

        private async void LoadDataSize()
        {
            var sizeInfo = WebAppDataExporter.GetDataSize();
            DataSizeText.Text = $"当前数据大小: {sizeInfo.TotalSizeFormatted} ({sizeInfo.FileCount} 个文件)";
            DataPathText.Text = $"存储位置: {sizeInfo.LocalStatePath}";
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportButton.IsEnabled = false;
                ExportStatusText.Text = "正在导出...";

                // 使用文件保存选择器
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = $"边栏助手_备份_{DateTime.Now:yyyyMMdd_HHmmss}"
                };
                savePicker.FileTypeChoices.Add("备份文件", new[] { ".zip" });

                // 获取窗口句柄（WinUI 3 必需）
                var app = (App)Application.Current;
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(app.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    bool success = await WebAppDataExporter.ExportDataAsync(file.Path);
                    
                    if (success)
                    {
                        ExportStatusText.Text = $"✅ 导出成功！文件已保存到: {file.Path}";
                        ExportStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Green
                        );
                    }
                    else
                    {
                        ExportStatusText.Text = "❌ 导出失败，请查看日志";
                        ExportStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Red
                        );
                    }
                }
                else
                {
                    ExportStatusText.Text = "已取消导出";
                }
            }
            catch (Exception ex)
            {
                ExportStatusText.Text = $"❌ 错误: {ex.Message}";
                ExportStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Red
                );
            }
            finally
            {
                ExportButton.IsEnabled = true;
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ImportButton.IsEnabled = false;
                ImportStatusText.Text = "正在导入...";

                // 使用文件打开选择器
                var openPicker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };
                openPicker.FileTypeFilter.Add(".zip");

                // 获取窗口句柄（WinUI 3 必需）
                var app = (App)Application.Current;
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(app.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);

                var file = await openPicker.PickSingleFileAsync();
                if (file != null)
                {
                    bool overwrite = OverwriteModeCheckBox.IsChecked == true;
                    bool success = await WebAppDataExporter.ImportDataAsync(file.Path, overwrite);
                    
                    if (success)
                    {
                        ImportStatusText.Text = overwrite 
                            ? "✅ 导入成功！数据已覆盖，请重启应用生效" 
                            : "✅ 导入成功！数据已合并，请重启应用生效";
                        ImportStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Green
                        );
                        
                        LoadDataSize(); // 刷新数据大小
                    }
                    else
                    {
                        ImportStatusText.Text = "❌ 导入失败，请查看日志";
                        ImportStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Red
                        );
                    }
                }
                else
                {
                    ImportStatusText.Text = "已取消导入";
                }
            }
            catch (Exception ex)
            {
                ImportStatusText.Text = $"❌ 错误: {ex.Message}";
                ImportStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Red
                );
            }
            finally
            {
                ImportButton.IsEnabled = true;
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadDataSize();
        }
    }

    // XAML 部分（内联定义）
    public sealed partial class DataManagementPage
    {
        private void InitializeComponent()
        {
            var root = new StackPanel
            {
                Spacing = 16,
                Padding = new Thickness(24)
            };

            // 标题
            var title = new TextBlock
            {
                Text = "数据管理",
                Style = (Style)Application.Current.Resources["TitleTextBlockStyle"]
            };

            // 数据大小信息
            var infoPanel = new StackPanel { Spacing = 8 };
            DataSizeText = new TextBlock { Text = "正在加载..." };
            DataPathText = new TextBlock 
            { 
                Text = "",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7
            };
            var refreshButton = new Button
            {
                Content = "🔄 刷新",
                Margin = new Thickness(0, 8, 0, 0)
            };
            refreshButton.Click += RefreshButton_Click;
            
            infoPanel.Children.Add(DataSizeText);
            infoPanel.Children.Add(DataPathText);
            infoPanel.Children.Add(refreshButton);

            // 导出部分
            var exportCard = new Expander
            {
                Header = "📤 导出数据",
                IsExpanded = true,
                Margin = new Thickness(0, 16, 0, 0)
            };
            var exportContent = new StackPanel { Spacing = 12, Padding = new Thickness(16) };
            exportContent.Children.Add(new TextBlock
            {
                Text = "将网站快捷方式和图标缓存导出为备份文件",
                TextWrapping = TextWrapping.Wrap
            });
            ExportButton = new Button { Content = "选择导出位置..." };
            ExportButton.Click += ExportButton_Click;
            exportContent.Children.Add(ExportButton);
            ExportStatusText = new TextBlock 
            { 
                Text = "",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };
            exportContent.Children.Add(ExportStatusText);
            exportCard.Content = exportContent;

            // 导入部分
            var importCard = new Expander
            {
                Header = "📥 导入数据",
                IsExpanded = false,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var importContent = new StackPanel { Spacing = 12, Padding = new Thickness(16) };
            importContent.Children.Add(new TextBlock
            {
                Text = "从备份文件恢复网站快捷方式和图标",
                TextWrapping = TextWrapping.Wrap
            });
            OverwriteModeCheckBox = new CheckBox
            {
                Content = "覆盖模式（勾选则替换现有数据，不勾选则合并）"
            };
            importContent.Children.Add(OverwriteModeCheckBox);
            ImportButton = new Button { Content = "选择备份文件..." };
            ImportButton.Click += ImportButton_Click;
            importContent.Children.Add(ImportButton);
            ImportStatusText = new TextBlock 
            { 
                Text = "",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };
            importContent.Children.Add(ImportStatusText);
            importCard.Content = importContent;

            // 组装
            root.Children.Add(title);
            root.Children.Add(infoPanel);
            root.Children.Add(exportCard);
            root.Children.Add(importCard);

            Content = new ScrollViewer
            {
                Content = root
            };
        }

        private TextBlock DataSizeText = null!;
        private TextBlock DataPathText = null!;
        private Button ExportButton = null!;
        private TextBlock ExportStatusText = null!;
        private Button ImportButton = null!;
        private CheckBox OverwriteModeCheckBox = null!;
        private TextBlock ImportStatusText = null!;
    }
}
