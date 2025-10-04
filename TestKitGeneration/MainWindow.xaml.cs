using System.Windows;
using TestKitGeneration.ViewModels;

namespace TestKitGeneration
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new GeneratorViewModel();
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is GeneratorViewModel vm)
            {
                vm.Cleanup();
            }
        }
    }
}