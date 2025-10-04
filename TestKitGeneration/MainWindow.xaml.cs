using System.Windows;
using TestKitGeneration.ViewModels;

namespace TestKitGeneration
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// Hosts the current screen (Setup, View, or Generate) via ContentControl.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new GeneratorViewModel();
        }
    }
}