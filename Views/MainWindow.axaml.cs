using Avalonia.Controls;
using FileViewerApp.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace FileViewerApp.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainWindowViewModel();
            DataContext = viewModel;

            // Passa il riferimento della window al ViewModel
            viewModel.SetWindow(this);
        }
    }
}