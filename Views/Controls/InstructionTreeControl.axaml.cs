using Avalonia.Controls;
using System;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using FileViewerApp.Models;
using FileViewerApp.ViewModels;

namespace FileViewerApp.Views.Controls
{
    public partial class InstructionTreeControl : UserControl
    {
        private DateTime? _lastClickTime;
        public InstructionTreeControl()
        {
            InitializeComponent();
            this.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // React only on double click
            // Avalonia: double click detection via timestamp + IsLeftButtonPressed count workaround
            // Simpler approach: treat any rapid second press within 300ms on same node as double click.
            if (!_lastClickTime.HasValue || (DateTime.UtcNow - _lastClickTime.Value).TotalMilliseconds > 300)
            {
                _lastClickTime = DateTime.UtcNow;
                return; // first click ignored
            }
            _lastClickTime = null; // consume double click

            if (DataContext is MainWindowViewModel vm)
            {
                var node = vm.SelectedNode;
                if (node == null) return;
                var match = vm.EditableInstructions.FirstOrDefault(i => i.Number == node.InstructionNumber);
                if (match != null && !ReferenceEquals(vm.SelectedInstruction, match))
                {
                    vm.SelectedInstruction = match; // Scroll handled by ContentTabsControl
                }
            }
        }
    }
}