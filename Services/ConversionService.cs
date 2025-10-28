using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileViewerApp.Models;
using FileViewerApp.Models.FileViewerApp.Models;

namespace FileViewerApp.Services
{
    /// <summary>
    /// Servizio unificato per tutte le conversioni TXT ↔ DAT
    /// </summary>
    public class ConversionService
    {
        private readonly OpCodeService _opCodeService;

        public ConversionService(OpCodeService opCodeService)
        {
            _opCodeService = opCodeService;
        }

        #region TEXT TO BINARY CONVERSION

        /// <summary>
        /// Converte contenuto testuale in file binario
        /// </summary>
        public async Task<byte[]> ConvertTextToBinaryAsync(string textContent, string programName = "MAIN")
        {
            var instructions = ParseTextInstructions(textContent, out string headerName);
            return GenerateBinaryFromInstructions(instructions, headerName ?? programName);
        }

        /// <summary>
        /// Converte file di testo in binario
        /// </summary>
        public async Task<byte[]> ConvertTextFileAsync(string textFilePath)
        {
            var textContent = await File.ReadAllTextAsync(textFilePath);
            var programName = Path.GetFileNameWithoutExtension(textFilePath);
            return await ConvertTextToBinaryAsync(textContent, programName);
        }

        // Migliora il metodo ParseTextInstructions per gestire meglio il formato del file 1.txt

        private List<Instruction> ParseTextInstructions(string textContent, out string headerName)
        {
            var instructions = new List<Instruction>();
            headerName = "MAIN"; // Default

            var lines = textContent.Split('\n')
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.Trim().StartsWith("//"))
                .ToList();

            // Cerca header nel formato "106;MAIN" o simili
            var headerLine = lines.FirstOrDefault(l => l.Contains(";") &&
                (l.Contains("106") || l.Split(';').Length == 2));

            if (headerLine != null)
            {
                var headerParts = headerLine.Split(';');
                if (headerParts.Length >= 2)
                {
                    headerName = headerParts[1].Trim();
                }
                lines.Remove(headerLine);
            }

            // Parse istruzioni con formato migliorato
            foreach (var line in lines)
            {
                if (TryParseTextInstruction(line.Trim(), out var instruction))
                {
                    instructions.Add(instruction);
                }
            }

            return instructions;
        }

        private bool TryParseTextInstruction(string line, out Instruction instruction)
        {
            instruction = null;

            try
            {
                // Rimuovi spazi extra e caratteri finali come ";"
                line = line.TrimEnd(';', ' ', '\t');

                // Parse format: "1 , IF_NUM      ,      565,        0,        0,        0"
                var parts = line.Split(',')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();

                if (parts.Length >= 2)
                {
                    // Primo campo: numero istruzione
                    if (!int.TryParse(parts[0], out int instructionNumber))
                        return false;

                    // Secondo campo: nome comando (rimuovi spazi extra)
                    var commandName = parts[1].Trim();
                    var opCodeInfo = _opCodeService.GetOpCodeByName(commandName);

                    if (opCodeInfo != null)
                    {
                        // Parametri (massimo 8, completa con zeri se mancanti)
                        var parameters = new int[8];

                        // Prendi tutti i parametri disponibili dopo il nome comando
                        for (int i = 0; i < 8; i++)
                        {
                            if (i + 2 < parts.Length && int.TryParse(parts[i + 2], out int param))
                            {
                                parameters[i] = param;
                            }
                            else
                            {
                                parameters[i] = 0;
                            }
                        }

                        instruction = new Instruction
                        {
                            Number = instructionNumber,
                            OpCode = opCodeInfo.Id,
                            Name = opCodeInfo.Name,
                            Parameters = parameters
                        };

                        Console.WriteLine($"Parsed: {instructionNumber} -> {commandName} (OpCode: {opCodeInfo.Id})");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"OpCode non trovato per comando: '{commandName}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore parsing linea '{line}': {ex.Message}");
            }

            return false;
        }

        private byte[] GenerateBinaryFromInstructions(List<Instruction> instructions, string programName)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // 1. Scrivi header (8 bytes)
            var headerText = $" 106{programName}".PadRight(8).Substring(0, 8);
            var headerBytes = Encoding.ASCII.GetBytes(headerText);
            writer.Write(headerBytes);

            // 2. Scrivi padding fino all'offset 0x32 (50)
            var paddingBytes = new byte[42];
            writer.Write(paddingBytes);

            // 3. Scrivi le istruzioni (36 bytes ciascuna)
            foreach (var instruction in instructions.OrderBy(i => i.Number))
            {
                // OpCode (4 bytes)
                writer.Write(instruction.OpCode);

                // 8 parametri (4 bytes ciascuno = 32 bytes)
                for (int i = 0; i < 8; i++)
                {
                    writer.Write(instruction.Parameters[i]);
                }
            }

            return stream.ToArray();
        }

        #endregion

        #region BINARY TO TEXT CONVERSION

        /// <summary>
        /// Converte file binario in formato testuale
        /// </summary>
        public async Task<string> ConvertBinaryToTextAsync(byte[] binaryData)
        {
            var instructions = ExtractInstructionsFromBinary(binaryData, out string header);
            return GenerateTextFromInstructions(instructions, header);
        }

        /// <summary>
        /// Converte file binario in formato testuale
        /// </summary>
        public async Task<string> ConvertBinaryFileAsync(string binaryFilePath)
        {
            var binaryData = await File.ReadAllBytesAsync(binaryFilePath);
            return await ConvertBinaryToTextAsync(binaryData);
        }

        public List<Instruction> ExtractInstructionsFromBinary(byte[] binaryData, out string header)
        {
            var instructions = new List<Instruction>();
            header = "";

            if (binaryData.Length < 50)
                return instructions;

            // Extract header
            if (binaryData.Length >= 8)
            {
                header = Encoding.ASCII.GetString(binaryData, 0, 8).Trim();
            }

            int offset = 0x32; // 50 decimale
            int instructionNumber = 1;

            while (offset + 36 <= binaryData.Length)
            {
                // Leggi OpCode
                var opCode = BitConverter.ToInt32(binaryData, offset);

                if (opCode == 0)
                {
                    offset += 36;
                    continue;
                }

                var opCodeInfo = _opCodeService.GetOpCodeInfo(opCode);
                if (opCodeInfo == null)
                {
                    offset += 36;
                    continue;
                }

                // Leggi parametri
                var parameters = new int[8];
                for (int i = 0; i < 8; i++)
                {
                    parameters[i] = BitConverter.ToInt32(binaryData, offset + 4 + (i * 4));
                }

                instructions.Add(new Instruction
                {
                    Number = instructionNumber,
                    OpCode = opCode,
                    Name = opCodeInfo.Name,
                    Parameters = parameters,
                    Offset = offset
                });

                offset += 36;
                instructionNumber++;
            }

            return instructions;
        }

        private string GenerateTextFromInstructions(List<Instruction> instructions, string header)
        {
            var builder = new StringBuilder();

            // Header
            if (!string.IsNullOrEmpty(header))
            {
                var parts = header.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    builder.AppendLine($"{parts[0]};{parts[1]}");
                }
            }

            // Istruzioni
            foreach (var instruction in instructions)
            {
                var opCodeInfo = _opCodeService.GetOpCodeInfo(instruction.OpCode);
                var paramCount = opCodeInfo?.ParamCount ?? 8;

                var line = $"{instruction.Number,4} , {instruction.Name,-12}";

                // Aggiungi solo i parametri significativi
                var significantParams = instruction.Parameters.Take(paramCount).ToArray();
                if (significantParams.Length > 0)
                {
                    line += ", " + string.Join(", ", significantParams.Select(p => $"{p,8}"));
                }

                builder.AppendLine(line + ";");
            }

            return builder.ToString();
        }

        #endregion

        #region FILE I/O HELPERS

        /// <summary>
        /// Salva dati binari su file
        /// </summary>
        public async Task SaveBinaryFileAsync(byte[] binaryData, string outputPath)
        {
            await File.WriteAllBytesAsync(outputPath, binaryData);
        }

        /// <summary>
        /// Salva contenuto testuale su file
        /// </summary>
        public async Task SaveTextFileAsync(string textContent, string outputPath)
        {
            await File.WriteAllTextAsync(outputPath, textContent, Encoding.UTF8);
        }

        #endregion
    }
}