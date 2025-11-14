using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
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
            if (this.FindControl<ListBox>("InstructionsList") is ListBox list)
            {
                list.SelectionChanged += (s, e) =>
                {
                    var sel = list.SelectedItems;
                    var collected = new System.Collections.Generic.List<EditableInstruction>();
                    if (sel != null)
                    {
                        foreach (var item in sel)
                        {
                            if (item is EditableInstruction ei) collected.Add(ei);
                        }
                    }
                    vm.UpdateSelectedInstructions(collected);
                };
                list.KeyDown += (s, e) =>
                {
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    {
                        if (e.Key == Key.C)
                        {
                            if (vm.CopyInstructionsCommand.CanExecute(null)) vm.CopyInstructionsCommand.Execute(null);
                            e.Handled = true;
                        }
                        else if (e.Key == Key.X)
                        {
                            if (vm.CutInstructionsCommand.CanExecute(null)) vm.CutInstructionsCommand.Execute(null);
                            e.Handled = true;
                        }
                        else if (e.Key == Key.V)
                        {
                            if (vm.PasteInstructionsCommand.CanExecute(null)) vm.PasteInstructionsCommand.Execute(null);
                            e.Handled = true;
                        }
                    }
                };
            }
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