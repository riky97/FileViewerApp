using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using ReactiveUI;
using FileViewerApp.Models;
using FileViewerApp.ViewModels;

namespace FileViewerApp.ViewModels.Controls
{
    public class ContentTabsControlViewModel : ViewModelBase, IDisposable
    {
        private readonly MainWindowViewModel _parent;

        public ContentTabsControlViewModel(MainWindowViewModel parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _parent.PropertyChanged += Parent_PropertyChanged;
            _parent.EditableInstructions.CollectionChanged += (s, e) => this.RaisePropertyChanged(nameof(EditableInstructions));
            _parent.InstructionTree.CollectionChanged += (s, e) => this.RaisePropertyChanged(nameof(InstructionTree));
            _parent.AvailableInstructions.CollectionChanged += (s, e) => this.RaisePropertyChanged(nameof(AvailableInstructions));
        }

        private void Parent_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Propaga le notifiche al binding delle view
            this.RaisePropertyChanged(e?.PropertyName);
        }

        // Expose parent for code-behind usage
        public MainWindowViewModel Parent => _parent;

        // Forwarded simple properties
        public string EditorText { get => _parent.EditorText; set => _parent.EditorText = value; }
        public string HexView { get => _parent.HexView; set => _parent.HexView = value; }
        public string InstructionView { get => _parent.InstructionView; set => _parent.InstructionView = value; }
        public string DecodedData { get => _parent.DecodedData; set => _parent.DecodedData = value; }

        public string FileName => _parent.FileName;
        public string FileSize => _parent.FileSize;
        public int LineCount => _parent.LineCount;

        // Collections (direct proxies)
        public ObservableCollection<InstructionNode> InstructionTree => _parent.InstructionTree;
        public ObservableCollection<EditableInstruction> EditableInstructions => _parent.EditableInstructions;
        public ObservableCollection<string> AvailableInstructions => _parent.AvailableInstructions;

        // Selection proxies
        public InstructionNode? SelectedNode { get => _parent.SelectedNode; set => _parent.SelectedNode = value; }
        public EditableInstruction? SelectedInstruction { get => _parent.SelectedInstruction; set => _parent.SelectedInstruction = value; }
        public int SelectedParameterIndex { get => _parent.SelectedParameterIndex; set => _parent.SelectedParameterIndex = value; }
        public string SelectedParameterName => _parent.SelectedParameterName;
        public ResourceOption? SelectedResourceOption { get => _parent.SelectedResourceOption; set => _parent.SelectedResourceOption = value; }

        // Command proxies
        public ICommand AddInstructionCommand => _parent.AddInstructionCommand;
        public ICommand RemoveInstructionCommand => _parent.RemoveInstructionCommand;
        public ICommand MoveUpCommand => _parent.MoveUpCommand;
        public ICommand MoveDownCommand => _parent.MoveDownCommand;
        public ICommand SaveChangesCommand => _parent.SaveChangesCommand;
        public ICommand DiscardChangesCommand => _parent.DiscardChangesCommand;
        public ICommand SelectParameterCommand => _parent.SelectParameterCommand;
        public ICommand ApplyResourceOptionCommand => _parent.ApplyResourceOptionCommand;
        public ICommand ParameterClickCommand => _parent.ParameterClickCommand;

        // State proxies used by XAML
        public bool IsEditMode => _parent.IsEditMode;
        public bool CanRemoveInstruction => _parent.CanRemoveInstruction;
        public bool CanMoveUp => _parent.CanMoveUp;
        public bool CanMoveDown => _parent.CanMoveDown;
        public bool HasUnsavedChanges => _parent.HasUnsavedChanges;

        public ObservableCollection<ResourceOption> ResourceExplorerOptions => _parent.ResourceExplorerOptions;
        public string ResourceSearchText { get => _parent.ResourceSearchText; set => _parent.ResourceSearchText = value; }

        public void Dispose()
        {
            _parent.PropertyChanged -= Parent_PropertyChanged;
        }
    }
}
