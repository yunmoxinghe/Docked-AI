using Microsoft.UI.Xaml;
using Docked_AI.Features.MainWindow;
using Docked_AI.Features.MainWindow.Host;

namespace Docked_AI
{
    public sealed partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly WindowHostController _windowController;

        public bool IsWindowVisible => _viewModel.IsWindowVisible;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainWindowViewModel();
            if (Content is FrameworkElement rootElement)
            {
                rootElement.DataContext = _viewModel;
            }

            _windowController = new WindowHostController(this, _viewModel);
        }

        public void ToggleWindow()
        {
            _windowController.ToggleWindow();
        }
    }
}
