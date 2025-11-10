using Avalonia.Controls;
using Avalonia.Interactivity;
using FileViewerApp.Models;
using FileViewerApp.ViewModels;
using FileViewerApp.ViewModels.Controls;

namespace FileViewerApp.Views.Controls;

public partial class ContentTabsControl : UserControl
{
    public ContentTabsControl()
    {
        InitializeComponent();
        this.AttachedToVisualTree += (_, __) => HookSelectionChanged();
    }

    private void OnInstructionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb &&
            cb.Tag is EditableInstruction instr &&
            cb.SelectedItem is string newName)
        {
            // DataContext is the control ViewModel (ContentTabsControlViewModel)
            if (DataContext is ContentTabsControlViewModel vm && vm.Parent != null)
            {
                vm.Parent.OnInstructionNameChanged(instr, newName);
            }
            else if (DataContext is MainWindowViewModel mainVm)
            {
                // fallback for older wiring
                mainVm.OnInstructionNameChanged(instr, newName);
            }
        }
    }

    private void HookSelectionChanged()
    {
        if (DataContext is ContentTabsControlViewModel vm)
        {
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.SelectedInstruction))
                {
                    ScrollToSelected(vm.SelectedInstruction);
                }
            };
        }
    }

    private void ScrollToSelected(EditableInstruction? instr)
    {
        if (instr == null) return;
        if (this.FindControl<ListBox>("InstructionsList") is ListBox list)
        {
            list.ScrollIntoView(instr);
        }
    }
}