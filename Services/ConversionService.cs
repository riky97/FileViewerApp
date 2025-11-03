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
    public class ConversionService
    {
        private readonly OpCodeService _opCodeService;
        public ConversionService(OpCodeService opCodeService) => _opCodeService = opCodeService;

        #region TEXT → BINARY
        public async Task<byte[]> ConvertTextToBinaryAsync(string textContent, string programName = "MAIN")
        {
            var instructions = ParseTextInstructions(textContent, out string headerName);
            return GenerateBinaryFromInstructions(instructions, headerName ?? programName);
        }
        public async Task<byte[]> ConvertTextFileAsync(string path) => await ConvertTextToBinaryAsync(await File.ReadAllTextAsync(path), Path.GetFileNameWithoutExtension(path));

        private List<Instruction> ParseTextInstructions(string textContent, out string headerName)
        {
            var list = new List<Instruction>();
            headerName = "MAIN";
            var lines = textContent.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l) && !l.Trim().StartsWith("//")).ToList();
            var headerLine = lines.FirstOrDefault(l => l.Contains(";") && (l.Contains("106") || l.Split(';').Length == 2));
            if (headerLine != null)
            {
                var parts = headerLine.Split(';');
                if (parts.Length >= 2) headerName = parts[1].Trim();
                lines.Remove(headerLine);
            }
            foreach (var line in lines)
                if (TryParseTextInstruction(line.Trim(), out var instr)) list.Add(instr);
            return list;
        }
        private bool TryParseTextInstruction(string line, out Instruction instruction)
        {
            instruction = null;
            try
            {
                line = line.TrimEnd(';', ' ', '\t');
                var parts = line.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                if (parts.Length < 2) return false;
                if (!int.TryParse(parts[0], out int num)) return false;
                var name = parts[1];
                var info = _opCodeService.GetOpCodeByName(name);
                if (info == null) return false;
                var pars = new int[8];
                for (int i = 0; i < 8; i++)
                {
                    int idx = i + 2;
                    pars[i] = (idx < parts.Length && int.TryParse(parts[idx], out int val)) ? val : 0;
                }
                instruction = new Instruction { Number = num, OpCode = info.Id, Name = info.Name, Parameters = pars };
                return true;
            }
            catch { return false; }
        }
        private byte[] GenerateBinaryFromInstructions(List<Instruction> instructions, string programName)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            // Header 8 bytes (keep legacy style beginning with space)
            var header = $" 106{programName}".PadRight(8).Substring(0, 8);
            bw.Write(Encoding.ASCII.GetBytes(header));
            // Padding to reach offset 0x32 (50 dec) => 42 bytes
            bw.Write(new byte[42]);
            // Each instruction: 4 bytes opcode + 8 * 4 bytes params = 36 bytes
            foreach (var instr in instructions.OrderBy(i => i.Number))
            {
                bw.Write(instr.OpCode);
                var pars = instr.Parameters ?? Array.Empty<int>();
                for (int i = 0; i < 8; i++) bw.Write(i < pars.Length ? pars[i] : 0);
            }
            return ms.ToArray();
        }
        #endregion

        #region BINARY → TEXT
        public async Task<string> ConvertBinaryToTextAsync(byte[] binary) => GenerateTextFromInstructions(ExtractInstructionsFromBinary(binary, out string header), header);
        public async Task<string> ConvertBinaryFileAsync(string path) => await ConvertBinaryToTextAsync(await File.ReadAllBytesAsync(path));

        public List<Instruction> ExtractInstructionsFromBinary(byte[] data, out string header)
        {
            var list = new List<Instruction>();
            header = string.Empty;
            if (data.Length < 50) return list;
            header = Encoding.ASCII.GetString(data, 0, 8).Trim();
            int offset = 0x32; // 50
            int number = 1;
            bool fineEncountered = false;
            while (offset + 36 <= data.Length && !fineEncountered)
            {
                int opCode = BitConverter.ToInt32(data, offset);
                // Detect FINE (ID 0) as terminator
                var info = _opCodeService.GetOpCodeInfo(opCode);
                if (opCode == 0 && info != null && string.Equals(info.Name, "FINE", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(new Instruction
                    {
                        Number = number,
                        OpCode = opCode,
                        Name = info.Name,
                        Parameters = new int[8],
                        Offset = offset
                    });
                    fineEncountered = true; // stop after FINE
                    break;
                }
                // Skip padding zeros that are not FINE definitions
                if (opCode == 0)
                {
                    offset += 36;
                    continue;
                }
                var parameters = new int[8];
                for (int i = 0; i < 8; i++) parameters[i] = BitConverter.ToInt32(data, offset + 4 + (i * 4));
                string name = info?.Name ?? $"UNKNOWN_{opCode}";
                list.Add(new Instruction
                {
                    Number = number,
                    OpCode = opCode,
                    Name = name,
                    Parameters = parameters,
                    Offset = offset
                });
                offset += 36;
                number++;
            }
            return list;
        }

        private string GenerateTextFromInstructions(List<Instruction> instructions, string header)
        {
            var sb = new StringBuilder();
            string programName = "MAIN";
            if (!string.IsNullOrWhiteSpace(header))
            {
                var letters = new string(header.Where(c => !char.IsDigit(c)).ToArray()).Trim();
                if (!string.IsNullOrEmpty(letters)) programName = letters;
            }
            sb.AppendLine($"{instructions.Count};{programName}");
            int indentLevel = 0;
            foreach (var instr in instructions)
            {
                var info = _opCodeService.GetOpCodeInfo(instr.OpCode);
                var indentMode = info?.IndentMode ?? IndentMode.None;
                if (indentMode == IndentMode.RemoveIndent || indentMode == IndentMode.RemoveAndAddIndent)
                    indentLevel = Math.Max(0, indentLevel - 1);
                int paramCount = info?.ParamCount ?? 8;
                var significant = instr.Parameters.Take(paramCount).ToArray();
                sb.Append($"{instr.Number,4} , ");
                string indentedName = new string(' ', indentLevel * 2) + instr.Name;
                if (indentedName.Length > 12) indentedName = indentedName.Substring(0, 12);
                sb.Append(indentedName.PadRight(12));
                foreach (var p in significant) sb.Append("," + p.ToString().PadLeft(11));
                sb.Append("  ;\n");
                if (indentMode == IndentMode.AddIndent || indentMode == IndentMode.RemoveAndAddIndent)
                    indentLevel++;
            }
            return sb.ToString();
        }
        #endregion

        #region SAVE HELPERS
        public async Task SaveBinaryFileAsync(byte[] data, string path) => await File.WriteAllBytesAsync(path, data);
        public async Task SaveTextFileAsync(string text, string path) => await File.WriteAllTextAsync(path, text, Encoding.UTF8);
        #endregion
    }
}