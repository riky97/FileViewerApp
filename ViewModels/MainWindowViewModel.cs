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
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Input.Platform;

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
        private string _operationDescription = string.Empty; // testo mostrato accanto allo spinner
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
        public ObservableCollection<ResourceOption> ParameterPickerOptions { get; } = new();
        private int _selectedParameterIndex = -1;
        public int SelectedParameterIndex { get => _selectedParameterIndex; set { this.RaiseAndSetIfChanged(ref _selectedParameterIndex, value); RefreshResourceExplorer(); this.RaisePropertyChanged(nameof(SelectedParameterName)); } }
        public string SelectedParameterName
        {
            get
            {
                if (SelectedInstruction == null || SelectedParameterIndex < 0 || SelectedParameterIndex >= SelectedInstruction.ParameterEntries.Count)
                    return "(nessuno)";
                var p = SelectedInstruction.ParameterEntries[SelectedParameterIndex];
                return string.IsNullOrWhiteSpace(p.Name) ? SelectedParameterIndex.ToString() : p.Name;
            }
        }
        public ObservableCollection<ResourceOption> ResourceExplorerOptions { get; } = new();
        private string _resourceSearchText = string.Empty;
        public string ResourceSearchText { get => _resourceSearchText; set { this.RaiseAndSetIfChanged(ref _resourceSearchText, value); RefreshResourceExplorer(); } }
        public IRelayCommand SelectParameterCommand { get; private set; }
        public IRelayCommand ApplyResourceOptionCommand { get; private set; }

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

        // Copy/Cut/Paste commands for instructions
        public IAsyncRelayCommand CopyInstructionsCommand { get; }
        public IAsyncRelayCommand CutInstructionsCommand { get; }
        public IAsyncRelayCommand PasteInstructionsCommand { get; }
        public IAsyncRelayCommand CancelCutCommand { get; }

        // Command to explicitly load XML definitions
        public IAsyncRelayCommand LoadDefinitionsCommand { get; }

        // Esporre i sotto-ViewModel per i control
        public ToolbarControlViewModel ToolbarViewModel { get; }
        public ContentTabsControlViewModel ContentTabsControlViewModel { get; }

        // New commands for expand/collapse
        public System.Windows.Input.ICommand? ExpandAllCommand { get; }
        public System.Windows.Input.ICommand? CollapseAllCommand { get; }

        private string _diffText = string.Empty;
        public string DiffText { get => _diffText; set => this.RaiseAndSetIfChanged(ref _diffText, value); }

        private readonly List<PendingChange> _pendingChanges = new();
        private List<EditableInstruction> _selectedInstructions = new();
        public IReadOnlyList<EditableInstruction> SelectedInstructions => _selectedInstructions;
        // Summary testuale delle modifiche pendenti (aggiunte/rimozioni/spostamenti) per UI
        private string _pendingChangesSummary = "Nessuna modifica";
        public string PendingChangesSummary { get => _pendingChangesSummary; private set => this.RaiseAndSetIfChanged(ref _pendingChangesSummary, value); }

        // Diff tracking for visual gutter
        private List<(int Number, int OpCode, int[] Params, string Name)> _baseline = new();
        private HashSet<int> _removedNumbers = new();

        private enum PendingChangeType { Add, Remove, Move }
        private record PendingChange(PendingChangeType Type, string Detail);

        private IRelayCommand? _refreshParameterOptionsCommand;
        private IRelayCommand? _applySelectedParameterOptionCommand;
        public IRelayCommand RefreshParameterOptionsCommand => _refreshParameterOptionsCommand!;
        public IRelayCommand ApplySelectedParameterOptionCommand => _applySelectedParameterOptionCommand!;

        private ResourceOption? _selectedResourceOption;
        public ResourceOption? SelectedResourceOption { get => _selectedResourceOption; set { this.RaiseAndSetIfChanged(ref _selectedResourceOption, value); ApplyResourceOptionCommand?.NotifyCanExecuteChanged(); } }

        public IRelayCommand<InstructionParameter> ParameterClickCommand { get; }

        public MainWindowViewModel()
        {
            var opCodeService = new OpCodeService();
            EditableInstruction.OpCodeServiceProvider = opCodeService; // collega servizio alle istruzioni
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
            SaveChangesCommand = new AsyncRelayCommand(SaveChangesAsync, () => IsDefinitionsLoaded && HasUnsavedChanges && AreAllParametersValid());
            DiscardChangesCommand = new AsyncRelayCommand(DiscardChangesAsync, () => IsDefinitionsLoaded && HasUnsavedChanges);

            CopyInstructionsCommand = new AsyncRelayCommand(CopyInstructionsAsync, () => IsDefinitionsLoaded && _selectedInstructions.Count > 0);
            CutInstructionsCommand = new AsyncRelayCommand(CutInstructionsAsync, () => IsDefinitionsLoaded && _selectedInstructions.Count > 0 && IsEditMode);
            PasteInstructionsCommand = new AsyncRelayCommand(PasteInstructionsAsync, () => IsDefinitionsLoaded && IsEditMode);
            CancelCutCommand = new AsyncRelayCommand(CancelCutAsync, () => IsDefinitionsLoaded && IsEditMode && _cutBuffer != null && _cutBuffer.Count > 0);

            // Create expand/collapse commands
            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);

            _refreshParameterOptionsCommand = new RelayCommand(RefreshParameterOptions);
            _applySelectedParameterOptionCommand = new RelayCommand(ApplySelectedParameterOption, () => SelectedParameterOption != null && SelectedInstruction != null);

            ParameterClickCommand = new RelayCommand<InstructionParameter>(param =>
            {
                if (param == null) return;
                var instr = EditableInstructions.FirstOrDefault(e => e.ParameterEntries.Contains(param));
                if (instr != null && !ReferenceEquals(SelectedInstruction, instr))
                    SelectedInstruction = instr;
                SelectedParameterIndex = param.Index;
                foreach (var p in EditableInstructions.SelectMany(e => e.ParameterEntries)) p.IsSelected = false;
                param.IsSelected = true;
                this.RaisePropertyChanged(nameof(SelectedParameterName));
            }, param => param != null);

            // Now initialize the control viewmodels so their bindings see valid command instances
            ToolbarViewModel = new ToolbarControlViewModel(this);
            ContentTabsControlViewModel = new ContentTabsControlViewModel(this);

            UpdateStatus("Pronto - Carica definizioni (.xml) prima di aprire file");
            EditableInstructions.CollectionChanged += OnInstructionsCollectionChanged;

            // Subscribe to DefinitionsLoaded event — when service loads definitions (ex: LoadFromFileAsync)
            _orchestrator.GetOpCodeService().DefinitionsLoaded += OpCodeService_DefinitionsLoaded;

            // Do NOT auto-load definitions or call LoadAvailableInstructions here.
            // Everything starts disabled until user loads the XML via LoadDefinitionsCommand.

            RevertToHistoryCommand = new AsyncRelayCommand(RevertToHistoryAsync, () => CanRevertSelected);
            HistoryEntries.CollectionChanged += (_, __) => { this.RaisePropertyChanged(nameof(CanRevertSelected)); RevertToHistoryCommand.NotifyCanExecuteChanged(); };

            // Command to select parameter in center panel
            SelectParameterCommand = new RelayCommand<int>(idx => { if (SelectedInstruction == null) return; SelectedParameterIndex = idx; }, idx => SelectedInstruction != null && idx >= 0 && idx < (SelectedInstruction?.ParamCount ?? 0));
            ApplyResourceOptionCommand = new RelayCommand<ResourceOption>(opt => ApplySelectedResourceOption(opt), opt => SelectedInstruction != null && SelectedParameterIndex >= 0 && opt?.Id.HasValue == true);
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
        public string OperationDescription { get => _operationDescription; set => this.RaiseAndSetIfChanged(ref _operationDescription, value); }
        public string CurrentFileType { get => _currentFileType; set => this.RaiseAndSetIfChanged(ref _currentFileType, value); }
        public bool IsEditMode { get => _isEditMode; set { this.RaiseAndSetIfChanged(ref _isEditMode, value); UpdateCanEditState(); } }
        public bool HasUnsavedChanges { get => _hasUnsavedChanges; set { this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value); NotifyAllCommands(); UpdatePendingSummary(); } }
        public bool CanEdit { get => _canEdit; set => this.RaiseAndSetIfChanged(ref _canEdit, value); }
        public EditableInstruction? SelectedInstruction
        {
            get => _selectedInstruction;
            set
            {
                var previous = _selectedInstruction;
                if (previous != null) previous.IsCurrent = false; // disattiva precedente
                this.RaiseAndSetIfChanged(ref _selectedInstruction, value);
                if (_selectedInstruction != null) _selectedInstruction.IsCurrent = true; // attiva nuova
                UpdateAllButtonStates();
                NotifyAllCommands();
                RefreshParameterOptions();
                SelectedParameterIndex = -1;
                foreach (var p in EditableInstructions.SelectMany(e => e.ParameterEntries)) p.IsSelected = false;
                // Riesegui controllo validità parametri globale
                NotifyAllCommands();
                this.RaisePropertyChanged(nameof(SelectedParameterName));
                NotifyAllCommands(); // ensure copy/cut enable updates
            }
        }

        public bool CanRemoveInstruction => SelectedInstruction != null && IsEditMode;
        public bool CanMoveUp => SelectedInstruction != null && IsEditMode && EditableInstructions.IndexOf(SelectedInstruction) > 0;
        public bool CanMoveDown => SelectedInstruction != null && IsEditMode && EditableInstructions.IndexOf(SelectedInstruction) < EditableInstructions.Count - 1;
        public bool CanRevertSelected => SelectedHistoryEntry != null && HistoryEntries.IndexOf(SelectedHistoryEntry) > 0;

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

        private bool AreAllParametersValid()
        {
            // Tutte le istruzioni valide e ogni parametro range valido
            foreach (var instr in EditableInstructions)
            {
                if (!instr.IsValid) return false;
                foreach (var p in instr.ParameterEntries)
                {
                    if (!p.HasResourceGroups && p.IsNumericType && !p.IsRangeValid)
                        return false;
                }
            }
            return true;
        }

        private void SubscribeInstruction(EditableInstruction instr)
        {
            instr.InstructionChanged += (_, __) => NotifyAllCommands();
            foreach (var p in instr.ParameterEntries)
                p.PropertyChanged += (_, __2) => NotifyAllCommands();
        }

        private void UnsubscribeInstruction(EditableInstruction instr)
        {
            instr.InstructionChanged -= (_, __) => NotifyAllCommands(); // cannot remove anonymous; left intentionally minimal
            foreach (var p in instr.ParameterEntries)
                p.PropertyChanged -= (_, __2) => NotifyAllCommands();
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
                    // Normalizza: rimuove spazi e underscore per confronto flessibile
                    string Normalize(string s) => new string(s.Where(c => c != ' ' && c != '_').ToArray()).ToUpperInvariant();
                    var targetNorm = Normalize(newInstructionName);
                    // Prova a trovare nome equivalente fra quelli disponibili
                    var altName = svc.GetAllOpCodeNames().FirstOrDefault(n => Normalize(n) == targetNorm);
                    if (altName != null)
                        info = svc.GetOpCodeByName(altName);
                }
                if (info == null)
                {
                    // Fallback: mappa manuale sinonimi noti
                    var synonyms = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
                    {
                        {"INIZ_MEM", 5},{"INIZ MEM",5},
                        {"END_IF",15},{"END IF",15},
                        {"IF_NUM",105},{"IF NUM",105},
                        {"IF_STR",110},{"IF STR",110}
                    };
                    if (synonyms.TryGetValue(newInstructionName.Trim(), out var op))
                    {
                        var resolved = svc.GetOpCodeInfo(op);
                        if (resolved != null) info = resolved;
                    }
                }
                if (info == null)
                {
                    StatusText = $"Istruzione '{newInstructionName}' non trovata";
                    return;
                }
                // Set OpCode first: model setter now applies ParamCount + default params + resource options.
                instruction.OpCode = info.Id;
                instruction.Name = info.Name; // Name property marks modified already; keep alignment.
                // ParamCount & defaults handled by OpCode setter. No manual SetParameters needed here.
                // Se il loader non fornisce dettagli parametri (Parameters vuoto) azzera eventuali nomi precedenti per evitare confusione
                if (info.Parameters.Count == 0)
                {
                    foreach (var p in instruction.ParameterEntries)
                        p.Name = string.Empty;
                }
                instruction.IsModified = true;
                HasUnsavedChanges = true;
                StatusText = $"Modificata istruzione {instruction.Number:D3}";
                // Resource options already reloaded by OpCode setter.
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

                OperationDescription = "Apertura file...";
                IsProcessing = true;
                var path = files[0].Path.LocalPath;
                _currentFileBytes = await File.ReadAllBytesAsync(path);

                _currentProcessedFile = await _orchestrator.ProcessFileAsync(path);
                LoadAvailableInstructions();

                await UpdateUIFromProcessedFile(_currentProcessedFile);
                await PopulateEditableInstructions(_currentProcessedFile);
                RebuildInstructionViewSimple();
                RebuildTreeViewInitial();
                UpdateStatus($"Caricato: {_currentProcessedFile.FileType} - {_currentProcessedFile.Instructions.Count} istruzioni");
                AddHistory(HistoryActionType.Load, $"Caricato {Path.GetFileName(path)}");
                AddHistory(HistoryActionType.Load, "Stato iniziale", force: true);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Errore apertura: {ex.Message}");
            }
            finally { IsProcessing = false; OperationDescription = string.Empty; }
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

            OperationDescription = "Salvataggio file...";
            IsProcessing = true;
            try
            {
                if (HasUnsavedChanges)
                    await ApplyChangesToFile();

                var outPath = file.Path.LocalPath;
                var bytes = await _orchestrator.SaveFileAsync(_currentProcessedFile, outPath);
                UpdateStatus($"Salvato {Path.GetFileName(outPath)} ({FormatFileSize(bytes.Length)})");
            }
            finally
            {
                IsProcessing = false;
                OperationDescription = string.Empty;
            }
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

            OperationDescription = "Conversione file...";
            IsProcessing = true;
            try
            {
                if (HasUnsavedChanges)
                    await ApplyChangesToFile();

                var bytes = await _orchestrator.ConvertAsync(_currentProcessedFile, target, file.Path.LocalPath);
                UpdateStatus($"Convertito: {Path.GetFileName(file.Path.LocalPath)} ({FormatFileSize(bytes.Length)})");
            }
            finally
            {
                IsProcessing = false;
                OperationDescription = string.Empty;
            }
        }

        private async Task RefreshAsync()
        {
            if (_currentProcessedFile == null)
            {
                UpdateStatus("Nessun file");
                return;
            }
            OperationDescription = "Aggiornamento...";
            IsProcessing = true;
            _currentProcessedFile = await _orchestrator.ProcessFileAsync(_currentProcessedFile.FilePath, useCache: false);
            await UpdateUIFromProcessedFile(_currentProcessedFile);
            await PopulateEditableInstructions(_currentProcessedFile);
            RebuildInstructionViewSimple();
            RebuildTreeViewInitial();
            UpdateStatus("Aggiornato");
            IsProcessing = false;
            OperationDescription = string.Empty;
        }

        private async Task AddInstructionAsync()
        {
            if (!IsEditMode) return;
            // Determine insertion index: after selected instruction if any, else at end
            int insertionIndex = EditableInstructions.Count; // default append
            if (SelectedInstruction != null)
            {
                int selIndex = EditableInstructions.IndexOf(SelectedInstruction);
                if (selIndex >= 0)
                    insertionIndex = selIndex + 1; // insert right after selected
            }

            var newInstr = new EditableInstruction
            {
                Number = insertionIndex + 1, // provisional, will be renumbered
                OpCode = 1,
                Name = "NULLA",
                IsModified = true,
                DiffKind = InstructionDiffKind.Added
            };

            if (insertionIndex >= 0 && insertionIndex < EditableInstructions.Count)
                EditableInstructions.Insert(insertionIndex, newInstr);
            else
                EditableInstructions.Add(newInstr);

            RenumberInstructions(); // ensure all Numbers updated
            SelectedInstruction = newInstr; // focus the newly added instruction for immediate editing
            HasUnsavedChanges = true;
            UpdateCanEditState();
            StatusText = "Aggiunta istruzione";
            AppendPending(PendingChangeType.Add, $"Add #{newInstr.Number}");
            RecomputeDiff();
            await Task.CompletedTask;
        }

        private async Task RemoveInstructionAsync()
        {
            if (SelectedInstruction == null || !IsEditMode) return;
            int removedNum = SelectedInstruction.Number;
            EditableInstructions.Remove(SelectedInstruction);
            RenumberInstructions();
            HasUnsavedChanges = true;
            UpdateCanEditState();
            StatusText = "Rimossa istruzione";
            AppendPending(PendingChangeType.Remove, $"Del #{removedNum}");
            RecomputeDiff();
            await Task.CompletedTask;
        }

        private async Task MoveUpAsync()
        {
            try
            {
                if (!CanMoveUp || SelectedInstruction == null) return;
                var instr = SelectedInstruction;
                int i = EditableInstructions.IndexOf(instr);
                if (i <= 0) return;
                EditableInstructions.Move(i, i - 1);
                RenumberInstructions();
                SelectedInstruction = instr;
                HasUnsavedChanges = true;
                StatusText = $"Spostata su {instr.Number}";
                AppendPending(PendingChangeType.Move, $"MoveUp to {instr.Number}");
            }
            catch (Exception ex)
            {
                StatusText = $"Errore MoveUp: {ex.Message}";
            }
            await Task.CompletedTask;
        }

        private async Task MoveDownAsync()
        {
            try
            {
                if (!CanMoveDown || SelectedInstruction == null) return;
                var instr = SelectedInstruction;
                int i = EditableInstructions.IndexOf(instr);
                if (i < 0 || i >= EditableInstructions.Count - 1) return;
                EditableInstructions.Move(i, i + 1);
                RenumberInstructions();
                SelectedInstruction = instr;
                HasUnsavedChanges = true;
                StatusText = $"Spostata giù {instr.Number}";
                AppendPending(PendingChangeType.Move, $"MoveDown to {instr.Number}");
            }
            catch (Exception ex)
            {
                StatusText = $"Errore MoveDown: {ex.Message}";
            }
            await Task.CompletedTask;
        }

        private async Task SaveChangesAsync()
        {
            if (!HasUnsavedChanges || _currentFileBytes == null) return;
            OperationDescription = "Salvataggio modifiche...";
            IsProcessing = true;
            try
            {
                StatusText = "Salvataggio modifiche...";
                // Rimuovi eventuali placeholder di taglio prima di consolidare lo stato
                ClearCutVisualState();
                await ApplyChangesToFile();
                foreach (var instr in EditableInstructions)
                    instr.IsModified = false;
                HasUnsavedChanges = false;
                var summary = BuildPendingSummary();
                StatusText = "Modifiche salvate";
                AddHistory(HistoryActionType.Save, $"Salvataggio modifiche ({summary})", force: true);
                ClearPendingChanges();
                await UpdateHexViewAsync();
                RebuildInstructionViewSimple();
                RebuildTreeViewSimple();
                // Dopo un salvataggio il nuovo stato diventa baseline per la diff e lo scarto selettivo
                CaptureBaseline();
                RecomputeDiff();
            }
            finally
            {
                IsProcessing = false;
                OperationDescription = string.Empty;
            }
        }

        private async Task DiscardChangesAsync()
        {
            if (!HasUnsavedChanges) return;
            OperationDescription = "Scarto modifiche non salvate...";
            IsProcessing = true;
            try
            {
                // Ricostruisci completamente lo stato partendo dalla baseline salvata
                var svc = _orchestrator.GetOpCodeService();
                var restored = new List<EditableInstruction>();
                foreach (var b in _baseline.OrderBy(b => b.Number))
                {
                    var info = svc.GetOpCodeInfo(b.OpCode);
                    var e = new EditableInstruction
                    {
                        Number = b.Number,
                        OpCode = b.OpCode,
                        Name = b.Name,
                        ParamCount = info?.ParamCount ?? 8
                    };
                    e.BeginSilentUpdate();
                    e.SetParameters((int[])b.Params.Clone());
                    e.EndSilentUpdate();
                    e.EnableModificationTracking();
                    e.IsModified = false;
                    e.DiffKind = InstructionDiffKind.Unchanged;
                    e.LoadResourceOptions();
                    restored.Add(e);
                }
                EditableInstructions.Clear();
                foreach (var r in restored) EditableInstructions.Add(r);
                RenumberInstructions();
                HasUnsavedChanges = false;
                ClearPendingChanges();
                StatusText = "Modifiche non salvate scartate";
                RebuildInstructionViewSimple();
                RebuildTreeViewSimple();
                RecomputeDiff();
            }
            finally
            {
                IsProcessing = false;
                OperationDescription = string.Empty;
            }
            await Task.CompletedTask;
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
            ExpandAll();
        }

        private void RebuildTreeViewSimple()
        {
            InstructionTree.Clear();
            if (EditableInstructions.Count == 0) return;

            var instructions = EditableInstructions
                .OrderBy(e => e.Number)
                .Select(e => new Instruction
                {
                    Number = e.Number,
                    OpCode = e.OpCode,
                    Name = e.Name,
                    Parameters = e.GetParameters()
                }).ToList();

            // Ensure non-null header to avoid CS8600 nullable warning
            string header = _currentProcessedFile?.Header ?? string.Empty;
            if (string.IsNullOrWhiteSpace(header))
            {
                var fileBase = Path.GetFileNameWithoutExtension(_currentProcessedFile?.FileName ?? "TEMP");
                header = $" 106{fileBase}";
            }
            header = header.PadRight(8).Substring(0, 8);

            var nodes = _orchestrator.GenerateInstructionTree(instructions, header);
            foreach (var n in nodes)
                InstructionTree.Add(n);
            ExpandAll();
        }

        private Task UpdateUIFromProcessedFile(ProcessedFile processedFile)
        {
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
            return Task.CompletedTask;
        }

        private Task PopulateEditableInstructions(ProcessedFile processedFile)
        {
            EditableInstructions.Clear();
            var svc = _orchestrator.GetOpCodeService();
            foreach (var instr in processedFile.Instructions)
            {
                var info = svc.GetOpCodeInfo(instr.OpCode);
                var e = new EditableInstruction
                {
                    Number = instr.Number,
                    OpCode = instr.OpCode,
                    Name = instr.Name,
                    ParamCount = info?.ParamCount ?? 8
                };
                e.SetParameters(instr.Parameters.ToArray());
                EditableInstructions.Add(e);
            }
            IsEditMode = EditableInstructions.Count > 0;
            HasUnsavedChanges = false;
            foreach (var instruction in EditableInstructions)
                instruction.LoadResourceOptions();
            CaptureBaseline();
            RecomputeDiff();
            return Task.CompletedTask;
        }

        public void PopulateEditableInstructionsSync(ProcessedFile processedFile)
        {
            PopulateEditableInstructions(processedFile).GetAwaiter().GetResult();
        }

        private Task ApplyChangesToFile()
        {
            if (_currentProcessedFile == null) return Task.CompletedTask;

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

            return Task.CompletedTask;
        }

        private Task UpdateHexViewAsync()
        {
            if (_currentFileBytes == null) return Task.CompletedTask;
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
            return Task.CompletedTask;
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
            if (_isReverting || _suppressInstructionEvents || _ignoreInstructionChanged) return; // ignora eventi in fasi protette
            HasUnsavedChanges = EditableInstructions.Any(i => i.IsModified);
#if DEBUG
            Debug.WriteLine($"[DEBUG] OnInstructionChanged -> HasUnsavedChanges={HasUnsavedChanges}");
#endif
            UpdateAllButtonStates();
            NotifyAllCommands();
            if (sender is EditableInstruction && (
                e.PropertyName == nameof(EditableInstruction.OpCode) ||
                e.PropertyName == nameof(EditableInstruction.Name) ||
                e.PropertyName == nameof(EditableInstruction.ParamCount) ||
                (e.PropertyName != null && e.PropertyName.StartsWith("Param")) ||
                e.PropertyName == nameof(EditableInstruction.InstructionText)))
            {
                RebuildTreeViewSimple();
            }
        }

        private async Task RevertToHistoryAsync()
        {
            if (SelectedHistoryEntry == null) return;
            if (!await ShowRevertConfirmationAsync()) return;
            _isReverting = true;
            _suppressInstructionEvents = true; // sopprimi durante la ricostruzione
            OperationDescription = "Ripristino versione...";
            IsProcessing = true;
            try
            {
                // Qualsiasi placeholder di taglio va eliminato al revert
                ClearCutVisualState();
                RecomputeDiff();
                var snap = _historyService.RevertTo(SelectedHistoryEntry.Id);
                EditableInstructions.Clear();
                var svc = _orchestrator.GetOpCodeService();
                foreach (var s in snap.OrderBy(s => s.Number))
                {
                    var e = new EditableInstruction();
                    e.BeginSilentUpdate();
                    e.Number = s.Number;
                    e.OpCode = s.OpCode;
                    e.Name = s.Name;
                    e.SetParameters(s.Parameters);
                    var info = svc.GetOpCodeInfo(s.OpCode);
                    if (info != null) e.ParamCount = info.ParamCount;
                    e.EndSilentUpdate();
                    EditableInstructions.Add(e);
                }

                // Disable modification tracking on revert-loaded instructions
                foreach (var instr in EditableInstructions)
                    instr.DisableModificationTracking();

                RenumberInstructions();
                ClearPendingChanges();
                RebuildInstructionViewSimple();
                RebuildTreeViewSimple();
                await ApplyChangesToFile(); // sincronizza modello file
                ResetUnsavedChanges();
                // Il nuovo stato diventa baseline: nessun placeholder dopo revert
                CaptureBaseline();
                RecomputeDiff();
                AddHistory(HistoryActionType.Revert, $"Revert a #{SelectedHistoryEntry.Id}");
            }
            catch (Exception ex)
            {
                StatusText = $"Errore revert: {ex.Message}";
#if DEBUG
                Debug.WriteLine($"[DEBUG] Errore revert: {ex}");
#endif
            }
            finally
            {
                _suppressInstructionEvents = false;
                _isReverting = false;
                ResetUnsavedChanges(); // sicurezza finale
                SaveChangesCommand.NotifyCanExecuteChanged();
                DiscardChangesCommand.NotifyCanExecuteChanged();
                RevertToHistoryCommand.NotifyCanExecuteChanged();
                NotifyAllCommands();
                StatusText = $"{DateTime.Now:HH:mm:ss} - Revert completato";
                IsProcessing = false; OperationDescription = string.Empty;
#if DEBUG
                Debug.WriteLine("[DEBUG] Revert completato");
#endif
                // Re-enable modification tracking after UI idle
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    foreach (var instr in EditableInstructions)
                        instr.EnableModificationTracking();
                    ResetUnsavedChanges(); // assicurati stato pulito dopo riabilitazione tracking
                    _ignoreInstructionChanged = false; // da ora eventi validi
#if DEBUG
                    Debug.WriteLine("[DEBUG] Tracking riabilitato e stato pulito");
#endif
                    NotifyAllCommands();
                }, DispatcherPriority.Background);
            }
        }

        private async Task<bool> ShowRevertConfirmationAsync()
        {
            if (_currentWindow == null) return true; // fallback: procedi
            var tcs = new TaskCompletionSource<bool>();
            var dialog = new Window
            {
                Title = "Conferma Revert",
                Width = 360,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var cancelBtn = new Button { Content = "Annulla" };
            cancelBtn.Click += (_, __) => { tcs.TrySetResult(false); dialog.Close(); };
            var revertBtn = new Button { Content = "Revert" };
            revertBtn.Click += (_, __) => { tcs.TrySetResult(true); dialog.Close(); };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Children = { cancelBtn, revertBtn }
            };

            dialog.Content = new StackPanel
            {
                Spacing = 12,
                Margin = new Avalonia.Thickness(16),
                Children =
                {
                    new TextBlock{ Text = "Vuoi ripristinare lo stato selezionato?", TextWrapping = TextWrapping.Wrap },
                    buttons
                }
            };

            _ = dialog.ShowDialog(_currentWindow); // non bloccare thread chiamante
            return await tcs.Task;
        }

        private void BuildDiffFromSelection()
        {
            if (SelectedHistoryEntry == null)
            {
                DiffText = string.Empty;
                return;
            }
            try
            {
                var index = HistoryEntries.IndexOf(SelectedHistoryEntry);
                HistoryEntry? previous = null;
                if (index >= 0 && index + 1 < HistoryEntries.Count)
                    previous = HistoryEntries[index + 1];

                RecomputeDiff(); // Recompute diff after removing instruction
                if (previous == null)
                {
                    DiffText = $"Entry #{SelectedHistoryEntry.Id} (nessun snapshot precedente)\nNessuna diff disponibile";
                    return;
                }

                var oldSnap = previous.Snapshot;
                var newSnap = SelectedHistoryEntry.Snapshot;

                string Sig(InstructionSnapshot s) => $"{s.OpCode}|{s.Name}|{string.Join(',', s.Parameters)}";
                var oldSigs = oldSnap.Select(Sig).ToList();
                var newSigs = newSnap.Select(Sig).ToList();

                // Greedy match ignoring order to detect pure moves
                var newSigUsage = new Dictionary<int, int>(); // maps new index used count (occurrence)
                var matchedOld = new int[oldSnap.Count]; // store matched new index or -1
                for (int i = 0; i < matchedOld.Length; i++) matchedOld[i] = -1;

                for (int oi = 0; oi < oldSnap.Count; oi++)
                {
                    var sig = oldSigs[oi];
                    for (int ni = 0; ni < newSnap.Count; ni++)
                    {
                        if (newSigs[ni] == sig && !newSigUsage.ContainsKey(ni))
                        {
                            matchedOld[oi] = ni;
                            newSigUsage[ni] = 1;
                            break;
                        }
                    }
                }

                var removed = new List<InstructionSnapshot>();
                for (int oi = 0; oi < oldSnap.Count; oi++) if (matchedOld[oi] == -1) removed.Add(oldSnap[oi]);
                var added = new List<InstructionSnapshot>();
                for (int ni = 0; ni < newSnap.Count; ni++) if (!newSigUsage.ContainsKey(ni)) added.Add(newSnap[ni]);

                var sb = new StringBuilder();
                sb.AppendLine($"Diff #{previous.Id} -> #{SelectedHistoryEntry.Id} (ignora semplice rinumerazione)");
                sb.AppendLine(new string('-', 65));

                int changeCount = 0;
                foreach (var r in removed)
                {
                    changeCount++;
                    sb.AppendLine($"- {r.Number:D3} {r.Name} {FormatParams(r.Parameters)}");
                }
                foreach (var a in added)
                {
                    changeCount++;
                    sb.AppendLine($"+ {a.Number:D3} {a.Name} {FormatParams(a.Parameters)}");
                }

                // Pure move detection (no real add/remove; signatures identical count)
                bool isPureReorder = changeCount == 0 && oldSnap.Count == newSnap.Count && oldSnap.Count > 0;
                if (isPureReorder)
                {
                    // Build movement list: match by same signature sequence order preserving first occurrences
                    var moves = new List<string>();
                    for (int oi = 0; oi < oldSnap.Count; oi++)
                    {
                        int ni = matchedOld[oi];
                        if (ni >= 0 && oi != ni)
                        {
                            var instrOld = oldSnap[oi];
                            var instrNew = newSnap[ni];
                            // report move using original number and new number
                            moves.Add($"mv {instrOld.Number:D3} -> {instrNew.Number:D3} {instrOld.Name}");
                        }
                    }
                    if (moves.Count > 0)
                    {
                        sb.AppendLine("Spostamenti:");
                        foreach (var m in moves) sb.AppendLine("  " + m);
                    }
                    else
                    {
                        sb.AppendLine("(Solo rinumerazione senza spostamenti)");
                    }
                }
                else
                {
                    // Heuristic modifications pairing removed & added when counts equal
                    if (removed.Count == added.Count && removed.Count > 0)
                    {
                        sb.AppendLine("Modifiche:");
                        for (int i = 0; i < removed.Count; i++)
                        {
                            var r = removed[i];
                            var a = added[i];
                            sb.AppendLine($"~ {r.Number:D3}->{a.Number:D3} {r.Name} -> {a.Name}");
                            int maxP = Math.Max(r.Parameters.Length, a.Parameters.Length);
                            for (int p = 0; p < maxP; p++)
                            {
                                int ov = p < r.Parameters.Length ? r.Parameters[p] : 0;
                                int nv = p < a.Parameters.Length ? a.Parameters[p] : 0;
                                if (ov != nv)
                                    sb.AppendLine($"    P{p}: {ov} -> {nv}");
                            }
                        }
                    }
                    if (changeCount == 0)
                        sb.AppendLine("(Nessuna differenza significativa)");
                }

                DiffText = sb.ToString();
            }
            catch (Exception ex)
            {
                DiffText = $"Errore diff: {ex.Message}";
            }
        }

        private string FormatParams(int[] pars)
        {
            if (pars == null || pars.Length == 0) return string.Empty;
            return string.Join(',', pars.Where(p => p != 0));
        }

        private void AppendPending(PendingChangeType type, string detail)
        {
            _pendingChanges.Add(new PendingChange(type, detail));
            UpdatePendingSummary();
        }
        private string BuildPendingSummary()
        {
            if (_pendingChanges.Count == 0) return "Nessuna modifica strutturale";
            int adds = _pendingChanges.Count(c => c.Type == PendingChangeType.Add);
            int removes = _pendingChanges.Count(c => c.Type == PendingChangeType.Remove);
            int moves = _pendingChanges.Count(c => c.Type == PendingChangeType.Move);
            var parts = new List<string>();
            if (adds > 0) parts.Add($"Aggiunte: {adds}");
            if (removes > 0) parts.Add($"Rimosse: {removes}");
            if (moves > 0) parts.Add($"Spostate: {moves}");
            return string.Join(", ", parts);
        }
        private void ClearPendingChanges()
        {
            _pendingChanges.Clear();
            UpdatePendingSummary();
        }
        private void UpdatePendingSummary()
        {
            if (!HasUnsavedChanges || _pendingChanges.Count == 0)
            {
                PendingChangesSummary = HasUnsavedChanges ? "Modifiche presenti" : "Nessuna modifica";
            }
            else
            {
                PendingChangesSummary = BuildPendingSummary();
            }
        }
                // Aggiorna riepilogo quando cambia stato globale modifiche
                // (Rimosso metodo parziale di change notification; gestione integrata nel setter.)
        private bool _isReverting = false; // flag per ignorare eventi durante revert
        private bool _suppressInstructionEvents = false; // sopprime OnInstructionChanged temporaneamente
        private bool _ignoreInstructionChanged = false; // ignora eventi temporaneamente (post-revert)

        private void ResetUnsavedChanges()
        {
            foreach (var instr in EditableInstructions)
                instr.IsModified = false;
            HasUnsavedChanges = false;
#if DEBUG
            Debug.WriteLine("[DEBUG] ResetUnsavedChanges eseguito");
#endif
        }

        // === Ripristino proprietà e metodi mancanti ===
        private readonly HistoryService _historyService = new();
        public ObservableCollection<HistoryEntry> HistoryEntries { get; } = new();
        public IAsyncRelayCommand RevertToHistoryCommand { get; } // già inizializzato nel costruttore
        private HistoryEntry? _selectedHistoryEntry;
        public HistoryEntry? SelectedHistoryEntry { get => _selectedHistoryEntry; set { this.RaiseAndSetIfChanged(ref _selectedHistoryEntry, value); RevertToHistoryCommand.NotifyCanExecuteChanged(); BuildDiffFromSelection(); this.RaisePropertyChanged(nameof(CanRevertSelected)); } }

        private void AddHistory(HistoryActionType actionType, string description, bool force = false)
        {
            var entry = _historyService.CaptureSnapshot(EditableInstructions, actionType, description, force);
            if (!HistoryEntries.Contains(entry)) HistoryEntries.Insert(0, entry);
            this.RaisePropertyChanged(nameof(CanRevertSelected));
            RevertToHistoryCommand.NotifyCanExecuteChanged();
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
        private void NotifyAllCommands() => NotificationAllCommands();
        private void NotificationAllCommands()
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
            RevertToHistoryCommand.NotifyCanExecuteChanged();
            _applySelectedParameterOptionCommand?.NotifyCanExecuteChanged();
            ApplyResourceOptionCommand?.NotifyCanExecuteChanged();
            CopyInstructionsCommand.NotifyCanExecuteChanged();
            CutInstructionsCommand.NotifyCanExecuteChanged();
            PasteInstructionsCommand.NotifyCanExecuteChanged();
            CancelCutCommand.NotifyCanExecuteChanged();
        }
        private void UpdateStatus(string msg)
        {
            StatusText = $"{DateTime.Now:HH:mm:ss} - {msg}";
        }
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} bytes";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
        private void RenumberInstructions()
        {
            for (int i = 0; i < EditableInstructions.Count; i++)
                EditableInstructions[i].Number = i + 1;
        }
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

        private int _parameterPickerIndex;
        public int ParameterPickerIndex { get => _parameterPickerIndex; set { this.RaiseAndSetIfChanged(ref _parameterPickerIndex, value); RefreshParameterOptions(); } }
        private string _parameterPickerFilter = string.Empty;
        public string ParameterPickerFilter { get => _parameterPickerFilter; set { this.RaiseAndSetIfChanged(ref _parameterPickerFilter, value); RefreshParameterOptions(); } }
        private ResourceOption? _selectedParameterOption;
        public ResourceOption? SelectedParameterOption { get => _selectedParameterOption; set { this.RaiseAndSetIfChanged(ref _selectedParameterOption, value); _applySelectedParameterOptionCommand?.NotifyCanExecuteChanged(); } }
        private void RefreshParameterOptions()
        {
            ParameterPickerOptions.Clear();
            if (SelectedInstruction == null) return;
            var svc = _orchestrator.GetOpCodeService();
            var raw = svc.GetParamOptions(SelectedInstruction.OpCode, ParameterPickerIndex);
            var filtered = string.IsNullOrWhiteSpace(ParameterPickerFilter) ? raw : svc.FilterParamOptions(SelectedInstruction.OpCode, ParameterPickerIndex, ParameterPickerFilter);
            foreach (var opt in filtered.OrderBy(o => o.Id.HasValue ? 0 : 1).ThenBy(o => o.Id).ThenBy(o => o.Display))
                ParameterPickerOptions.Add(opt);
            _applySelectedParameterOptionCommand?.NotifyCanExecuteChanged();
        }
        private void RefreshResourceExplorer()
        {
            ResourceExplorerOptions.Clear();
            if (SelectedInstruction == null || SelectedParameterIndex < 0) return;
            var svc = _orchestrator.GetOpCodeService();
            IEnumerable<ResourceOption> baseOpts = svc.GetParamOptions(SelectedInstruction.OpCode, SelectedParameterIndex);
            if (!baseOpts.Any() && !string.IsNullOrWhiteSpace(SelectedInstruction.Name))
                baseOpts = svc.GetParamOptionsByName(SelectedInstruction.Name, SelectedParameterIndex);
            if (!string.IsNullOrWhiteSpace(ResourceSearchText))
            {
                var filter = ResourceSearchText.Trim();
                bool numeric = int.TryParse(filter, out var num);
                baseOpts = baseOpts.Where(o => (numeric && o.Id == num) || o.Display.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }
            foreach (var o in baseOpts.OrderBy(o => o.Id).ThenBy(o => o.Display))
                ResourceExplorerOptions.Add(o);
        }
        private void ApplySelectedResourceOption(ResourceOption? opt)
        {
            if (SelectedInstruction == null || SelectedParameterIndex < 0 || opt?.Id == null) return;
            var val = opt.Id.Value;
            if (SelectedParameterIndex < SelectedInstruction.ParameterEntries.Count)
            {
                SelectedInstruction.ParameterEntries[SelectedParameterIndex].Value = val;
                SelectedInstruction.IsModified = true;
                HasUnsavedChanges = true;
                StatusText = $"Param {SelectedParameterIndex} -> {val}";
                RefreshResourceExplorer();
            }
        }

        private void ApplySelectedParameterOption()
        {
            if (SelectedInstruction == null || SelectedParameterOption?.Id == null) return;
            int val = SelectedParameterOption.Id.Value;
            if (ParameterPickerIndex >= 0 && ParameterPickerIndex < SelectedInstruction.ParameterEntries.Count)
            {
                SelectedInstruction.ParameterEntries[ParameterPickerIndex].Value = val;
                SelectedInstruction.IsModified = true;
                HasUnsavedChanges = true;
                StatusText = $"Param {ParameterPickerIndex} -> {val}";
            }
        }

        public ContentTabsControlViewModel ContentTabsViewModel => ContentTabsControlViewModel; // alias per XAML legacy

        // Update selection list from UI (multi-select)
        public void UpdateSelectedInstructions(List<EditableInstruction> list)
        {
            _selectedInstructions = list ?? new List<EditableInstruction>();
            this.RaisePropertyChanged(nameof(SelectedInstructions));
            NotifyAllCommands();
        }

        private const string ClipboardSignature = "FVAPP-INSTR:";

        private async Task CopyInstructionsAsync()
        {
            if (_selectedInstructions.Count == 0 || _currentWindow?.Clipboard == null) return;
            // Clear previous cut state if any
            ClearCutVisualState();
            var payload = _selectedInstructions
                .OrderBy(i => i.Number)
                .Select(i => new PasteInstructionDto
                {
                    OpCode = i.OpCode,
                    Name = i.Name,
                    Params = i.GetParameters().Take(i.ParamCount).ToArray()
                }).ToList();
            var json = JsonSerializer.Serialize(payload);
            await _currentWindow.Clipboard.SetTextAsync(ClipboardSignature + json);
            UpdateStatus($"Copiate {_selectedInstructions.Count} istruzioni");
        }

        private async Task CutInstructionsAsync()
        {
            if (_selectedInstructions.Count == 0) return;
            // Copy but keep items; mark visually
            await CopyInstructionsAsync();
            ClearCutVisualState(); // ensure no previous leftover
            _cutBuffer = _selectedInstructions.ToList();
            foreach (var instr in _cutBuffer)
            {
                instr.IsCutPending = true;
            }
            UpdateStatus($"Tagliate (in attesa incolla) {_cutBuffer.Count} istruzioni");
        }

        private async Task PasteInstructionsAsync()
        {
            if (_currentWindow?.Clipboard == null) return;
            var text = await _currentWindow.Clipboard.TryGetTextAsync();
            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith(ClipboardSignature)) return;
            var json = text.Substring(ClipboardSignature.Length);
            List<PasteInstructionDto>? items;
            try
            {
                items = JsonSerializer.Deserialize<List<PasteInstructionDto>>(json);
            }
            catch
            {
                UpdateStatus("Formato clipboard non valido");
                return;
            }
            if (items == null || items.Count == 0) return;

            int insertIndex = SelectedInstruction != null ? EditableInstructions.IndexOf(SelectedInstruction) + 1 : EditableInstructions.Count;
            var svc = _orchestrator.GetOpCodeService();
            foreach (var dto in items)
            {
                var info = svc.GetOpCodeByName(dto.Name) ?? svc.GetOpCodeInfo(dto.OpCode);
                var newInstr = new EditableInstruction
                {
                    OpCode = info?.Id ?? dto.OpCode,
                    Name = info?.Name ?? dto.Name,
                    ParamCount = info?.ParamCount ?? 8,
                    IsModified = true
                };
                newInstr.SetParameters(dto.Params ?? Array.Empty<int>());
                EditableInstructions.Insert(insertIndex++, newInstr);
            }
            // If we have a cut buffer matching the clipboard content, remove originals now
            if (_cutBuffer?.Count > 0)
            {
                foreach (var instr in _cutBuffer)
                {
                    EditableInstructions.Remove(instr);
                }
                _cutBuffer.Clear();
                ClearCutVisualState();
            }
            RenumberInstructions();
            HasUnsavedChanges = true;
            UpdateStatus($"Incollate {items.Count} istruzioni");
            RebuildInstructionViewSimple();
            RebuildTreeViewSimple();
            RecomputeDiff();
        }

        private List<EditableInstruction>? _cutBuffer;
        private void ClearCutVisualState()
        {
            if (_cutBuffer == null) return;
            foreach (var instr in _cutBuffer)
                instr.IsCutPending = false;
            _cutBuffer.Clear();
            CancelCutCommand.NotifyCanExecuteChanged();
        }

        private Task CancelCutAsync()
        {
            ClearCutVisualState();
            UpdateStatus("Taglio annullato");
            RecomputeDiff();
            return Task.CompletedTask;
        }

        // Baseline snapshot for visual diff
        private void CaptureBaseline()
        {
            _baseline = EditableInstructions
                .Select(i => (i.Number, i.OpCode, i.GetParameters(), i.Name))
                .ToList();
            _removedNumbers.Clear();
            foreach (var i in EditableInstructions)
                i.DiffKind = InstructionDiffKind.Unchanged;
        }

        private void RecomputeDiff()
        {
            var currentMap = EditableInstructions.ToDictionary(i => i.Number);
            var baselineNumbers = _baseline.Select(b => b.Number).ToHashSet();

            // Removed instructions (in baseline but not in current list)
            _removedNumbers = new HashSet<int>(baselineNumbers.Except(currentMap.Keys));

            // Added or Modified
            foreach (var i in EditableInstructions)
            {
                if (!baselineNumbers.Contains(i.Number))
                {
                    i.DiffKind = InstructionDiffKind.Added;
                    continue;
                }
                var b = _baseline.First(x => x.Number == i.Number);
                if (b.OpCode != i.OpCode || b.Name != i.Name || !ParamsEqual(b.Params, i.GetParameters()))
                    i.DiffKind = i.DiffKind == InstructionDiffKind.Added ? InstructionDiffKind.Added : InstructionDiffKind.Modified;
                else if (i.DiffKind != InstructionDiffKind.CutPending)
                    i.DiffKind = InstructionDiffKind.Unchanged;
            }
        }

        private bool ParamsEqual(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private class PasteInstructionDto
        {
            public int OpCode { get; set; }
            public string Name { get; set; } = string.Empty;
            public int[]? Params { get; set; }
        }
    }
}