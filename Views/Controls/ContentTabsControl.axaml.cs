using Avalonia.Controls;
using Avalonia.Interactivity;
using FileViewerApp.Models;
using FileViewerApp.ViewModels;

namespace FileViewerApp.Views.Controls;

public partial class ContentTabsControl : UserControl
{
    public ContentTabsControl()
    {
        InitializeComponent();
    }

    private void OnInstructionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb &&
            cb.Tag is EditableInstruction instr &&
            cb.SelectedItem is string newName &&
            DataContext is MainWindowViewModel vm)
        {
            vm.OnInstructionNameChanged(instr, newName);
        }
    }
}