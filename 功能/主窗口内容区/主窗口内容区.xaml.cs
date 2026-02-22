using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.MainWindowContent
{
    public sealed partial class MainWindowContent : UserControl
    {
        public MainWindowContent()
        {
            InitializeComponent();
            DataContext = new MainWindowContentViewModel();
        }
    }
}
