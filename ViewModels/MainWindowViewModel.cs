using System.IO;
using Avalonia.Controls;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using System;
using System.Windows.Input;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using FileViewerApp.Models;
using FileViewerApp.Services;
using System.Collections.ObjectModel;
using FileViewerApp.Enums;
using System.ComponentModel;
using System.Text;

namespace FileViewerApp.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private Window? _currentWindow;
        private readonly FileProcessorOrchestrator _orchestrator;

        // Current processed file
        private ProcessedFile? _currentProcessedFile;
        private byte[]? _currentFileBytes;

        // UI Properties
        private string _editorText = string.Empty;
        private string _hexView = string.Empty;
        private string _decodedData = string.Empty;
        private string _instructionView = string.Empty;
        private string _fileName = "Nessun file aperto";
        private string _fileSize = "0 bytes";
        private int _lineCount = 0;
        private int _charCount = 0;
        private string _fileEncoding = "UTF-8";
        private int _startOffset = 0x32; // 50 decimale
        private int _recordCount = 100;

        // TreeView properties
        private ObservableCollection<InstructionNode> _instructionTree = new();
        private InstructionNode? _selectedNode;

        // Status properties
        private string _statusText = "Pronto";
        private bool _isProcessing = false;
        private string _currentFileType = "Nessuno";

        // Editor properties
        private bool _isEditMode = false;
        private bool _hasUnsavedChanges = false;
        private bool _canEdit = false;
        private EditableInstruction? _selectedInstruction;

        public MainWindowViewModel()
        {
            // CREAZIONE DIRETTA SENZA SERVICE LOCATOR
            var opCodeService = new OpCodeService();
            _orchestrator = new FileProcessorOrchestrator(opCodeService);

            // Initialize commands
            OpenFileCommand = new AsyncRelayCommand(OpenFileAsync);
            CloseFileCommand = new AsyncRelayCommand(CloseFileAsync);
            SaveFileCommand = new AsyncRelayCommand(SaveFileAsync);
            ConvertFileCommand = new AsyncRelayCommand(ConvertFileAsync);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);

            // Editor commands
            AddInstructionCommand = new AsyncRelayCommand(AddInstructionAsync);
            RemoveInstructionCommand = new AsyncRelayCommand(RemoveInstructionAsync);
            MoveUpCommand = new AsyncRelayCommand(MoveUpAsync);
            MoveDownCommand = new AsyncRelayCommand(MoveDownAsync);
            SaveChangesCommand = new AsyncRelayCommand(SaveChangesAsync);
            DiscardChangesCommand = new AsyncRelayCommand(DiscardChangesAsync);
            DeleteInstructionCommand = new AsyncRelayCommand(DeleteSelectedInstructionAsync);

            // Set initial status
            UpdateStatus("Pronto - Seleziona un file per iniziare");

            // Subscribe to instruction changes
            EditableInstructions.CollectionChanged += OnInstructionsCollectionChanged;
        }

        public void SetWindow(Window window)
        {
            _currentWindow = window;
        }

        #region Properties for UI Binding

        public string EditorText
        {
            get => _editorText;
            set => this.RaiseAndSetIfChanged(ref _editorText, value);
        }

        public string HexView
        {
            get => _hexView;
            set => this.RaiseAndSetIfChanged(ref _hexView, value);
        }

        public string DecodedData
        {
            get => _decodedData;
            set => this.RaiseAndSetIfChanged(ref _decodedData, value);
        }

        public string InstructionView
        {
            get => _instructionView;
            set => this.RaiseAndSetIfChanged(ref _instructionView, value);
        }

        public string FileName
        {
            get => _fileName;
            set => this.RaiseAndSetIfChanged(ref _fileName, value);
        }

        public string FileSize
        {
            get => _fileSize;
            set => this.RaiseAndSetIfChanged(ref _fileSize, value);
        }

        public int LineCount
        {
            get => _lineCount;
            set => this.RaiseAndSetIfChanged(ref _lineCount, value);
        }

        public int CharCount
        {
            get => _charCount;
            set => this.RaiseAndSetIfChanged(ref _charCount, value);
        }

        public string FileEncoding
        {
            get => _fileEncoding;
            set => this.RaiseAndSetIfChanged(ref _fileEncoding, value);
        }

        public int StartOffset
        {
            get => _startOffset;
            set => this.RaiseAndSetIfChanged(ref _startOffset, value);
        }

        public int RecordCount
        {
            get => _recordCount;
            set => this.RaiseAndSetIfChanged(ref _recordCount, value);
        }

        public ObservableCollection<InstructionNode> InstructionTree
        {
            get => _instructionTree;
            set => this.RaiseAndSetIfChanged(ref _instructionTree, value);
        }

        public InstructionNode? SelectedNode
        {
            get => _selectedNode;
            set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set => this.RaiseAndSetIfChanged(ref _isProcessing, value);
        }

        public string CurrentFileType
        {
            get => _currentFileType;
            set => this.RaiseAndSetIfChanged(ref _currentFileType, value);
        }

        // Editor properties
        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                this.RaiseAndSetIfChanged(ref _isEditMode, value);
                UpdateCanEditState();
            }
        }

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value);
        }

        public bool CanEdit
        {
            get => _canEdit;
            set => this.RaiseAndSetIfChanged(ref _canEdit, value);
        }

        // CORREZIONE PRINCIPALE: Solo getter come nella documentazione Avalonia
        public ObservableCollection<EditableInstruction> EditableInstructions { get; } = new();

        public EditableInstruction? SelectedInstruction
        {
            get => _selectedInstruction;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedInstruction, value);
                UpdateMoveButtonsState();
            }
        }

        // Computed properties for buttons
        public bool CanRemoveInstruction => SelectedInstruction != null && CanEdit;
        public bool CanMoveUp => SelectedInstruction != null && CanEdit &&
            EditableInstructions.IndexOf(SelectedInstruction) > 0;
        public bool CanMoveDown => SelectedInstruction != null && CanEdit &&
            EditableInstructions.IndexOf(SelectedInstruction) < EditableInstructions.Count - 1;

        #endregion

        #region Commands

        public ICommand OpenFileCommand { get; }
        public ICommand CloseFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand ConvertFileCommand { get; }
        public ICommand RefreshCommand { get; }

        // Editor commands
        public ICommand AddInstructionCommand { get; }
        public ICommand RemoveInstructionCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand SaveChangesCommand { get; }
        public ICommand DiscardChangesCommand { get; }
        public ICommand DeleteInstructionCommand { get; }

        #endregion

        #region Command Implementations

        private async Task OpenFileAsync()
        {
            try
            {
                if (_currentWindow?.StorageProvider == null)
                {
                    UpdateStatus("Errore: StorageProvider non disponibile");
                    return;
                }

                if (!_currentWindow.StorageProvider.CanOpen)
                {
                    UpdateStatus("Errore: StorageProvider non supporta l'apertura di file");
                    return;
                }

                var options = new FilePickerOpenOptions
                {
                    Title = "Seleziona un file",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("File di programma")
                        {
                            Patterns = new[] { "*.dat", "*.bin", "*.txt" }
                        },
                        new FilePickerFileType("File di configurazione")
                        {
                            Patterns = new[] { "*.xml", "*.cfg" }
                        },
                        FilePickerFileTypes.All
                    }
                };

                IsProcessing = true;
                UpdateStatus("Seleziona un file...");

                var files = await _currentWindow.StorageProvider.OpenFilePickerAsync(options);

                if (files != null && files.Count > 0)
                {
                    var file = files[0];
                    var filePath = file.Path.LocalPath;

                    UpdateStatus($"Elaborazione file: {Path.GetFileName(filePath)}...");

                    // Carica i bytes del file per l'editing
                    _currentFileBytes = await File.ReadAllBytesAsync(filePath);

                    // USA L'ORCHESTRATOR PER PROCESSARE IL FILE
                    _currentProcessedFile = await _orchestrator.ProcessFileAsync(filePath);

                    // Aggiorna l'UI con i dati processati
                    await UpdateUIFromProcessedFile(_currentProcessedFile);

                    // Popola le istruzioni editabili
                    await PopulateEditableInstructions(_currentProcessedFile);

                    UpdateStatus($"File caricato: {_currentProcessedFile.FileType} - {_currentProcessedFile.Instructions.Count} istruzioni");
                }
                else
                {
                    UpdateStatus("Nessun file selezionato");
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Errore durante l'apertura del file: {ex.Message}";
                UpdateStatus(errorMsg);
                DecodedData = errorMsg;
                Console.WriteLine($"ERRORE: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task CloseFileAsync()
        {
            try
            {
                // Check for unsaved changes
                if (HasUnsavedChanges)
                {
                    // TODO: Show confirmation dialog
                    UpdateStatus("Attenzione: ci sono modifiche non salvate");
                }

                // Reset all properties
                _currentProcessedFile = null;
                _currentFileBytes = null;

                EditorText = string.Empty;
                HexView = string.Empty;
                DecodedData = string.Empty;
                InstructionView = string.Empty;
                FileName = "Nessun file aperto";
                FileSize = "0 bytes";
                LineCount = 0;
                CharCount = 0;
                FileEncoding = "UTF-8";
                StartOffset = 0x32;
                RecordCount = 100;
                CurrentFileType = "Nessuno";

                InstructionTree.Clear();
                SelectedNode = null;

                // Clear editor
                EditableInstructions.Clear();
                SelectedInstruction = null;
                IsEditMode = false;
                HasUnsavedChanges = false;

                UpdateStatus("File chiuso - Pronto per un nuovo file");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Errore durante la chiusura: {ex.Message}");
            }
        }

        private async Task SaveFileAsync()
        {
            try
            {
                if (_currentProcessedFile == null)
                {
                    UpdateStatus("Nessun file da salvare");
                    return;
                }

                if (_currentWindow?.StorageProvider == null)
                {
                    UpdateStatus("Errore: StorageProvider non disponibile");
                    return;
                }

                var options = new FilePickerSaveOptions
                {
                    Title = "Salva file",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("File binario (.dat)")
                        {
                            Patterns = new[] { "*.dat" }
                        },
                        new FilePickerFileType("File di testo (.txt)")
                        {
                            Patterns = new[] { "*.txt" }
                        },
                        new FilePickerFileType("File binario (.bin)")
                        {
                            Patterns = new[] { "*.bin" }
                        }
                    },
                    SuggestedFileName = Path.GetFileNameWithoutExtension(_currentProcessedFile.FileName)
                };

                IsProcessing = true;
                UpdateStatus("Salvataggio in corso...");

                var file = await _currentWindow.StorageProvider.SaveFilePickerAsync(options);

                if (file != null)
                {
                    var outputPath = file.Path.LocalPath;

                    // Se ci sono modifiche, applica le modifiche prima di salvare
                    if (HasUnsavedChanges)
                    {
                        await ApplyChangesToFile();
                    }

                    // USA L'ORCHESTRATOR PER SALVARE IL FILE
                    var savedData = await _orchestrator.SaveFileAsync(_currentProcessedFile, outputPath);

                    UpdateStatus($"File salvato: {Path.GetFileName(outputPath)} ({FormatFileSize(savedData.Length)})");
                }
                else
                {
                    UpdateStatus("Salvataggio annullato");
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Errore durante il salvataggio: {ex.Message}";
                UpdateStatus(errorMsg);
                Console.WriteLine($"ERRORE SALVATAGGIO: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task ConvertFileAsync()
        {
            try
            {
                if (_currentProcessedFile == null)
                {
                    UpdateStatus("Nessun file da convertire");
                    return;
                }

                if (_currentWindow?.StorageProvider == null)
                {
                    UpdateStatus("Errore: StorageProvider non disponibile");
                    return;
                }

                // Determina il formato di destinazione opposto al corrente
                var targetType = _currentProcessedFile.FileType == FileType.BinaryProgram
                    ? FileType.TextProgram
                    : FileType.BinaryProgram;

                var extension = targetType == FileType.BinaryProgram ? ".dat" : ".txt";
                var description = targetType == FileType.BinaryProgram ? "File binario" : "File di testo";

                var options = new FilePickerSaveOptions
                {
                    Title = $"Converti in {description}",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType($"{description} ({extension})")
                        {
                            Patterns = new[] { $"*{extension}" }
                        }
                    },
                    SuggestedFileName = Path.GetFileNameWithoutExtension(_currentProcessedFile.FileName) + extension
                };

                IsProcessing = true;
                UpdateStatus("Conversione in corso...");

                var file = await _currentWindow.StorageProvider.SaveFilePickerAsync(options);

                if (file != null)
                {
                    var outputPath = file.Path.LocalPath;

                    // Se ci sono modifiche, applica le modifiche prima di convertire
                    if (HasUnsavedChanges)
                    {
                        await ApplyChangesToFile();
                    }

                    // USA L'ORCHESTRATOR PER CONVERTIRE IL FILE
                    var convertedData = await _orchestrator.ConvertAsync(_currentProcessedFile, targetType, outputPath);

                    UpdateStatus($"File convertito: {Path.GetFileName(outputPath)} ({FormatFileSize(convertedData.Length)})");

                    // Mostra risultato conversione
                    DecodedData += $"\n\n=== CONVERSIONE COMPLETATA ===\n";
                    DecodedData += $"File convertito in: {outputPath}\n";
                    DecodedData += $"Dimensione: {FormatFileSize(convertedData.Length)}\n";
                    DecodedData += $"Formato: {targetType}\n";
                }
                else
                {
                    UpdateStatus("Conversione annullata");
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Errore durante la conversione: {ex.Message}";
                UpdateStatus(errorMsg);
                DecodedData += $"\n\nERRORE CONVERSIONE: {errorMsg}";
                Console.WriteLine($"ERRORE CONVERSIONE: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task RefreshAsync()
        {
            try
            {
                if (_currentProcessedFile == null)
                {
                    UpdateStatus("Nessun file da aggiornare");
                    return;
                }

                IsProcessing = true;
                UpdateStatus("Aggiornamento in corso...");

                // Riprocessa il file corrente
                _currentProcessedFile = await _orchestrator.ProcessFileAsync(_currentProcessedFile.FilePath, useCache: false);

                // Aggiorna l'UI
                await UpdateUIFromProcessedFile(_currentProcessedFile);

                // Ripopola le istruzioni editabili
                await PopulateEditableInstructions(_currentProcessedFile);

                UpdateStatus($"File aggiornato: {_currentProcessedFile.Instructions.Count} istruzioni");
            }
            catch (Exception ex)
            {
                var errorMsg = $"Errore durante l'aggiornamento: {ex.Message}";
                UpdateStatus(errorMsg);
                Console.WriteLine($"ERRORE REFRESH: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        #endregion

        // Sostituisci i metodi Editor Command Implementations con questa versione corretta:

        #region Editor Command Implementations

        private async Task AddInstructionAsync()
        {
            if (!CanEdit) return;

            var newInstruction = new EditableInstruction
            {
                Number = EditableInstructions.Count + 1,
                OpCode = 1,
                Name = "NULLA"
            };

            EditableInstructions.Add(newInstruction);
            HasUnsavedChanges = true;
            UpdateCanEditState();

            // AGGIUNTO: Aggiorna TreeView immediatamente
            await UpdateTreeViewFromInstructions();

            StatusText = $"Istruzione aggiunta - Totale: {EditableInstructions.Count}";
        }

        private async Task RemoveInstructionAsync()
        {
            if (SelectedInstruction == null || !CanEdit) return;

            EditableInstructions.Remove(SelectedInstruction);
            HasUnsavedChanges = true;

            // Rinumera le istruzioni
            RenumberInstructions();
            UpdateCanEditState();

            // AGGIUNTO: Aggiorna TreeView immediatamente
            await UpdateTreeViewFromInstructions();

            StatusText = $"Istruzione rimossa - Totale: {EditableInstructions.Count}";
        }

        private async Task DeleteSelectedInstructionAsync()
        {
            if (SelectedInstruction != null && CanEdit)
            {
                await RemoveInstructionAsync();
            }
        }

        private async Task MoveUpAsync()
        {
            if (SelectedInstruction == null || !CanMoveUp) return;

            var index = EditableInstructions.IndexOf(SelectedInstruction);
            EditableInstructions.Move(index, index - 1);
            HasUnsavedChanges = true;

            RenumberInstructions();
            UpdateMoveButtonsState();

            // AGGIUNTO: Aggiorna TreeView immediatamente
            await UpdateTreeViewFromInstructions();

            StatusText = "Istruzione spostata verso l'alto";
        }

        private async Task MoveDownAsync()
        {
            if (SelectedInstruction == null || !CanMoveDown) return;

            var index = EditableInstructions.IndexOf(SelectedInstruction);
            EditableInstructions.Move(index, index + 1);
            HasUnsavedChanges = true;

            RenumberInstructions();
            UpdateMoveButtonsState();

            // AGGIUNTO: Aggiorna TreeView immediatamente
            await UpdateTreeViewFromInstructions();

            StatusText = "Istruzione spostata verso il basso";
        }

        private async Task SaveChangesAsync()
        {
            if (!HasUnsavedChanges || _currentFileBytes == null) return;

            try
            {
                IsProcessing = true;
                StatusText = "Salvataggio modifiche...";

                await ApplyChangesToFile();

                // Resetta i flag di modifica
                foreach (var instruction in EditableInstructions)
                {
                    instruction.ResetModifications();
                }

                HasUnsavedChanges = false;
                IsProcessing = false;
                StatusText = "Modifiche salvate con successo";

                // Aggiorna le altre viste
                await UpdateHexViewAsync();
                await RefreshViews();

                // AGGIUNTO: Aggiorna TreeView per rimuovere i flag [MOD]
                await UpdateTreeViewFromInstructions();
            }
            catch (Exception ex)
            {
                IsProcessing = false;
                StatusText = $"Errore durante il salvataggio: {ex.Message}";
            }
        }

        private async Task DiscardChangesAsync()
        {
            if (!HasUnsavedChanges) return;

            try
            {
                StatusText = "Ripristino modifiche originali...";

                // Ripopola le istruzioni dal file originale
                if (_currentProcessedFile != null)
                {
                    await PopulateEditableInstructions(_currentProcessedFile);
                }

                HasUnsavedChanges = false;
                StatusText = "Modifiche scartate";

                // AGGIUNTO: Il TreeView verrà aggiornato da PopulateEditableInstructions
            }
            catch (Exception ex)
            {
                StatusText = $"Errore durante il ripristino: {ex.Message}";
            }
        }

        #endregion

        #region Helper Methods

        private async Task UpdateUIFromProcessedFile(ProcessedFile processedFile)
        {
            try
            {
                // Basic file info
                FileName = processedFile.FileName;
                FileSize = FormatFileSize(processedFile.FileSize);
                CurrentFileType = processedFile.FileType.ToString();

                // Content views
                HexView = processedFile.HexView;
                InstructionView = processedFile.TextualView;

                // Per file TXT, mostra il contenuto testuale originale nell'Editor
                if (processedFile.FileType == FileType.TextProgram)
                {
                    EditorText = System.Text.Encoding.UTF8.GetString(processedFile.RawContent);
                    LineCount = EditorText.Split('\n').Length;
                    CharCount = EditorText.Length;
                }
                else
                {
                    // Per file binari, mostra info del file
                    try
                    {
                        EditorText = System.Text.Encoding.UTF8.GetString(processedFile.RawContent);
                        LineCount = EditorText.Split('\n').Length;
                        CharCount = EditorText.Length;
                    }
                    catch
                    {
                        EditorText = $"File binario - vedere la vista Hex\n\nTipo: {processedFile.FileType}\nDimensione: {FormatFileSize(processedFile.FileSize)}";
                        LineCount = 0;
                        CharCount = 0;
                    }
                }

                // Instruction tree
                InstructionTree.Clear();
                foreach (var node in processedFile.InstructionTree)
                {
                    InstructionTree.Add(node);
                }

                // Expand tree
                ExpandAllNodes(InstructionTree);

                // Messages dettagliati
                var messages = string.Join("\n", processedFile.Messages);
                DecodedData = $"=== INFORMAZIONI FILE ===\n";
                DecodedData += $"Nome: {processedFile.FileName}\n";
                DecodedData += $"Tipo: {processedFile.FileType}\n";
                DecodedData += $"Dimensione originale: {FormatFileSize(processedFile.FileSize)}\n";
                DecodedData += $"Header: {processedFile.Header}\n";
                DecodedData += $"Istruzioni trovate: {processedFile.Instructions.Count}\n";

                // Per file TXT, mostra info sulla conversione binaria
                if (processedFile.FileType == FileType.TextProgram && !string.IsNullOrEmpty(processedFile.HexView))
                {
                    // Calcola dimensione del binario generato dalle righe hex
                    var hexLines = processedFile.HexView.Split('\n').Where(l => l.Contains("  ")).Count();
                    var estimatedBinarySize = hexLines * 16;
                    DecodedData += $"File binario generato: ~{estimatedBinarySize} bytes\n";
                }

                DecodedData += "\n=== MESSAGGI PROCESSING ===\n" + messages + "\n\n";

                if (processedFile.Instructions.Count > 0)
                {
                    DecodedData += $"=== PRIMA 20 ISTRUZIONI ===\n";
                    DecodedData += "Num | OpCode | Nome             | Parametri principali\n";
                    DecodedData += new string('-', 65) + "\n";

                    foreach (var instruction in processedFile.Instructions.Take(20))
                    {
                        var opCodeInfo = _orchestrator.GetOpCodeService().GetOpCodeInfo(instruction.OpCode);
                        var paramCount = opCodeInfo?.ParamCount ?? 3;
                        var significantParams = instruction.Parameters.Take(paramCount).ToArray();
                        var paramString = significantParams.Length > 0
                            ? string.Join(", ", significantParams)
                            : "(nessuno)";

                        DecodedData += $"{instruction.Number,3} | {instruction.OpCode,6} | {instruction.Name,-15} | {paramString}\n";
                    }

                    if (processedFile.Instructions.Count > 20)
                    {
                        DecodedData += $"... e altre {processedFile.Instructions.Count - 20} istruzioni\n";
                    }
                }
                else
                {
                    DecodedData += "=== NESSUNA ISTRUZIONE TROVATA ===\n";
                    DecodedData += "Possibili cause:\n";
                    DecodedData += "- Formato file non riconosciuto\n";
                    DecodedData += "- OpCodes non presenti nel dizionario\n";
                    DecodedData += "- Errore di parsing\n";
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore aggiornamento UI: {ex.Message}");
                DecodedData = $"ERRORE AGGIORNAMENTO UI: {ex.Message}\n\n{ex.StackTrace}";
            }
        }

        private async Task PopulateEditableInstructions(ProcessedFile processedFile)
        {
            EditableInstructions.Clear();

            foreach (var instruction in processedFile.Instructions)
            {
                var editableInstruction = new EditableInstruction
                {
                    Number = instruction.Number,
                    OpCode = instruction.OpCode,
                    Name = instruction.Name
                };

                // Imposta i parametri
                var parameters = instruction.Parameters.ToArray();
                editableInstruction.SetParameters(parameters);

                EditableInstructions.Add(editableInstruction);
            }

            // Abilita edit mode se ci sono istruzioni
            IsEditMode = EditableInstructions.Count > 0;
            HasUnsavedChanges = false;

            // Aggiorna TreeView
            await UpdateTreeViewFromInstructions();
        }


        private async Task UpdateTreeViewFromInstructions()
        {
            try
            {
                // Se non ci sono istruzioni editabili, non aggiornare l'albero
                if (EditableInstructions.Count == 0)
                {
                    return;
                }

                // Se l'albero è vuoto, non possiamo aggiornare (probabilmente non è ancora stato caricato un file)
                if (InstructionTree.Count == 0)
                {
                    return;
                }

                Console.WriteLine($"[DEBUG] Aggiornamento TreeView - Istruzioni: {EditableInstructions.Count}, Nodi albero: {InstructionTree.Count}");

                // Cerca e aggiorna solo i nodi delle istruzioni nell'albero esistente
                await UpdateInstructionNodesRecursively(InstructionTree);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore aggiornamento TreeView: {ex.Message}");
            }
        }

        // Aggiungi questo nuovo metodo helper:
        private async Task UpdateInstructionNodesRecursively(ObservableCollection<InstructionNode> nodes)
        {
            foreach (var node in nodes)
            {
                // Se questo nodo rappresenta un'istruzione, aggiornalo con i dati modificati
                if (TryGetInstructionFromNodeName(node.Name, out int instructionNumber))
                {
                    var editableInstruction = EditableInstructions.FirstOrDefault(i => i.Number == instructionNumber);
                    if (editableInstruction != null)
                    {
                        // Aggiorna solo il nome del nodo con i dati correnti
                        var modifiedFlag = editableInstruction.IsModified ? " [MOD]" : "";
                        var validFlag = editableInstruction.IsValid ? "" : " [ERR]";

                        // Mantieni il formato originale ma aggiorna i dati
                        node.Name = $"{editableInstruction.Number:D3}: {editableInstruction.Name} ({editableInstruction.OpCode}){modifiedFlag}{validFlag}";
                    }
                }

                // Ricorsivamente aggiorna i nodi figli
                if (node.Children.Count > 0)
                {
                    await UpdateInstructionNodesRecursively(node.Children);
                }
            }
        }

        // Aggiungi questo metodo helper per estrarre il numero istruzione dal nome del nodo:
        private bool TryGetInstructionFromNodeName(string nodeName, out int instructionNumber)
        {
            instructionNumber = 0;

            if (string.IsNullOrEmpty(nodeName)) return false;

            // Cerca pattern come "001: NOME_ISTRUZIONE" o "123: ALTRA_ISTRUZIONE"
            var match = System.Text.RegularExpressions.Regex.Match(nodeName, @"^(\d{1,3}):\s");
            if (match.Success && int.TryParse(match.Groups[1].Value, out instructionNumber))
            {
                return true;
            }

            return false;
        }

        private async Task ApplyChangesToFile()
        {
            if (_currentFileBytes == null || _currentProcessedFile == null) return;

            // Crea una copia del file bytes per le modifiche
            var modifiedBytes = new byte[_currentFileBytes.Length];
            Array.Copy(_currentFileBytes, modifiedBytes, _currentFileBytes.Length);

            // Applica le modifiche delle istruzioni al file
            foreach (var instruction in EditableInstructions.Where(i => i.IsModified))
            {
                int offset = _startOffset + ((instruction.Number - 1) * 9);

                if (offset + 8 < modifiedBytes.Length)
                {
                    // Scrivi OpCode
                    modifiedBytes[offset] = (byte)instruction.OpCode;

                    // Scrivi parametri
                    var parameters = instruction.GetParameters();
                    for (int i = 0; i < parameters.Length && offset + i + 1 < modifiedBytes.Length; i++)
                    {
                        modifiedBytes[offset + i + 1] = (byte)parameters[i];
                    }
                }
            }

            // Aggiorna il file corrente con le modifiche
            _currentFileBytes = modifiedBytes;

            // Aggiorna anche il ProcessedFile per mantenere consistenza
            _currentProcessedFile.RawContent = modifiedBytes;

            await Task.CompletedTask;
        }

        private async Task UpdateHexViewAsync()
        {
            if (_currentFileBytes == null) return;

            var hexBuilder = new StringBuilder();
            var maxBytes = Math.Min(_currentFileBytes.Length, 1024); // Limita a 1KB per performance

            for (int i = 0; i < maxBytes; i += 16)
            {
                // Address
                hexBuilder.Append($"{i:X8}: ");

                // Hex values
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < maxBytes)
                    {
                        hexBuilder.Append($"{_currentFileBytes[i + j]:X2} ");
                    }
                    else
                    {
                        hexBuilder.Append("   ");
                    }

                    if (j == 7) hexBuilder.Append(" ");
                }

                hexBuilder.Append(" |");

                // ASCII representation
                for (int j = 0; j < 16 && i + j < maxBytes; j++)
                {
                    byte b = _currentFileBytes[i + j];
                    hexBuilder.Append(b >= 32 && b <= 126 ? (char)b : '.');
                }

                hexBuilder.AppendLine("|");
            }

            if (_currentFileBytes.Length > maxBytes)
            {
                hexBuilder.AppendLine($"\n... (mostrando solo i primi {maxBytes} bytes di {_currentFileBytes.Length})");
            }

            HexView = hexBuilder.ToString();
            await Task.CompletedTask;
        }

        private void ExpandAllNodes(ObservableCollection<InstructionNode> nodes)
        {
            foreach (var node in nodes)
            {
                node.IsExpanded = true;
                if (node.Children.Count > 0)
                {
                    ExpandAllNodes(node.Children);
                }
            }
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} bytes";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            else
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        private void UpdateStatus(string message)
        {
            StatusText = $"{DateTime.Now:HH:mm:ss} - {message}";
            Console.WriteLine($"STATUS: {message}");
        }

        #region Editor Support Methods


        private void OnInstructionsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Quando vengono aggiunte nuove istruzioni
            if (e.NewItems != null)
            {
                foreach (EditableInstruction item in e.NewItems)
                {
                    item.InstructionChanged += OnInstructionChanged;
                }
            }

            // Quando vengono rimosse istruzioni
            if (e.OldItems != null)
            {
                foreach (EditableInstruction item in e.OldItems)
                {
                    item.InstructionChanged -= OnInstructionChanged;
                }
            }

            UpdateCanEditState();

            // AGGIUNTO: Aggiorna TreeView quando cambia la collezione (add/remove)
            Task.Run(async () => await UpdateTreeViewFromInstructions());
        }

        private void OnInstructionChanged(object? sender, PropertyChangedEventArgs e)
        {
            HasUnsavedChanges = EditableInstructions.Any(i => i.IsModified);
            UpdateMoveButtonsState();

            // Aggiorna anche le viste di sola lettura E il TreeView
            Task.Run(async () =>
            {
                await RefreshViews();
                await UpdateTreeViewFromInstructions();
            });
        }

        private async Task RefreshViews()
        {
            await Task.Delay(100); // Piccolo delay per evitare troppi aggiornamenti

            // Aggiorna la vista istruzioni
            var instructionBuilder = new StringBuilder();
            instructionBuilder.AppendLine("ISTRUZIONI DECODIFICATE (MODIFICATE)");
            instructionBuilder.AppendLine("====================================");
            instructionBuilder.AppendLine();

            foreach (var instruction in EditableInstructions.OrderBy(i => i.Number))
            {
                var modifiedFlag = instruction.IsModified ? " [MODIFICATA]" : "";
                var validFlag = instruction.IsValid ? "" : " [ERRORE]";

                var paramList = string.Join(", ", instruction.GetParameters());
                instructionBuilder.AppendLine($"{instruction.Number:D3}: {instruction.Name} ({instruction.OpCode}) [{paramList}]{modifiedFlag}{validFlag}");

                if (!instruction.IsValid)
                {
                    instructionBuilder.AppendLine($"     -> {instruction.ValidationSummary}");
                }
            }

            instructionBuilder.AppendLine();
            instructionBuilder.AppendLine($"Totale istruzioni: {EditableInstructions.Count}");
            instructionBuilder.AppendLine($"Istruzioni modificate: {EditableInstructions.Count(i => i.IsModified)}");
            instructionBuilder.AppendLine($"Istruzioni valide: {EditableInstructions.Count(i => i.IsValid)}");

            InstructionView = instructionBuilder.ToString();
        }

        private void RenumberInstructions()
        {
            for (int i = 0; i < EditableInstructions.Count; i++)
            {
                EditableInstructions[i].Number = i + 1;
            }
        }

        private void UpdateCanEditState()
        {
            CanEdit = IsEditMode && EditableInstructions.Count > 0;
            this.RaisePropertyChanged(nameof(CanRemoveInstruction));
            UpdateMoveButtonsState();
        }

        private void UpdateMoveButtonsState()
        {
            this.RaisePropertyChanged(nameof(CanMoveUp));
            this.RaisePropertyChanged(nameof(CanMoveDown));
        }

        #endregion

        #endregion
    }
}