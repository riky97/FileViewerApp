using System;
using System.ComponentModel;
using FileViewerApp.ViewModels;
using ReactiveUI;
using System.Windows.Input;

namespace FileViewerApp.ViewModels.Controls;

public class ToolbarControlViewModel : ViewModelBase, IDisposable
{
    private readonly MainWindowViewModel _parent;

    public ToolbarControlViewModel(MainWindowViewModel parent)
    {
        _parent = parent ?? throw new System.ArgumentNullException(nameof(parent));
        _parent.PropertyChanged += Parent_PropertyChanged;
    }

    private void Parent_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Propaga la notifica al view model del controllo così le view legate al controllo vedono gli aggiornamenti
        this.RaisePropertyChanged(e?.PropertyName);
    }

    // Command proxies
    public ICommand? LoadDefinitionsCommand => _parent.LoadDefinitionsCommand;
    public ICommand? OpenFileCommand => _parent.OpenFileCommand;
    public ICommand? SaveFileCommand => _parent.SaveFileCommand;
    public ICommand? ConvertFileCommand => _parent.ConvertFileCommand;
    public ICommand? RefreshCommand => _parent.RefreshCommand;
    public ICommand? CloseFileCommand => _parent.CloseFileCommand;

    // New proxies for expand/collapse tree
    public ICommand? ExpandAllCommand => _parent.ExpandAllCommand;
    public ICommand? CollapseAllCommand => _parent.CollapseAllCommand;

    // State proxies
    public bool IsProcessing => _parent.IsProcessing;
    public bool IsDefinitionsLoaded => _parent.IsDefinitionsLoaded;
    public string StatusText => _parent.StatusText;

    public void Dispose()
    {
        _parent.PropertyChanged -= Parent_PropertyChanged;
    }
}
