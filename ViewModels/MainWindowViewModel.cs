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

        private Dictionary<int, OpCodeInfo> _opCodeInfos = new Dictionary<int, OpCodeInfo>();

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

                // ASCII rappresentation
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
                return;
            }

            try
            {
                // Carica le definizioni degli OpCode se non già fatto
                if (_opCodeInfos.Count == 0)
                {
                    await LoadOpCodeDefinitions();
                }

                // Genera vista tecnica (decoder)
                var decodedBuilder = new StringBuilder();
                decodedBuilder.AppendLine("DECODER CODICE PROGRAMMA");
                decodedBuilder.AppendLine("=" + new string('=', 80));

                // Analizza header
                if (_currentFileBytes.Length >= 8)
                {
                    var headerText = Encoding.ASCII.GetString(_currentFileBytes, 0, 8);
                    decodedBuilder.AppendLine($"Header: '{headerText.Trim()}'");
                    decodedBuilder.AppendLine($"Start Offset: 0x{StartOffset:X8} ({StartOffset})");
                    decodedBuilder.AppendLine();
                }

                decodedBuilder.AppendLine("ISTRUZIONI DECODIFICATE (Vista tecnica):");
                decodedBuilder.AppendLine("-" + new string('-', 80));
                decodedBuilder.AppendLine("Num | Offset | OpCode/Nome         | Parametri");
                decodedBuilder.AppendLine("-" + new string('-', 80));

                // Genera vista istruzioni semplificata - SOLO NOMI OPCODE
                var instructionBuilder = new StringBuilder();
                instructionBuilder.AppendLine("// PROGRAMMA DECODIFICATO");
                instructionBuilder.AppendLine("// " + new string('=', 60));

                if (_currentFileBytes.Length >= 8)
                {
                    var headerText = Encoding.ASCII.GetString(_currentFileBytes, 0, 8);
                    instructionBuilder.AppendLine($"// File: {headerText.Trim()}");
                    instructionBuilder.AppendLine();
                }

                // Usa StartOffset corretto (0x32 = 50)
                int offset = StartOffset;
                int instructionNumber = 1;
                int maxInstructions = RecordCount;
                int indentLevel = 0;

                Console.WriteLine($"DEBUG: Inizio decodifica da offset 0x{offset:X} ({offset})");

                while (offset + 36 <= _currentFileBytes.Length && instructionNumber <= maxInstructions)
                {
                    // Leggi OpCode (primo intero)
                    var opCodeBytes = new byte[4];
                    Array.Copy(_currentFileBytes, offset, opCodeBytes, 0, 4);
                    int opCode = BitConverter.ToInt32(opCodeBytes, 0);

                    Console.WriteLine($"DEBUG: Offset 0x{offset:X}, OpCode letto: {opCode}");

                    if (opCode == 0)
                    {
                        Console.WriteLine("DEBUG: OpCode 0 trovato, salto...");
                        offset += 36; // Salta blocchi vuoti
                        continue;
                    }

                    // Trova informazioni sull'OpCode
                    string opName = _opCodeInfos.ContainsKey(opCode) ? _opCodeInfos[opCode].Name : $"UNKNOWN_{opCode}";
                    int expectedParams = _opCodeInfos.ContainsKey(opCode) ? _opCodeInfos[opCode].ParamCount : 8;

                    Console.WriteLine($"DEBUG: OpCode {opCode} -> {opName}");

                    // Vista tecnica (decoder)
                    decodedBuilder.Append($"{instructionNumber,3:D} | 0x{offset:X6} | ");
                    decodedBuilder.Append($"{opCode,3} {opName,-15} ");

                    // Leggi i parametri (8 interi dopo l'OpCode)
                    var parameters = new List<int>();
                    for (int i = 1; i <= 8 && offset + (i * 4) + 3 < _currentFileBytes.Length; i++)
                    {
                        var paramBytes = new byte[4];
                        Array.Copy(_currentFileBytes, offset + (i * 4), paramBytes, 0, 4);
                        parameters.Add(BitConverter.ToInt32(paramBytes, 0));
                    }

                    // Vista tecnica - parametri
                    var significantParams = parameters.Take(expectedParams).ToArray();
                    if (significantParams.Length > 0)
                    {
                        decodedBuilder.Append($"| {string.Join(", ", significantParams.Select(p => p.ToString().PadLeft(8)))}");
                    }
                    else if (expectedParams == 0)
                    {
                        decodedBuilder.Append("| (nessun parametro)");
                    }
                    decodedBuilder.AppendLine();

                    // *** LOGICA DI INDENTAZIONE CORRETTA ***
                    // Diminuisci indentazione PRIMA di scrivere queste istruzioni
                    if (opName == "END IF" || opName == "ENDCALL")
                    {
                        indentLevel = Math.Max(0, indentLevel - 1);
                    }
                    else if (opName == "ELSE")
                    {
                        // ELSE rimane allo stesso livello del IF corrispondente
                        // Ma se è un ELSE annidato, mantiene il livello attuale
                        if (indentLevel > 0)
                        {
                            indentLevel = Math.Max(0, indentLevel - 1);
                        }
                    }

                    // Calcola spazi basato sul pattern osservato
                    string indent;
                    switch (indentLevel)
                    {
                        case 0:
                            indent = "";        // 0 spazi
                            break;
                        case 1:
                            indent = "     ";   // 5 spazi
                            break;
                        case 2:
                            indent = "        "; // 8 spazi
                            break;
                        default:
                            indent = new string(' ', 5 + (indentLevel - 1) * 3); // Pattern: 5, 8, 11, 14...
                            break;
                    }

                    instructionBuilder.AppendLine($"{indent}{opName}");

                    // Aumenta indentazione DOPO aver scritto queste istruzioni
                    if (opName == "IF" || opName == "IF_MEM" || opName == "IF_NUM" ||
                        opName == "CALL" || opName == "LABEL")
                    {
                        indentLevel++;
                    }
                    else if (opName == "ELSE")
                    {
                        indentLevel++; // ELSE apre un nuovo blocco
                    }

                    offset += 36; // Ogni istruzione è di 36 byte (1 opcode + 8 parametri)
                    instructionNumber++;
                }

                if (instructionNumber == 1)
                {
                    decodedBuilder.AppendLine("Nessuna istruzione trovata all'offset specificato");
                    instructionBuilder.AppendLine($"// Nessuna istruzione trovata all'offset 0x{StartOffset:X}");
                    Console.WriteLine("DEBUG: Nessuna istruzione trovata!");
                }
                else
                {
                    decodedBuilder.AppendLine();
                    decodedBuilder.AppendLine($"Processate {instructionNumber - 1} istruzioni.");
                    decodedBuilder.AppendLine($"OpCodes caricati: {_opCodeInfos.Count}");

                    instructionBuilder.AppendLine();
                    instructionBuilder.AppendLine($"// Totale istruzioni: {instructionNumber - 1}");
                    Console.WriteLine($"DEBUG: Processate {instructionNumber - 1} istruzioni");
                }

                DecodedData = decodedBuilder.ToString();
                InstructionView = instructionBuilder.ToString();
            }
            catch (Exception ex)
            {
                var errorMsg = $"Errore nel decoder: {ex.Message}\n{ex.StackTrace}";
                DecodedData = errorMsg;
                InstructionView = errorMsg;
                Console.WriteLine($"ERRORE: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        private async Task LoadOpCodeDefinitions()
        {
            try
            {
                // Cerca il file INFO.CFG nelle cartelle standard
                var infoCfgPaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "INFO.CFG"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "INFO.CFG"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Data", "INFO.CFG"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Resources", "INFO.CFG"),
                    Path.Combine(Directory.GetCurrentDirectory(), "INFO.CFG"),
                };

                string? infoCfgPath = null;
                foreach (var path in infoCfgPaths)
                {
                    if (File.Exists(path))
                    {
                        infoCfgPath = path;
                        Console.WriteLine($"Trovato INFO.CFG in: {path}");
                        break;
                    }
                }

                if (infoCfgPath != null)
                {
                    var lines = await File.ReadAllLinesAsync(infoCfgPath);

                    foreach (var line in lines)
                    {
                        if (line.StartsWith(",") && line.Contains(","))
                        {
                            var parts = line.Split(',');
                            if (parts.Length >= 3)
                            {
                                var name = parts[1].Trim();
                                if (int.TryParse(parts[2].Trim(), out int opCode))
                                {
                                    var paramCount = parts.Length >= 4 && int.TryParse(parts[3].Trim(), out int pc) ? pc : 0;

                                    _opCodeInfos[opCode] = new OpCodeInfo
                                    {
                                        Name = name,
                                        ParamCount = paramCount
                                    };
                                }
                            }
                        }
                    }

                    Console.WriteLine($"Caricati {_opCodeInfos.Count} OpCodes da {infoCfgPath}");
                }
                else
                {
                    Console.WriteLine("INFO.CFG non trovato, usando definizioni di fallback");
                    LoadFallbackOpCodes();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento INFO.CFG: {ex.Message}");
                LoadFallbackOpCodes();
            }

            await Task.CompletedTask;
        }

        private void LoadFallbackOpCodes()
        {
            _opCodeInfos = new Dictionary<int, OpCodeInfo>
            {
                [0] = new OpCodeInfo { Name = "FINE", ParamCount = 0 },
                [1] = new OpCodeInfo { Name = "NULLA", ParamCount = 0 },
                [3] = new OpCodeInfo { Name = "AMUOVI", ParamCount = 3 },
                [4] = new OpCodeInfo { Name = "RMUOVI", ParamCount = 3 },
                [5] = new OpCodeInfo { Name = "INIZ MEM", ParamCount = 2 },
                [7] = new OpCodeInfo { Name = "LOGICA", ParamCount = 8 },
                [9] = new OpCodeInfo { Name = "AMUOVI MEM", ParamCount = 3 },
                [10] = new OpCodeInfo { Name = "VELOCITA", ParamCount = 2 },
                [12] = new OpCodeInfo { Name = "AZZERA", ParamCount = 1 },
                [13] = new OpCodeInfo { Name = "IF", ParamCount = 1 },
                [14] = new OpCodeInfo { Name = "ELSE", ParamCount = 0 },
                [15] = new OpCodeInfo { Name = "END IF", ParamCount = 0 },
                [16] = new OpCodeInfo { Name = "FORZA", ParamCount = 2 },
                [17] = new OpCodeInfo { Name = "ASPETTA", ParamCount = 4 },
                [19] = new OpCodeInfo { Name = "PIU", ParamCount = 0 },
                [20] = new OpCodeInfo { Name = "PAUSA", ParamCount = 1 },
                [21] = new OpCodeInfo { Name = "ACCEL", ParamCount = 2 },
                [36] = new OpCodeInfo { Name = "AZIONA", ParamCount = 6 },
                [37] = new OpCodeInfo { Name = "LABEL", ParamCount = 1 },
                [38] = new OpCodeInfo { Name = "CALL", ParamCount = 3 },
                [39] = new OpCodeInfo { Name = "ENDCALL", ParamCount = 0 },
                [40] = new OpCodeInfo { Name = "STOP", ParamCount = 0 },
                [49] = new OpCodeInfo { Name = "IMPULSO", ParamCount = 2 },
                [54] = new OpCodeInfo { Name = "SET PRESA", ParamCount = 8 },
                [57] = new OpCodeInfo { Name = "RIL. PEZZO", ParamCount = 0 },
                [92] = new OpCodeInfo { Name = "WR_EVENT", ParamCount = 8 },
                [94] = new OpCodeInfo { Name = "G_MISSIONE", ParamCount = 0 },
                [95] = new OpCodeInfo { Name = "JOB", ParamCount = 1 },
                [96] = new OpCodeInfo { Name = "ERRORE", ParamCount = 1 },
                [97] = new OpCodeInfo { Name = "PROCESSO", ParamCount = 2 },
                [104] = new OpCodeInfo { Name = "IF_MEM", ParamCount = 4 },
                [105] = new OpCodeInfo { Name = "IF_NUM", ParamCount = 3 },
                [106] = new OpCodeInfo { Name = "SET TERNA", ParamCount = 0 },
                [107] = new OpCodeInfo { Name = "OK PRESA", ParamCount = 0 },
                [113] = new OpCodeInfo { Name = "CLIENT", ParamCount = 1 },
                [114] = new OpCodeInfo { Name = "ATM", ParamCount = 0 },
                [115] = new OpCodeInfo { Name = "MAGAZZINO", ParamCount = 0 },
                [116] = new OpCodeInfo { Name = "VERIF NCP", ParamCount = 0 },
                [117] = new OpCodeInfo { Name = "COPIA ZONA", ParamCount = 2 },
                [119] = new OpCodeInfo { Name = "GET EDGE", ParamCount = 7 },
                [120] = new OpCodeInfo { Name = "CHECKRUN ASPS", ParamCount = 0 },
                [121] = new OpCodeInfo { Name = "RESET MAGAZ", ParamCount = 0 },
                [122] = new OpCodeInfo { Name = "CASS ASPS", ParamCount = 0 }
            };
        }
    }

    // Classe helper per le informazioni degli OpCode
    public class OpCodeInfo
    {
        public string Name { get; set; } = "";
        public int ParamCount { get; set; }
        public List<string> ParamTypes { get; set; } = new List<string>();
    }
}