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
        }
    }
}