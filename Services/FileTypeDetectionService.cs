using System;
using System.IO;
using System.Linq;
using System.Text;
using FileViewerApp.Enums;
using FileViewerApp.Models;

namespace FileViewerApp.Services
{
    /// <summary>
    /// Servizio per rilevare automaticamente il tipo di file
    /// </summary>
    public class FileTypeDetectionService
    {
        /// <summary>
        /// Rileva il tipo di file basandosi su estensione e contenuto
        /// </summary>
        public FileType DetectFileType(string filePath, byte[] content)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            var fileName = Path.GetFileName(filePath).ToUpper();

            // 1. Controllo per file di definizione OpCode
            if (IsOpCodeDefinitionFile(fileName, extension, content))
            {
                return FileType.OpCodeDefinition;
            }

            // 2. Controllo per file di testo con istruzioni
            if (IsTextProgramFile(extension, content))
            {
                return FileType.TextProgram;
            }

            // 3. Controllo per file binario programma
            if (IsBinaryProgramFile(extension, content))
            {
                return FileType.BinaryProgram;
            }

            return FileType.Unknown;
        }

        private bool IsOpCodeDefinitionFile(string fileName, string extension, byte[] content)
        {
            // Controlla nome file
            if (fileName.Contains("INFO"))
            {
                return true;
            }

            // Controlla estensione
            if (extension == ".xml" || extension == ".cfg")
            {
                // Verifica contenuto per XML
                if (extension == ".xml")
                {
                    var text = Encoding.UTF8.GetString(content.Take(1000).ToArray());
                    return text.Contains("<CMD") || text.Contains("<Root");
                }

                // Verifica contenuto per CFG
                if (extension == ".cfg")
                {
                    var text = Encoding.ASCII.GetString(content.Take(1000).ToArray());
                    return text.Contains(",") && (text.Contains("IF") || text.Contains("MEM"));
                }
            }

            return false;
        }

        private bool IsTextProgramFile(string extension, byte[] content)
        {
            if (extension != ".txt")
                return false;

            try
            {
                var text = Encoding.UTF8.GetString(content.Take(2000).ToArray());
                var lines = text.Split('\n').Take(10);

                // Cerca pattern tipici dei file di istruzioni
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//"))
                        continue;

                    // Pattern: "numero , COMANDO, parametri"
                    var parts = trimmed.Split(',');
                    if (parts.Length >= 2)
                    {
                        var firstPart = parts[0].Trim();
                        var secondPart = parts[1].Trim().ToUpper();

                        // Primo campo deve essere un numero
                        if (int.TryParse(firstPart, out _))
                        {
                            // Secondo campo deve essere un comando noto
                            if (IsKnownCommand(secondPart))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Se non riesce a leggere come testo, non è un file di testo
                return false;
            }

            return false;
        }

        private bool IsBinaryProgramFile(string extension, byte[] content)
        {
            // Controlla estensioni tipiche
            if (extension == ".dat" || extension == ".bin")
            {
                return true;
            }

            // Se il file è sufficientemente grande e contiene dati binari strutturati
            if (content.Length >= 50)
            {
                // I primi 8 bytes dovrebbero essere l'header (ASCII + spazi)
                var header = content.Take(8).ToArray();

                // Controlla se l'header contiene caratteri ASCII stampabili
                bool hasValidHeader = header.All(b => b >= 32 && b <= 126 || b == 0);

                if (hasValidHeader)
                {
                    // Controlla se ci sono pattern di istruzioni all'offset 0x32 (50)
                    if (content.Length > 50 + 36) // Almeno una istruzione completa
                    {
                        // Leggi il primo OpCode all'offset 50
                        var opCodeBytes = content.Skip(50).Take(4).ToArray();
                        if (BitConverter.IsLittleEndian)
                        {
                            var opCode = BitConverter.ToInt32(opCodeBytes, 0);
                            // OpCodes validi sono tipicamente tra 0 e 200
                            return opCode >= 0 && opCode <= 200;
                        }
                    }
                }
            }

            return false;
        }

        private bool IsKnownCommand(string command)
        {
            var knownCommands = new[]
            {
                "IF", "IF_NUM", "IF_MEM", "ELSE", "END IF", "ENDIF",
                "CALL", "ENDCALL", "LABEL", "FINE", "NULLA",
                "INIZ MEM", "INIZIALIZZA", "MUOVI", "AMUOVI", "RMUOVI",
                "VELOCITA", "ACCEL", "FORZA", "AZIONA", "IMPULSO",
                "ASPETTA", "PAUSA", "AZZERA", "LOGICA", "PIU",
                "SET PRESA", "OK PRESA", "RIL. PEZZO", "STOP",
                "ERRORE", "PROCESSO", "JOB", "ATM", "MAGAZZINO"
            };

            return knownCommands.Contains(command);
        }
    }
}