using System.IO;
using Avalonia.Controls;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using System;
using System.Windows.Input;
using ReactiveUI;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using FileViewerApp.Models;
using FileViewerApp.Services;
using System.Collections.ObjectModel;

namespace FileViewerApp.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private Window? _currentWindow;
        private string _editorText = string.Empty;
        private string _hexView = string.Empty;
        private string _decodedData = string.Empty;
        private string _instructionView = string.Empty;
        private string _fileName = "Nessun file aperto";
        private string _fileSize = "0 bytes";
        private int _lineCount = 0;
        private int _charCount = 0;
        private string _fileEncoding = "UTF-8";
        private int _startOffset = 0x32; // 50 decimale - CORRETTO
        private int _recordCount = 100;
        private byte[]? _currentFileBytes;

        // Servizio per gestire gli OpCodes
        private readonly OpCodeService _opCodeService = new();

        // TreeView properties
        private ObservableCollection<InstructionNode> _instructionTree = new();
        private InstructionNode? _selectedNode;

        public MainWindowViewModel()
        {
            OpenFileCommand = new AsyncRelayCommand(OpenFileAsync);
            CloseFileCommand = new AsyncRelayCommand(CloseFileAsync);
            UpdateDecoderCommand = new AsyncRelayCommand(UpdateDecoderAsync);
        }

        public void SetWindow(Window window)
        {
            _currentWindow = window;
        }

        // Proprietà per il binding
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

        // TreeView Properties
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

        // Comandi
        public ICommand OpenFileCommand { get; }
        public ICommand CloseFileCommand { get; }
        public ICommand UpdateDecoderCommand { get; }

        private async Task OpenFileAsync()
        {
            try
            {
                if (_currentWindow?.StorageProvider == null)
                {
                    Console.WriteLine("StorageProvider non disponibile");
                    return;
                }

                if (!_currentWindow.StorageProvider.CanOpen)
                {
                    Console.WriteLine("StorageProvider non supporta l'apertura di file");
                    return;
                }

                var options = new FilePickerOpenOptions
                {
                    Title = "Seleziona un file",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        FilePickerFileTypes.All
                    }
                };

                var files = await _currentWindow.StorageProvider.OpenFilePickerAsync(options);

                if (files != null && files.Count > 0)
                {
                    var file = files[0];

                    if (file == null)
                    {
                        Console.WriteLine("File selezionato non valido");
                        return;
                    }

                    await using var stream = await file.OpenReadAsync();

                    // Leggi i bytes per analisi hex e decoder
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    _currentFileBytes = memoryStream.ToArray();

                    // Prova a leggere come testo per l'editor
                    try
                    {
                        stream.Position = 0;
                        using var reader = new StreamReader(stream);
                        var text = await reader.ReadToEndAsync();
                        EditorText = text;

                        // Aggiorna le proprietà del file
                        LineCount = text.Split('\n').Length;
                        CharCount = text.Length;
                    }
                    catch
                    {
                        // Se non è testo, mostra hex
                        EditorText = "File binario - vedere la vista Hex";
                        LineCount = 0;
                        CharCount = 0;
                    }

                    FileName = file.Name;

                    // Calcola dimensione file
                    if (_currentFileBytes.Length < 1024)
                        FileSize = $"{_currentFileBytes.Length} bytes";
                    else if (_currentFileBytes.Length < 1024 * 1024)
                        FileSize = $"{_currentFileBytes.Length / 1024.0:F1} KB";
                    else
                        FileSize = $"{_currentFileBytes.Length / (1024.0 * 1024.0):F1} MB";

                    // Genera la vista hex
                    HexView = GenerateHexView(_currentFileBytes);

                    // Genera automaticamente il decoder
                    await UpdateDecoderAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante l'apertura del file: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private async Task CloseFileAsync()
        {
            // Resetta tutte le proprietà
            EditorText = string.Empty;
            HexView = string.Empty;
            DecodedData = string.Empty;
            InstructionView = string.Empty;
            FileName = "Nessun file aperto";
            FileSize = "0 bytes";
            LineCount = 0;
            CharCount = 0;
            FileEncoding = "UTF-8";
            StartOffset = 0x32; // 50 decimale - CORRETTO
            RecordCount = 100;
            _currentFileBytes = null;
            InstructionTree.Clear();
            SelectedNode = null;

            await Task.CompletedTask;
        }

        private string GenerateHexView(byte[] bytes)
        {
            var hexBuilder = new StringBuilder();

            for (int i = 0; i < bytes.Length; i += 16)
            {
                // Offset
                hexBuilder.Append($"{i:X8}  ");

                // Hex bytes
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < bytes.Length)
                    {
                        hexBuilder.Append($"{bytes[i + j]:X2} ");
                        if (j == 7) hexBuilder.Append(" ");
                    }
                    else
                    {
                        hexBuilder.Append("   ");
                        if (j == 7) hexBuilder.Append(" ");
                    }
                }

                // ASCII representation
                hexBuilder.Append(" |");
                for (int j = 0; j < 16 && i + j < bytes.Length; j++)
                {
                    byte b = bytes[i + j];
                    hexBuilder.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                hexBuilder.AppendLine("|");
            }

            return hexBuilder.ToString();
        }

        private async Task UpdateDecoderAsync()
        {
            if (_currentFileBytes == null || _currentFileBytes.Length == 0)
            {
                DecodedData = "Nessun file caricato";
                InstructionView = "Nessun file caricato";
                InstructionTree.Clear();
                return;
            }

            try
            {
                // Carica le definizioni degli OpCode usando il servizio
                var opCodeInfos = await _opCodeService.LoadOpCodeDefinitionsAsync();

                // Clear existing tree
                InstructionTree.Clear();

                // Genera vista tecnica (decoder)
                var decodedBuilder = new StringBuilder();
                decodedBuilder.AppendLine("DECODER CODICE PROGRAMMA");
                decodedBuilder.AppendLine("=" + new string('=', 80));

                // Analizza header
                string headerText = "";
                if (_currentFileBytes.Length >= 8)
                {
                    headerText = Encoding.ASCII.GetString(_currentFileBytes, 0, 8);
                    decodedBuilder.AppendLine($"Header: '{headerText.Trim()}'");
                    decodedBuilder.AppendLine($"Start Offset: 0x{StartOffset:X8} ({StartOffset})");
                    decodedBuilder.AppendLine();
                }

                decodedBuilder.AppendLine("ISTRUZIONI DECODIFICATE (Vista tecnica):");
                decodedBuilder.AppendLine("-" + new string('-', 80));
                decodedBuilder.AppendLine("Num | Offset | OpCode/Nome         | Parametri | IndentMode");
                decodedBuilder.AppendLine("-" + new string('-', 80));

                // Genera vista istruzioni semplificata
                var instructionBuilder = new StringBuilder();
                instructionBuilder.AppendLine("// PROGRAMMA DECODIFICATO");
                instructionBuilder.AppendLine("// " + new string('=', 60));
                instructionBuilder.AppendLine($"// File: {headerText.Trim()}");
                instructionBuilder.AppendLine();

                // Stack per gestire la gerarchia della TreeView
                var nodeStack = new Stack<InstructionNode>();
                var rootNode = new InstructionNode
                {
                    Name = $"PROGRAMMA: {headerText.Trim()}",
                    Details = $"File binario - {_currentFileBytes.Length} bytes",
                    IsExpanded = true
                };
                InstructionTree.Add(rootNode);
                nodeStack.Push(rootNode);

                // Usa StartOffset corretto
                int offset = StartOffset;
                int instructionNumber = 1;
                int maxInstructions = RecordCount;
                int indentLevel = 0;

                while (offset + 36 <= _currentFileBytes.Length && instructionNumber <= maxInstructions)
                {
                    // Leggi OpCode
                    var opCodeBytes = new byte[4];
                    Array.Copy(_currentFileBytes, offset, opCodeBytes, 0, 4);
                    int opCode = BitConverter.ToInt32(opCodeBytes, 0);

                    if (opCode == 0)
                    {
                        offset += 36;
                        continue;
                    }

                    // Trova informazioni sull'OpCode usando il servizio
                    var opCodeInfo = _opCodeService.GetOpCodeInfo(opCode);
                    string opName = opCodeInfo?.Name ?? $"UNKNOWN_{opCode}";
                    int expectedParams = opCodeInfo?.ParamCount ?? 8;
                    var indentMode = opCodeInfo?.IndentMode ?? IndentMode.None;

                    // Leggi i parametri
                    var parameters = new List<int>();
                    for (int i = 1; i <= 8 && offset + (i * 4) + 3 < _currentFileBytes.Length; i++)
                    {
                        var paramBytes = new byte[4];
                        Array.Copy(_currentFileBytes, offset + (i * 4), paramBytes, 0, 4);
                        parameters.Add(BitConverter.ToInt32(paramBytes, 0));
                    }

                    var significantParams = parameters.Take(expectedParams).ToArray();
                    string paramString = significantParams.Length > 0 ?
                        string.Join(", ", significantParams) : "(nessun parametro)";

                    // *** GESTIONE INDENTAZIONE BASATA SU XML ***
                    // Gestisci la chiusura di blocchi PRIMA di processare il comando corrente
                    if (indentMode == IndentMode.RemoveIndent || indentMode == IndentMode.RemoveAndAddIndent)
                    {
                        if (nodeStack.Count > 1) // Non rimuovere il root
                        {
                            nodeStack.Pop();
                        }
                        indentLevel = Math.Max(0, indentLevel - 1);
                    }

                    // Calcola indentazione per la vista testo
                    string indent = new string(' ', indentLevel * 4); // 4 spazi per livello

                    // Vista tecnica con IndentMode
                    decodedBuilder.Append($"{instructionNumber,3:D} | 0x{offset:X6} | ");
                    decodedBuilder.Append($"{opCode,3} {opName,-15} ");
                    decodedBuilder.AppendLine($"| {paramString,-20} | {indentMode}");

                    // Vista istruzioni con indentazione
                    instructionBuilder.AppendLine($"{indent}{opName}");

                    // Crea il nodo per questa istruzione
                    var currentNode = new InstructionNode
                    {
                        Name = opName,
                        InstructionNumber = instructionNumber,
                        Offset = offset,
                        OpCode = opCode,
                        Parameters = paramString,
                        Details = $"Offset: 0x{offset:X6} | OpCode: {opCode} | IndentMode: {indentMode} | {opCodeInfo?.Category ?? "Unknown"}",
                        IsExpanded = true
                    };

                    // Aggiungi al nodo parent corrente
                    var parentNode = nodeStack.Peek();
                    parentNode.Children.Add(currentNode);

                    // Gestisci l'apertura di nuovi blocchi DOPO aver processato il comando
                    if (indentMode == IndentMode.AddIndent || indentMode == IndentMode.RemoveAndAddIndent)
                    {
                        nodeStack.Push(currentNode); // Questo diventa il nuovo parent
                        indentLevel++;
                    }

                    offset += 36;
                    instructionNumber++;
                }

                if (instructionNumber == 1)
                {
                    decodedBuilder.AppendLine("Nessuna istruzione trovata all'offset specificato");
                    instructionBuilder.AppendLine($"// Nessuna istruzione trovata all'offset 0x{StartOffset:X}");

                    var emptyNode = new InstructionNode
                    {
                        Name = "Nessuna istruzione trovata",
                        Details = $"Offset: 0x{StartOffset:X}",
                        IsExpanded = true
                    };
                    rootNode.Children.Add(emptyNode);
                }
                else
                {
                    decodedBuilder.AppendLine();
                    decodedBuilder.AppendLine($"Processate {instructionNumber - 1} istruzioni.");
                    instructionBuilder.AppendLine($"// Totale istruzioni: {instructionNumber - 1}");

                    rootNode.Details = $"File binario - {instructionNumber - 1} istruzioni";
                }

                DecodedData = decodedBuilder.ToString();
                InstructionView = instructionBuilder.ToString();

                // Espandi tutti i nodi della TreeView
                ExpandAllNodes(InstructionTree);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Errore nel decoder: {ex.Message}\n{ex.StackTrace}";
                DecodedData = errorMsg;
                InstructionView = errorMsg;
                InstructionTree.Clear();
                Console.WriteLine($"ERRORE: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Espande ricorsivamente tutti i nodi della TreeView
        /// </summary>
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
    }
}