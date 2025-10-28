using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FileViewerApp.Interfaces;
using FileViewerApp.Models;

namespace FileViewerApp.Services
{
    /// <summary>
    /// Loader per file INFO.CFG (formato attuale)
    /// </summary>
    public class CfgOpCodeLoader : IOpCodeLoader
    {
        public string LoaderName => "CFG File Loader";

        public bool CanHandle(string filePath)
        {
            return Path.GetExtension(filePath).ToLower() == ".cfg" ||
                   Path.GetFileName(filePath).ToUpper() == "INFO.CFG";
        }

        public async Task<Dictionary<int, OpCodeInfo>> LoadAsync(string filePath)
        {
            var opCodes = new Dictionary<int, OpCodeInfo>();
            var lines = await File.ReadAllLinesAsync(filePath);

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

                            opCodes[opCode] = new OpCodeInfo
                            {
                                Name = name,
                                ParamCount = paramCount,
                                Category = DetermineCategory(name),
                                Description = parts.Length >= 5 ? parts[4].Trim() : ""
                            };
                        }
                    }
                }
            }

            return opCodes;
        }

        private string DetermineCategory(string name)
        {
            return name.ToUpper() switch
            {
                var n when n.Contains("IF") || n.Contains("ELSE") || n.Contains("END") || n.Contains("CALL") || n.Contains("LABEL") => "Control Flow",
                var n when n.Contains("MEM") || n.Contains("AZZERA") || n.Contains("COPIA") => "Memory",
                var n when n.Contains("MUOVI") || n.Contains("VELOCITA") || n.Contains("ACCEL") => "Movement",
                var n when n.Contains("ASPETTA") || n.Contains("PAUSA") => "Timing",
                var n when n.Contains("FORZA") || n.Contains("AZIONA") || n.Contains("PRESA") || n.Contains("IMPULSO") => "IO",
                var n when n.Contains("ERRORE") => "Error Handling",
                var n when n.Contains("PROCESSO") || n.Contains("JOB") || n.Contains("ATM") || n.Contains("MAGAZZINO") => "System",
                _ => "General"
            };
        }
    }
}