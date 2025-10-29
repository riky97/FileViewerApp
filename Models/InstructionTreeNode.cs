using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;

namespace FileViewerApp.Models
{
    public class InstructionTreeNode : ReactiveObject
    {
        private string _name = "";
        private string _parameters = "";
        private ObservableCollection<InstructionTreeNode> _children = new();
        private bool _isExpanded = true;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string Parameters
        {
            get => _parameters;
            set => this.RaiseAndSetIfChanged(ref _parameters, value);
        }

        public ObservableCollection<InstructionTreeNode> Children
        {
            get => _children;
            set => this.RaiseAndSetIfChanged(ref _children, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
        }
    }

}