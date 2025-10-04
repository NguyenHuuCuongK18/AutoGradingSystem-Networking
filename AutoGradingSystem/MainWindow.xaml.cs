using System.Windows;
using AutoGradingSystem.ViewModels;

namespace AutoGradingSystem
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new GradingViewModel();
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is GradingViewModel vm)
            {
                vm.Cleanup();
            }
        }
    }
}