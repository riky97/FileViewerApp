using System;
using System.ComponentModel;
using FileViewerApp.ViewModels;
using ReactiveUI;

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
    public System.Windows.Input.ICommand? LoadDefinitionsCommand => _parent.LoadDefinitionsCommand;
    public System.Windows.Input.ICommand? OpenFileCommand => _parent.OpenFileCommand;
    public System.Windows.Input.ICommand? SaveFileCommand => _parent.SaveFileCommand;
    public System.Windows.Input.ICommand? ConvertFileCommand => _parent.ConvertFileCommand;
    public System.Windows.Input.ICommand? RefreshCommand => _parent.RefreshCommand;
    public System.Windows.Input.ICommand? CloseFileCommand => _parent.CloseFileCommand;

    // State proxies
    public bool IsProcessing => _parent.IsProcessing;
    public bool IsDefinitionsLoaded => _parent.IsDefinitionsLoaded;
    public string StatusText => _parent.StatusText;

    public void Dispose()
    {
        _parent.PropertyChanged -= Parent_PropertyChanged;
    }
}
