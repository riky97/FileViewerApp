using System.Collections.ObjectModel;
using ReactiveUI;

namespace FileViewerApp.Models
{
    public class InstructionNode : ReactiveObject
    {
        private string _name = string.Empty;
        private bool _isExpanded = true;
        private bool _isSelected = false;
        private string _details = string.Empty;
        private int _instructionNumber;
        private int _offset;
        private int _opCode;
        private string _parameters = string.Empty;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }

        public string Details
        {
            get => _details;
            set => this.RaiseAndSetIfChanged(ref _details, value);
        }

        public int InstructionNumber
        {
            get => _instructionNumber;
            set => this.RaiseAndSetIfChanged(ref _instructionNumber, value);
        }

        public int Offset
        {
            get => _offset;
            set => this.RaiseAndSetIfChanged(ref _offset, value);
        }

        public int OpCode
        {
            get => _opCode;
            set => this.RaiseAndSetIfChanged(ref _opCode, value);
        }

        public string Parameters
        {
            get => _parameters;
            set => this.RaiseAndSetIfChanged(ref _parameters, value);
        }

        public ObservableCollection<InstructionNode> Children { get; } = new();

        // Proprietà per il display nella TreeView
        public string DisplayText => $"{InstructionNumber,3}: {Name}";
        public string ToolTipText => $"Offset: 0x{Offset:X6} | OpCode: {OpCode} | Params: {Parameters}";
    }
}