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

namespace FileViewerApp.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private Window? _currentWindow;
        private readonly FileProcessorOrchestrator _orchestrator;

        // Current processed file
        private ProcessedFile? _currentProcessedFile;

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

            // Set initial status
            UpdateStatus("Pronto - Seleziona un file per iniziare");
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

        #endregion

        #region Commands

        public ICommand OpenFileCommand { get; }
        public ICommand CloseFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand ConvertFileCommand { get; }
        public ICommand RefreshCommand { get; }

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

                    // USA L'ORCHESTRATOR PER PROCESSARE IL FILE
                    _currentProcessedFile = await _orchestrator.ProcessFileAsync(filePath);

                    // Aggiorna l'UI con i dati processati
                    await UpdateUIFromProcessedFile(_currentProcessedFile);

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
                // Reset all properties
                _currentProcessedFile = null;

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

        #region Helper Methods


        // Nel metodo UpdateUIFromProcessedFile, aggiungi info di debug:

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

        #endregion
    }
}