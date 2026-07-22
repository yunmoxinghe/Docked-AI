using DockedTools.Features.Pages.WebApp.Shared;
using DockedTools.Features.AppEntry;
using DockedTools.Features.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace DockedTools.Features.Pages.Settings.Experimental
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
            DataSizeText.Text = string.Format(LocalizationHelper.GetString("DataManagement_CurrentSize"), sizeInfo.TotalSizeFormatted, sizeInfo.FileCount);
            DataPathText.Text = string.Format(LocalizationHelper.GetString("DataManagement_StorageLocation"), sizeInfo.LocalStatePath);
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportButton.IsEnabled = false;
                ExportStatusText.Text = LocalizationHelper.GetString("DataManagement_Exporting");

                // 使用文件保存选择器
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = string.Format(LocalizationHelper.GetString("DataManagement_BackupFileNameFormat"), DateTime.Now.ToString("yyyyMMdd_HHmmss"))
                };
                savePicker.FileTypeChoices.Add(LocalizationHelper.GetString("DataManagement_BackupFileFilter"), new[] { ".zip" });

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
                        ExportStatusText.Text = string.Format(LocalizationHelper.GetString("DataManagement_ExportSuccess"), file.Path);
                        ExportStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Green
                        );
                    }
                    else
                    {
                        ExportStatusText.Text = LocalizationHelper.GetString("DataManagement_ExportFailed");
                        ExportStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Red
                        );
                    }
                }
                else
                {
                    ExportStatusText.Text = LocalizationHelper.GetString("DataManagement_ExportCancelled");
                }
            }
            catch (Exception ex)
            {
                ExportStatusText.Text = string.Format(LocalizationHelper.GetString("DataManagement_Error"), ex.Message);
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
                ImportStatusText.Text = LocalizationHelper.GetString("DataManagement_Importing");

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
                            ? LocalizationHelper.GetString("DataManagement_ImportSuccessOverwrite")
                            : LocalizationHelper.GetString("DataManagement_ImportSuccessMerge");
                        ImportStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Green
                        );
                        
                        LoadDataSize(); // 刷新数据大小
                    }
                    else
                    {
                        ImportStatusText.Text = LocalizationHelper.GetString("DataManagement_ImportFailed");
                        ImportStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                            Microsoft.UI.Colors.Red
                        );
                    }
                }
                else
                {
                    ImportStatusText.Text = LocalizationHelper.GetString("DataManagement_ImportCancelled");
                }
            }
            catch (Exception ex)
            {
                ImportStatusText.Text = string.Format(LocalizationHelper.GetString("DataManagement_Error"), ex.Message);
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
                Text = LocalizationHelper.GetString("DataManagement_Title"),
                Style = (Style)Application.Current.Resources["TitleTextBlockStyle"]
            };

            // 数据大小信息
            var infoPanel = new StackPanel { Spacing = 8 };
            DataSizeText = new TextBlock { Text = LocalizationHelper.GetString("DataManagement_Loading") };
            DataPathText = new TextBlock 
            { 
                Text = "",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7
            };
            var refreshButton = new Button
            {
                Content = LocalizationHelper.GetString("DataManagement_RefreshButton"),
                Margin = new Thickness(0, 8, 0, 0)
            };
            refreshButton.Click += RefreshButton_Click;
            
            infoPanel.Children.Add(DataSizeText);
            infoPanel.Children.Add(DataPathText);
            infoPanel.Children.Add(refreshButton);

            // 导出部分
            var exportCard = new Expander
            {
                Header = LocalizationHelper.GetString("DataManagement_ExportHeader"),
                IsExpanded = true,
                Margin = new Thickness(0, 16, 0, 0)
            };
            var exportContent = new StackPanel { Spacing = 12, Padding = new Thickness(16) };
            exportContent.Children.Add(new TextBlock
            {
                Text = LocalizationHelper.GetString("DataManagement_ExportDescription"),
                TextWrapping = TextWrapping.Wrap
            });
            ExportButton = new Button { Content = LocalizationHelper.GetString("DataManagement_ExportButton") };
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
                Header = LocalizationHelper.GetString("DataManagement_ImportHeader"),
                IsExpanded = false,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var importContent = new StackPanel { Spacing = 12, Padding = new Thickness(16) };
            importContent.Children.Add(new TextBlock
            {
                Text = LocalizationHelper.GetString("DataManagement_ImportDescription"),
                TextWrapping = TextWrapping.Wrap
            });
            OverwriteModeCheckBox = new CheckBox
            {
                Content = LocalizationHelper.GetString("DataManagement_OverwriteMode")
            };
            importContent.Children.Add(OverwriteModeCheckBox);
            ImportButton = new Button { Content = LocalizationHelper.GetString("DataManagement_ImportButton") };
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
