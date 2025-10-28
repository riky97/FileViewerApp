using System;
using System.Collections.Generic;
using FileViewerApp.Enums;
using FileViewerApp.Models.FileViewerApp.Models;

namespace FileViewerApp.Models
{
    /// <summary>
    /// Rappresenta un file processato con tutte le informazioni estratte
    /// </summary>
    public class ProcessedFile
    {
        /// <summary>
        /// Percorso del file originale
        /// </summary>
        public string FilePath { get; set; } = "";

        /// <summary>
        /// Nome del file
        /// </summary>
        public string FileName { get; set; } = "";

        /// <summary>
        /// Tipo di file rilevato
        /// </summary>
        public FileType FileType { get; set; }

        /// <summary>
        /// Contenuto raw del file (bytes)
        /// </summary>
        public byte[] RawContent { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Header del programma (per file binari)
        /// </summary>
        public string Header { get; set; } = "";

        /// <summary>
        /// Lista delle istruzioni estratte
        /// </summary>
        public List<Instruction> Instructions { get; set; } = new();

        /// <summary>
        /// Vista hex del contenuto
        /// </summary>
        public string HexView { get; set; } = "";

        /// <summary>
        /// Vista testuale delle istruzioni
        /// </summary>
        public string TextualView { get; set; } = "";

        /// <summary>
        /// TreeView delle istruzioni (per UI)
        /// </summary>
        public List<InstructionNode> InstructionTree { get; set; } = new();

        /// <summary>
        /// Dimensione del file in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Informazioni di parsing (errori, warning, etc.)
        /// </summary>
        public List<string> Messages { get; set; } = new();
    }
}