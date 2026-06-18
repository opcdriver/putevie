using System.Windows;
using Putevie.ViewModels;

namespace Putevie.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
