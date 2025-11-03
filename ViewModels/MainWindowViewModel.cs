using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using CommunityToolkit.Mvvm.Input;
using FileViewerApp.Models;
using FileViewerApp.Services;
using FileViewerApp.Enums;
using FileViewerApp.ViewModels.Controls;
using FileViewerApp.Models.FileViewerApp.Models; // aggiungi questo using in cima al file

namespace FileViewerApp.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private Window? _currentWindow;
        private readonly FileProcessorOrchestrator _orchestrator;

        private ProcessedFile? _currentProcessedFile;
        private byte[]? _currentFileBytes;

        private string _editorText = string.Empty;
        private string _hexView = string.Empty;
        private string _decodedData = string.Empty;
        private string _instructionView = string.Empty;
        private string _fileName = "Nessun file aperto";
        private string _fileSize = "0 bytes";
        private int _line_count;
        private int _charCount;
        private string _fileEncoding = "UTF-8";
        private int _startOffset = 0x32;
        private int _recordCount = 100;
        private string _statusText = "Pronto";
        private bool _isProcessing;
        private string _currentFileType = "Nessuno";
        private bool _isEditMode;
        private bool _hasUnsavedChanges;
        private bool _canEdit;
        private EditableInstruction? _selectedInstruction;
        private bool _isDefinitionsLoaded;

        public ObservableCollection<string> AvailableInstructions { get; } = new();
        public ObservableCollection<InstructionNode> InstructionTree { get; private set; } = new();
        public ObservableCollection<EditableInstruction> EditableInstructions { get; } = new();

        // Commands typed as IAsyncRelayCommand so we can call NotifyCanExecuteChanged()
        public IAsyncRelayCommand OpenFileCommand { get; }
        public IAsyncRelayCommand CloseFileCommand { get; }
        public IAsyncRelayCommand SaveFileCommand { get; }
        public IAsyncRelayCommand ConvertFileCommand { get; }
        public IAsyncRelayCommand RefreshCommand { get; }
        public IAsyncRelayCommand AddInstructionCommand { get; }
        public IAsyncRelayCommand RemoveInstructionCommand { get; }
        public IAsyncRelayCommand MoveUpCommand { get; }
        public IAsyncRelayCommand MoveDownCommand { get; }
        public IAsyncRelayCommand SaveChangesCommand { get; }
        public IAsyncRelayCommand DiscardChangesCommand { get; }

        // Command to explicitly load XML definitions
        public IAsyncRelayCommand LoadDefinitionsCommand { get; }

        // Esporre i sotto-ViewModel per i control
        public ToolbarControlViewModel ToolbarViewModel { get; }
        public ContentTabsControlViewModel ContentTabsViewModel { get; }

        // New commands for expand/collapse
        public System.Windows.Input.ICommand? ExpandAllCommand { get; }
        public System.Windows.Input.ICommand? CollapseAllCommand { get; }

        public MainWindowViewModel()
        {
            var opCodeService = new OpCodeService();
            _orchestrator = new FileProcessorOrchestrator(opCodeService);

            // Initialize commands FIRST so control viewmodels see them when instantiated
            LoadDefinitionsCommand = new AsyncRelayCommand(LoadDefinitionsAsync); // always available

            OpenFileCommand = new AsyncRelayCommand(OpenFileAsync, () => IsDefinitionsLoaded);
            CloseFileCommand = new AsyncRelayCommand(CloseFileAsync, () => IsDefinitionsLoaded);
            SaveFileCommand = new AsyncRelayCommand(SaveFileAsync, () => IsDefinitionsLoaded);
            ConvertFileCommand = new AsyncRelayCommand(ConvertFileAsync, () => IsDefinitionsLoaded);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => IsDefinitionsLoaded);

            AddInstructionCommand = new AsyncRelayCommand(AddInstructionAsync, () => IsDefinitionsLoaded && IsEditMode);
            RemoveInstructionCommand = new AsyncRelayCommand(RemoveInstructionAsync, () => IsDefinitionsLoaded && CanRemoveInstruction);
            MoveUpCommand = new AsyncRelayCommand(MoveUpAsync, () => IsDefinitionsLoaded && CanMoveUp);
            MoveDownCommand = new AsyncRelayCommand(MoveDownAsync, () => IsDefinitionsLoaded && CanMoveDown);
            SaveChangesCommand = new AsyncRelayCommand(SaveChangesAsync, () => IsDefinitionsLoaded && HasUnsavedChanges);
            DiscardChangesCommand = new AsyncRelayCommand(DiscardChangesAsync, () => IsDefinitionsLoaded && HasUnsavedChanges);

            // Create expand/collapse commands
            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);

            // Now initialize the control viewmodels so their bindings see valid command instances
            ToolbarViewModel = new ToolbarControlViewModel(this);
            ContentTabsViewModel = new ContentTabsControlViewModel(this);

            UpdateStatus("Pronto - Carica definizioni (.xml) prima di aprire file");
            EditableInstructions.CollectionChanged += OnInstructionsCollectionChanged;

            // Subscribe to DefinitionsLoaded event — when service loads definitions (ex: LoadFromFileAsync)
            _orchestrator.GetOpCodeService().DefinitionsLoaded += OpCodeService_DefinitionsLoaded;

            // Do NOT auto-load definitions or call LoadAvailableInstructions here.
            // Everything starts disabled until user loads the XML via LoadDefinitionsCommand.
        }

        private void OpCodeService_DefinitionsLoaded(object? sender, EventArgs e)
        {
            // Mark loaded and update UI on UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsDefinitionsLoaded = true;
                LoadAvailableInstructions();
            });
        }

        // Property that controls overall availability of app functionality
        public bool IsDefinitionsLoaded
        {
            get => _isDefinitionsLoaded;
            private set
            {
                this.RaiseAndSetIfChanged(ref _isDefinitionsLoaded, value);
                NotifyAllCommands();
            }
        }

        // Metodo per caricare manualmente un file di definizioni (es. INFO.XML)
        private async Task LoadDefinitionsAsync()
        {
            try
            {
                if (_currentWindow?.StorageProvider == null || !_currentWindow.StorageProvider.CanOpen)
                {
                    UpdateStatus("StorageProvider non disponibile");
                    return;
                }

                var files = await _currentWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Seleziona file definizioni (INFO.XML)",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Definizioni XML"){ Patterns = new[]{"*.xml"} },
                        FilePickerFileTypes.All
                    }
                });

                if (files == null || files.Count == 0)
                {
                    UpdateStatus("Nessun file di definizioni selezionato");
                    return;
                }

                var path = files[0].Path.LocalPath;

                // Ask service to load definitions from chosen file. Service will raise DefinitionsLoaded event.
                await _orchestrator.GetOpCodeService().LoadFromFileAsync(path);

                // Ensure UI updated immediately as fallback
                IsDefinitionsLoaded = true;
                LoadAvailableInstructions();

                UpdateStatus($"Definizioni caricate da: {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Errore caricamento definizioni: {ex.Message}");
            }
        }

        public void SetWindow(Window window) => _currentWindow = window;

        private InstructionNode? _selectedNode;
        public InstructionNode? SelectedNode
        {
            get => _selectedNode;
            set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
        }

        public string EditorText { get => _editorText; set => this.RaiseAndSetIfChanged(ref _editorText, value); }
        public string HexView { get => _hexView; set => this.RaiseAndSetIfChanged(ref _hexView, value); }
        public string DecodedData { get => _decodedData; set => this.RaiseAndSetIfChanged(ref _decodedData, value); }
        public string InstructionView { get => _instructionView; set => this.RaiseAndSetIfChanged(ref _instructionView, value); }
        public string FileName { get => _fileName; set => this.RaiseAndSetIfChanged(ref _fileName, value); }
        public string FileSize { get => _fileSize; set => this.RaiseAndSetIfChanged(ref _fileSize, value); }
        public int LineCount { get => _line_count; set => this.RaiseAndSetIfChanged(ref _line_count, value); }
        public int CharCount { get => _charCount; set => this.RaiseAndSetIfChanged(ref _charCount, value); }
        public string FileEncoding { get => _fileEncoding; set => this.RaiseAndSetIfChanged(ref _fileEncoding, value); }
        public int StartOffset { get => _startOffset; set => this.RaiseAndSetIfChanged(ref _startOffset, value); }
        public int RecordCount { get => _recordCount; set => this.RaiseAndSetIfChanged(ref _recordCount, value); }
        public string StatusText { get => _statusText; set => this.RaiseAndSetIfChanged(ref _statusText, value); }
        public bool IsProcessing { get => _isProcessing; set => this.RaiseAndSetIfChanged(ref _isProcessing, value); }
        public string CurrentFileType { get => _currentFileType; set => this.RaiseAndSetIfChanged(ref _currentFileType, value); }
        public bool IsEditMode { get => _isEditMode; set { this.RaiseAndSetIfChanged(ref _isEditMode, value); UpdateCanEditState(); } }
        public bool HasUnsavedChanges { get => _hasUnsavedChanges; set { this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value); NotifyAllCommands(); } }
        public bool CanEdit { get => _canEdit; set => this.RaiseAndSetIfChanged(ref _canEdit, value); }
        public EditableInstruction? SelectedInstruction { get => _selectedInstruction; set { this.RaiseAndSetIfChanged(ref _selectedInstruction, value); UpdateAllButtonStates(); NotifyAllCommands(); } }

        public bool CanRemoveInstruction => SelectedInstruction != null && IsEditMode;
        public bool CanMoveUp => SelectedInstruction != null && IsEditMode && EditableInstructions.IndexOf(SelectedInstruction) > 0;
        public bool CanMoveDown => SelectedInstruction != null && IsEditMode && EditableInstructions.IndexOf(SelectedInstruction) < EditableInstructions.Count - 1;

        private void LoadAvailableInstructions()
        {
            try
            {
                var svc = _orchestrator.GetOpCodeService();
                var dict = svc.GetAllOpCodes();
                AvailableInstructions.Clear();
                foreach (var kv in dict.OrderBy(o => o.Value.Name))
                    AvailableInstructions.Add(kv.Value.Name);
                UpdateStatus($"Caricate {AvailableInstructions.Count} istruzioni");
                NotifyAllCommands();
            }
            catch (Exception ex) { UpdateStatus($"Errore lista istruzioni: {ex.Message}"); }
        }

        public void OnInstructionNameChanged(EditableInstruction instruction, string newInstructionName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newInstructionName)) return;
                var svc = _orchestrator.GetOpCodeService();
                var info = svc.GetOpCodeByName(newInstructionName);
                if (info == null)
                {
                    StatusText = $"Istruzione '{newInstructionName}' non trovata";
                    return;
                }
                instruction.OpCode = info.Id;
                instruction.Name = info.Name;
                instruction.IsModified = true;
                HasUnsavedChanges = true;
                StatusText = $"Modificata istruzione {instruction.Number:D3}";
            }
            catch (Exception ex)
            {
                StatusText = $"Errore modifica istruzione: {ex.Message}";
            }
        }

        private async Task OpenFileAsync()
        {
            try
            {
                if (_currentWindow?.StorageProvider == null || !_currentWindow.StorageProvider.CanOpen)
                {
                    UpdateStatus("StorageProvider non disponibile");
                    return;
                }

                var files = await _currentWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Seleziona un file",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Programma"){ Patterns = new[]{"*.dat","*.bin","*.txt"} },
                        FilePickerFileTypes.All
                    }
                });

                if (files == null || files.Count == 0)
                {
                    UpdateStatus("Nessun file selezionato");
                    return;
                }

                IsProcessing = true;
                var path = files[0].Path.LocalPath;
                _currentFileBytes = await File.ReadAllBytesAsync(path);

                // Processa il file (qui LoadOpCodeDefinitionsAsync viene eseguito dentro ProcessFileAsync)
                _currentProcessedFile = await _orchestrator.ProcessFileAsync(path);

                // Ricarica la lista delle istruzioni ora che OpCodeService è popolato
                LoadAvailableInstructions();

                await UpdateUIFromProcessedFile(_currentProcessedFile);
                await PopulateEditableInstructions(_currentProcessedFile);
                RebuildInstructionViewSimple();
                RebuildTreeViewInitial();
                UpdateStatus($"Caricato: {_currentProcessedFile.FileType} - {_currentProcessedFile.Instructions.Count} istruzioni");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Errore apertura: {ex.Message}");
            }
            finally { IsProcessing = false; }
        }

        private async Task CloseFileAsync()
        {
            _currentProcessedFile = null;
            _currentFileBytes = null;
            EditableInstructions.Clear();
            InstructionTree.Clear();
            FileName = "Nessun file aperto";
            FileSize = "0 bytes";
            EditorText = string.Empty;
            HexView = string.Empty;
            InstructionView = string.Empty;
            DecodedData = string.Empty;
            IsEditMode = false;
            HasUnsavedChanges = false;
            UpdateStatus("File chiuso");
            await Task.CompletedTask;
        }

        private async Task SaveFileAsync()
        {
            if (_currentProcessedFile == null)
            {
                UpdateStatus("Nessun file da salvare");
                return;
            }
            if (_currentWindow?.StorageProvider == null)
            {
                UpdateStatus("StorageProvider non disponibile");
                return;
            }

            var file = await _currentWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Salva file",
                SuggestedFileName = Path.GetFileNameWithoutExtension(_currentProcessedFile.FileName),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Binario"){ Patterns=new[]{"*.dat"}},
                    new FilePickerFileType("Testo"){ Patterns=new[]{"*.txt"}}
                }
            });

            if (file == null)
            {
                UpdateStatus("Salvataggio annullato");
                return;
            }

            if (HasUnsavedChanges)
                await ApplyChangesToFile();

            var outPath = file.Path.LocalPath;
            var bytes = await _orchestrator.SaveFileAsync(_currentProcessedFile, outPath);
            UpdateStatus($"Salvato {Path.GetFileName(outPath)} ({FormatFileSize(bytes.Length)})");
        }

        private async Task ConvertFileAsync()
        {
            if (_currentProcessedFile == null)
            {
                UpdateStatus("Nessun file da convertire");
                return;
            }
            var target = _currentProcessedFile.FileType == FileType.BinaryProgram ? FileType.TextProgram : FileType.BinaryProgram;
            if (_currentWindow?.StorageProvider == null)
            {
                UpdateStatus("StorageProvider non disponibile");
                return;
            }

            var ext = target == FileType.BinaryProgram ? ".dat" : ".txt";
            var file = await _currentWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Converti",
                SuggestedFileName = Path.GetFileNameWithoutExtension(_currentProcessedFile.FileName) + ext,
                FileTypeChoices = new[] { new FilePickerFileType("Output") { Patterns = new[] { $"*{ext}" } } }
            });

            if (file == null)
            {
                UpdateStatus("Conversione annullata");
                return;
            }

            if (HasUnsavedChanges)
                await ApplyChangesToFile();

            var bytes = await _orchestrator.ConvertAsync(_currentProcessedFile, target, file.Path.LocalPath);
            UpdateStatus($"Convertito: {Path.GetFileName(file.Path.LocalPath)} ({FormatFileSize(bytes.Length)})");
        }

        private async Task RefreshAsync()
        {
            if (_currentProcessedFile == null)
            {
                UpdateStatus("Nessun file");
                return;
            }
            IsProcessing = true;
            _currentProcessedFile = await _orchestrator.ProcessFileAsync(_currentProcessedFile.FilePath, useCache: false);
            await UpdateUIFromProcessedFile(_currentProcessedFile);
            await PopulateEditableInstructions(_currentProcessedFile);
            RebuildInstructionViewSimple();
            RebuildTreeViewInitial();
            UpdateStatus("Aggiornato");
            IsProcessing = false;
        }

        private async Task AddInstructionAsync()
        {
            if (!IsEditMode) return;
            EditableInstructions.Add(new EditableInstruction
            {
                Number = EditableInstructions.Count + 1,
                OpCode = 1,
                Name = "NULLA",
                IsModified = true
            });
            HasUnsavedChanges = true;
            UpdateCanEditState();
            StatusText = "Aggiunta istruzione";
            await Task.CompletedTask;
        }

        private async Task RemoveInstructionAsync()
        {
            if (SelectedInstruction == null || !IsEditMode) return;
            EditableInstructions.Remove(SelectedInstruction);
            RenumberInstructions();
            HasUnsavedChanges = true;
            UpdateCanEditState();
            StatusText = "Rimossa istruzione";
            await Task.CompletedTask;
        }

        private async Task MoveUpAsync()
        {
            if (!CanMoveUp || SelectedInstruction == null) return;
            var i = EditableInstructions.IndexOf(SelectedInstruction);
            EditableInstructions.Move(i, i - 1);
            RenumberInstructions();
            HasUnsavedChanges = true;
            StatusText = "Spostata su";
            await Task.CompletedTask;
        }

        private async Task MoveDownAsync()
        {
            if (!CanMoveDown || SelectedInstruction == null) return;
            var i = EditableInstructions.IndexOf(SelectedInstruction);
            EditableInstructions.Move(i, i + 1);
            RenumberInstructions();
            HasUnsavedChanges = true;
            StatusText = "Spostata giù";
            await Task.CompletedTask;
        }

        private async Task SaveChangesAsync()
        {
            if (!HasUnsavedChanges || _currentFileBytes == null) return;
            IsProcessing = true;
            StatusText = "Salvataggio modifiche...";
            await ApplyChangesToFile();
            foreach (var instr in EditableInstructions)
                instr.IsModified = false;
            HasUnsavedChanges = false;
            IsProcessing = false;
            StatusText = "Modifiche salvate";
            await UpdateHexViewAsync();
            RebuildInstructionViewSimple();
            RebuildTreeViewSimple();
        }

        private async Task DiscardChangesAsync()
        {
            if (!HasUnsavedChanges || _currentProcessedFile == null) return;
            await PopulateEditableInstructions(_currentProcessedFile);
            HasUnsavedChanges = false;
            RebuildInstructionViewSimple();
            RebuildTreeViewInitial();
            StatusText = "Modifiche scartate";
        }

        private void RebuildInstructionViewSimple()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ISTRUZIONI");
            sb.AppendLine("==========");
            sb.AppendLine();
            foreach (var i in EditableInstructions.OrderBy(i => i.Number))
            {
                var pars = i.GetParameters();
                // Mostra solo parametri significativi (non zero) oppure tutti se vuoi togliere filtro.
                var significant = pars.Where(p => p != 0).ToArray();
                string paramList = significant.Length > 0 ? string.Join(", ", significant) : "";
                if (paramList.Length > 0)
                    sb.AppendLine($"{i.Number:D3}: {i.Name} [{paramList}]");
                else
                    sb.AppendLine($"{i.Number:D3}: {i.Name}");
            }
            sb.AppendLine();
            sb.AppendLine($"Totale: {EditableInstructions.Count}");
            InstructionView = sb.ToString();
        }

        private void RebuildTreeViewInitial()
        {
            InstructionTree.Clear();
            if (_currentProcessedFile != null && _currentProcessedFile.InstructionTree.Count > 0)
            {
                foreach (var n in _currentProcessedFile.InstructionTree)
                    InstructionTree.Add(n);
            }
        }

        private async void RebuildTreeViewSimple()
        {
            InstructionTree.Clear();
            if (EditableInstructions.Count == 0) return;

            List<Instruction> instructions = new List<Instruction>();

            foreach (var ei in EditableInstructions)
            {
                var instr = new Instruction
                {
                    Number = ei.Number,
                    OpCode = ei.OpCode,
                    Name = ei.Name,
                    Parameters = ei.GetParameters()
                };
                instructions.Add(instr);
            }

            _currentProcessedFile!.InstructionTree = _orchestrator.GenerateInstructionTree(instructions, _currentProcessedFile!.Header);

            RebuildTreeViewInitial();

            // List<InstructionNode> rootNodes = _orchestrator.GenerateInstructionTree(instructions, _currentProcessedFile!.Header);
            // foreach (var node in rootNodes)
            // {
            //     InstructionTree.Add(node);
            // }

            // var root = new InstructionNode
            // {
            //     Name = $"Programma ({EditableInstructions.Count} istruzioni)",
            //     IsExpanded = true
            // };
            // foreach (var i in EditableInstructions.OrderBy(i => i.Number))
            // {
            //     root.Children.Add(new InstructionNode
            //     {
            //         Name = $"{i.Number:D3}: {i.Name} (OpCode: {i.OpCode})",
            //         IsExpanded = false
            //     });
            // }
            // InstructionTree.Add(root);
        }

        private async Task UpdateUIFromProcessedFile(ProcessedFile processedFile)
        {
            FileName = processedFile.FileName;
            FileSize = FormatFileSize(processedFile.FileSize);
            CurrentFileType = processedFile.FileType.ToString();
            HexView = processedFile.HexView;
            InstructionView = processedFile.TextualView;
            try
            {
                EditorText = Encoding.UTF8.GetString(processedFile.RawContent);
                LineCount = EditorText.Split('\n').Length;
                CharCount = EditorText.Length;
            }
            catch
            {
                EditorText = "Contenuto binario";
                LineCount = 0;
                CharCount = 0;
            }
            DecodedData = $"=== FILE ===\nNome: {processedFile.FileName}\nTipo: {processedFile.FileType}\nDimensione: {FormatFileSize(processedFile.FileSize)}\nIstruzioni: {processedFile.Instructions.Count}\n";
            await Task.CompletedTask;
        }

        private async Task PopulateEditableInstructions(ProcessedFile processedFile)
        {
            EditableInstructions.Clear();
            foreach (var instr in processedFile.Instructions)
            {
                var e = new EditableInstruction
                {
                    Number = instr.Number,
                    OpCode = instr.OpCode,
                    Name = instr.Name
                };
                e.SetParameters(instr.Parameters.ToArray());
                EditableInstructions.Add(e);
            }
            IsEditMode = EditableInstructions.Count > 0;
            HasUnsavedChanges = false;
            await Task.CompletedTask;
        }

        private async Task ApplyChangesToFile()
        {
            if (_currentProcessedFile == null) return;

            // Rebuild binary from editable instructions using 4-byte int layout
            // Header: preserve original header if available, else derive from file name
            string header = _currentProcessedFile.Header;
            if (string.IsNullOrWhiteSpace(header))
            {
                header = $" 106{Path.GetFileNameWithoutExtension(_currentProcessedFile.FileName)}";
            }
            header = header.PadRight(8).Substring(0, 8);

            var instructions = EditableInstructions.OrderBy(i => i.Number).ToList();
            int recordSize = 36; // 4 bytes opcode + 8 * 4 bytes params
            int totalSize = 8 + 42 + (instructions.Count * recordSize);
            var newBytes = new byte[totalSize];

            // Header
            Encoding.ASCII.GetBytes(header).CopyTo(newBytes, 0);
            // Padding already zero-initialized for bytes 8..49 (42 bytes)

            // Write instructions
            int offset = 0x32; // 50
            foreach (var instr in instructions)
            {
                // Write opcode
                BitConverter.GetBytes(instr.OpCode).CopyTo(newBytes, offset);
                var pars = instr.GetParameters();
                for (int p = 0; p < 8; p++)
                {
                    int val = p < pars.Length ? pars[p] : 0;
                    BitConverter.GetBytes(val).CopyTo(newBytes, offset + 4 + (p * 4));
                }
                offset += recordSize;
            }

            // Update processed file model
            _currentFileBytes = newBytes;
            _currentProcessedFile.RawContent = newBytes;
            _currentProcessedFile.FileSize = newBytes.Length;
            _currentProcessedFile.Instructions = instructions.Select(e => new Instruction
            {
                Number = e.Number,
                OpCode = e.OpCode,
                Name = e.Name,
                Parameters = e.GetParameters(),
                Offset = 0x32 + ((e.Number - 1) * recordSize)
            }).ToList();

            // Regenerate textual & tree views with orchestrator helpers
            _currentProcessedFile.HexView = _orchestrator.GetOpCodeService() != null ? _orchestrator.GenerateInstructionTree(_currentProcessedFile.Instructions, header) != null ? _currentProcessedFile.HexView : _currentProcessedFile.HexView : _currentProcessedFile.HexView; // leave hex for later explicit refresh
            // Simpler: rebuild InstructionTree
            _currentProcessedFile.InstructionTree = _orchestrator.GenerateInstructionTree(_currentProcessedFile.Instructions, header);

            await Task.CompletedTask;
        }

        private async Task UpdateHexViewAsync()
        {
            if (_currentFileBytes == null) return;
            var sb = new StringBuilder();
            var max = Math.Min(_currentFileBytes.Length, 1024);
            for (int i = 0; i < max; i += 16)
            {
                sb.Append($"{i:X8}: ");
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < max) sb.Append($"{_currentFileBytes[i + j]:X2} ");
                    else sb.Append("   ");
                    if (j == 7) sb.Append(" ");
                }
                sb.Append(" |");
                for (int j = 0; j < 16 && i + j < max; j++)
                {
                    var b = _currentFileBytes[i + j];
                    sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
                }
                sb.AppendLine("|");
            }
            if (_currentFileBytes.Length > max)
                sb.AppendLine($"\n... ({max} di {_currentFileBytes.Length})");
            HexView = sb.ToString();
            await Task.CompletedTask;
        }

        private void OnInstructionsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (EditableInstruction item in e.NewItems)
                    item.InstructionChanged += OnInstructionChanged;
            }
            if (e.OldItems != null)
            {
                foreach (EditableInstruction item in e.OldItems)
                    item.InstructionChanged -= OnInstructionChanged;
            }
            UpdateCanEditState();
        }

        private void OnInstructionChanged(object? sender, PropertyChangedEventArgs e)
        {
            HasUnsavedChanges = EditableInstructions.Any(i => i.IsModified);
            UpdateAllButtonStates();
            NotifyAllCommands();
            // Nessun refresh immediato: solo dopo Salva
        }

        private void UpdateCanEditState()
        {
            CanEdit = IsEditMode && EditableInstructions.Count > 0;
            UpdateAllButtonStates();
            NotifyAllCommands();
        }

        private void UpdateAllButtonStates()
        {
            this.RaisePropertyChanged(nameof(CanRemoveInstruction));
            this.RaisePropertyChanged(nameof(CanMoveUp));
            this.RaisePropertyChanged(nameof(CanMoveDown));
        }

        // Notify all AsyncRelayCommand instances to recompute CanExecute
        private void NotifyAllCommands()
        {
            OpenFileCommand.NotifyCanExecuteChanged();
            CloseFileCommand.NotifyCanExecuteChanged();
            SaveFileCommand.NotifyCanExecuteChanged();
            ConvertFileCommand.NotifyCanExecuteChanged();
            RefreshCommand.NotifyCanExecuteChanged();

            AddInstructionCommand.NotifyCanExecuteChanged();
            RemoveInstructionCommand.NotifyCanExecuteChanged();
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
            SaveChangesCommand.NotifyCanExecuteChanged();
            DiscardChangesCommand.NotifyCanExecuteChanged();

            LoadDefinitionsCommand.NotifyCanExecuteChanged();
        }

        private void UpdateStatus(string msg)
        {
            StatusText = $"{DateTime.Now:HH:mm:ss} - {msg}";
        }

        // Utility method added (was missing)
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} bytes";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        // Utility method added (was missing)
        private void RenumberInstructions()
        {
            for (int i = 0; i < EditableInstructions.Count; i++)
                EditableInstructions[i].Number = i + 1;
        }

        // New helper methods to expand/collapse the instruction tree
        private void ExpandAll()
        {
            foreach (var node in InstructionTree)
                SetNodeExpandedRecursive(node, true);
        }

        private void CollapseAll()
        {
            foreach (var node in InstructionTree)
                SetNodeExpandedRecursive(node, false);
        }

        private void SetNodeExpandedRecursive(InstructionNode node, bool value)
        {
            if (node == null) return;
            node.IsExpanded = value;
            foreach (var child in node.Children)
                SetNodeExpandedRecursive(child, value);
        }
    }
}