using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileViewerApp.Enums;
using FileViewerApp.Models;
using FileViewerApp.Models.FileViewerApp.Models;

namespace FileViewerApp.Services
{
    /// <summary>
    /// Orchestrator semplificato che coordina processing e conversioni
    /// </summary>
    public class FileProcessorOrchestrator
    {
        private readonly OpCodeService _opCodeService;
        private readonly ConversionService _conversionService;
        private readonly FileTypeDetectionService _detectionService;

        // Cache semplice
        private readonly Dictionary<string, ProcessedFile> _fileCache = new();

        public FileProcessorOrchestrator(OpCodeService opCodeService)
        {
            _opCodeService = opCodeService;
            _conversionService = new ConversionService(opCodeService);
            _detectionService = new FileTypeDetectionService();
        }

        #region FILE PROCESSING

        /// <summary>
        /// Processa un file da percorso
        /// </summary>
        public async Task<ProcessedFile> ProcessFileAsync(string filePath, bool useCache = true)
        {
            // Controlla cache
            if (useCache && _fileCache.TryGetValue(filePath, out var cachedFile))
            {
                return cachedFile;
            }

            var content = await File.ReadAllBytesAsync(filePath);
            var processedFile = await ProcessFileAsync(filePath, content);

            // Salva in cache
            if (useCache)
            {
                _fileCache[filePath] = processedFile;
            }

            return processedFile;
        }

        /// <summary>
        /// Processa un file da contenuto in memoria
        /// </summary>
        public async Task<ProcessedFile> ProcessFileAsync(string filePath, byte[] content)
        {
            var fileType = _detectionService.DetectFileType(filePath, content);

            var processedFile = new ProcessedFile
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                FileType = fileType,
                RawContent = content,
                FileSize = content.Length
            };

            try
            {
                // Carica OpCode definitions
                await _opCodeService.LoadOpCodeDefinitionsAsync();

                switch (fileType)
                {
                    case FileType.BinaryProgram:
                        await ProcessBinaryFile(processedFile);
                        break;

                    case FileType.TextProgram:
                        await ProcessTextFile(processedFile);
                        break;

                    case FileType.OpCodeDefinition:
                        await ProcessOpCodeDefinitionFile(processedFile);
                        break;

                    default:
                        processedFile.Messages.Add("Tipo file non supportato");
                        break;
                }
            }
            catch (Exception ex)
            {
                processedFile.Messages.Add($"Errore processing: {ex.Message}");
                Console.WriteLine($"Errore processing {filePath}: {ex.Message}");
            }

            return processedFile;
        }

        private async Task ProcessBinaryFile(ProcessedFile file)
        {
            // Extract header
            if (file.RawContent.Length >= 8)
            {
                file.Header = Encoding.ASCII.GetString(file.RawContent, 0, 8).Trim();
            }

            // Extract instructions usando ConversionService
            file.Instructions = _conversionService.ExtractInstructionsFromBinary(file.RawContent, out _);

            // Generate views
            file.HexView = GenerateHexView(file.RawContent);
            file.TextualView = GenerateInstructionTextView(file.Instructions, file.Header);
            file.InstructionTree = GenerateInstructionTree(file.Instructions, file.Header);

            file.Messages.Add($"File binario processato: {file.Instructions.Count} istruzioni");
        }

        private async Task ProcessTextFile(ProcessedFile file)
        {
            var textContent = Encoding.UTF8.GetString(file.RawContent);

            // Extract header from text
            var lines = textContent.Split('\n');
            var headerLine = lines.FirstOrDefault(l => l.Contains(";"));
            if (headerLine != null)
            {
                var parts = headerLine.Split(';');
                if (parts.Length >= 2)
                {
                    file.Header = parts[1].Trim();
                }
            }

            // Extract instructions
            file.Instructions = _conversionService.ExtractInstructionsFromBinary(
                await _conversionService.ConvertTextToBinaryAsync(textContent, file.Header), out _);

            // Generate binary equivalent for hex view
            var binaryData = await _conversionService.ConvertTextToBinaryAsync(textContent, file.Header);

            file.HexView = GenerateHexView(binaryData);
            file.TextualView = GenerateInstructionTextView(file.Instructions, file.Header);
            file.InstructionTree = GenerateInstructionTree(file.Instructions, file.Header);

            file.Messages.Add($"File testo processato: {file.Instructions.Count} istruzioni");
        }

        private async Task ProcessOpCodeDefinitionFile(ProcessedFile file)
        {
            file.Messages.Add("File di definizione OpCode rilevato");

            // Potrebbe essere usato per aggiornare le definizioni OpCode
            // Ma per ora lo trattiamo solo come file informativo

            try
            {
                var textContent = Encoding.UTF8.GetString(file.RawContent);
                file.TextualView = textContent;
                file.HexView = GenerateHexView(file.RawContent);
            }
            catch
            {
                file.TextualView = "File binario - vedere vista Hex";
                file.HexView = GenerateHexView(file.RawContent);
            }
        }

        #endregion

        #region CONVERSION METHODS

        /// <summary>
        /// Converte un file da un formato all'altro
        /// </summary>
        public async Task<byte[]> ConvertFileAsync(string inputPath, string outputPath)
        {
            var sourceFile = await ProcessFileAsync(inputPath);
            var targetType = DetermineFileTypeFromExtension(outputPath);
            return await ConvertAsync(sourceFile, targetType, outputPath);
        }

        /// <summary>
        /// Converte tra formati specifici
        /// </summary>
        public async Task<byte[]> ConvertAsync(ProcessedFile sourceFile, FileType targetType, string outputPath)
        {
            byte[] result;

            if (targetType == FileType.BinaryProgram)
            {
                // Convert to binary
                if (sourceFile.FileType == FileType.TextProgram)
                {
                    var textContent = Encoding.UTF8.GetString(sourceFile.RawContent);
                    result = await _conversionService.ConvertTextToBinaryAsync(textContent, sourceFile.Header);
                }
                else
                {
                    // Se è già binario, usa il contenuto originale
                    result = sourceFile.RawContent;
                }
            }
            else if (targetType == FileType.TextProgram)
            {
                // Convert to text
                if (sourceFile.FileType == FileType.BinaryProgram)
                {
                    var textContent = await _conversionService.ConvertBinaryToTextAsync(sourceFile.RawContent);
                    result = Encoding.UTF8.GetBytes(textContent);
                }
                else
                {
                    // Se è già testo, usa il contenuto originale
                    result = sourceFile.RawContent;
                }
            }
            else
            {
                throw new NotSupportedException($"Conversione a {targetType} non supportata");
            }

            await File.WriteAllBytesAsync(outputPath, result);
            return result;
        }

        /// <summary>
        /// Salva un file processato nel formato specificato
        /// </summary>
        public async Task<byte[]> SaveFileAsync(ProcessedFile file, string outputPath)
        {
            var targetType = DetermineFileTypeFromExtension(outputPath);
            return await ConvertAsync(file, targetType, outputPath);
        }

        #endregion

        #region VIEW GENERATION HELPERS

        private string GenerateHexView(byte[] content)
        {
            var hexBuilder = new StringBuilder();
            hexBuilder.AppendLine("OFFSET    00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F  |ASCII          |");
            hexBuilder.AppendLine(new string('-', 79));

            for (int i = 0; i < content.Length; i += 16)
            {
                hexBuilder.Append($"{i:X8}  ");

                // Hex bytes
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < content.Length)
                    {
                        hexBuilder.Append($"{content[i + j]:X2} ");
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
                for (int j = 0; j < 16 && i + j < content.Length; j++)
                {
                    byte b = content[i + j];
                    hexBuilder.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                hexBuilder.AppendLine("|");
            }

            return hexBuilder.ToString();
        }

        private string GenerateInstructionTextView(List<Instruction> instructions, string header)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"// PROGRAMMA DECODIFICATO: {header}");
            builder.AppendLine("// " + new string('=', 60));
            builder.AppendLine();

            int indentLevel = 0;

            foreach (var instruction in instructions)
            {
                var opCodeInfo = _opCodeService.GetOpCodeInfo(instruction.OpCode);
                var indentMode = opCodeInfo?.IndentMode ?? IndentMode.None;

                // Gestisci chiusura blocchi
                if (indentMode == IndentMode.RemoveIndent || indentMode == IndentMode.RemoveAndAddIndent)
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                }

                string indent = new string(' ', indentLevel * 4);

                // Mostra parametri significativi
                var paramCount = opCodeInfo?.ParamCount ?? 0;
                var significantParams = instruction.Parameters.Take(paramCount).ToArray();
                var paramString = significantParams.Length > 0
                    ? " (" + string.Join(", ", significantParams) + ")"
                    : "";

                builder.AppendLine($"{indent}{instruction.Name}{paramString}");

                // Gestisci apertura blocchi
                if (indentMode == IndentMode.AddIndent || indentMode == IndentMode.RemoveAndAddIndent)
                {
                    indentLevel++;
                }
            }

            return builder.ToString();
        }

        private List<InstructionNode> GenerateInstructionTree(List<Instruction> instructions, string header)
        {
            var tree = new List<InstructionNode>();
            var nodeStack = new Stack<InstructionNode>();

            var rootNode = new InstructionNode
            {
                Name = $"PROGRAMMA: {header}",
                Details = $"File - {instructions.Count} istruzioni",
                IsExpanded = true
            };
            tree.Add(rootNode);
            nodeStack.Push(rootNode);

            foreach (var instruction in instructions)
            {
                var opCodeInfo = _opCodeService.GetOpCodeInfo(instruction.OpCode);
                var indentMode = opCodeInfo?.IndentMode ?? IndentMode.None;

                // Gestisci chiusura blocchi
                if (indentMode == IndentMode.RemoveIndent || indentMode == IndentMode.RemoveAndAddIndent)
                {
                    if (nodeStack.Count > 1)
                    {
                        nodeStack.Pop();
                    }
                }

                var paramCount = opCodeInfo?.ParamCount ?? 8;
                var significantParams = instruction.Parameters.Take(paramCount).ToArray();

                var currentNode = new InstructionNode
                {
                    Name = instruction.Name,
                    InstructionNumber = instruction.Number,
                    Offset = instruction.Offset,
                    OpCode = instruction.OpCode,
                    Parameters = string.Join(", ", significantParams),
                    Details = $"Offset: 0x{instruction.Offset:X6} | OpCode: {instruction.OpCode} | {opCodeInfo?.Category ?? "Unknown"}",
                    IsExpanded = true
                };

                var parentNode = nodeStack.Peek();
                parentNode.Children.Add(currentNode);

                // Gestisci apertura blocchi
                if (indentMode == IndentMode.AddIndent || indentMode == IndentMode.RemoveAndAddIndent)
                {
                    nodeStack.Push(currentNode);
                }
            }

            return tree;
        }

        #endregion

        #region UTILITY METHODS

        private FileType DetermineFileTypeFromExtension(string filePath)
        {
            return Path.GetExtension(filePath).ToLower() switch
            {
                ".dat" or ".bin" => FileType.BinaryProgram,
                ".txt" => FileType.TextProgram,
                ".xml" => FileType.OpCodeDefinition,
                ".cfg" => FileType.OpCodeDefinition,
                _ => throw new NotSupportedException($"Estensione non supportata: {Path.GetExtension(filePath)}")
            };
        }

        /// <summary>
        /// Pulisce la cache
        /// </summary>
        public void ClearCache()
        {
            _fileCache.Clear();
        }

        /// <summary>
        /// Verifica se un file può essere processato
        /// </summary>
        public bool CanProcessFile(string filePath, byte[] content)
        {
            var fileType = _detectionService.DetectFileType(filePath, content);
            return fileType != FileType.Unknown;
        }

        #endregion
    }
}