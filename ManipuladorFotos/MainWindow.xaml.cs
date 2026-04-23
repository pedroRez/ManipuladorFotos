using System.Windows;
using ManipuladorFotos.ViewModels;

namespace ManipuladorFotos;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
