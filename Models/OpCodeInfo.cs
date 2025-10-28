using System.Collections.Generic;

namespace FileViewerApp.Models
{
    /// <summary>
    /// Modalità di indentazione per l'OpCode
    /// </summary>
    public enum IndentMode
    {
        None,                    // NONE
        AddIndent,              // ADD_INDENT
        RemoveIndent,           // REMOVE_INDENT
        RemoveAndAddIndent      // REMOVE_AND_ADD_INDENT
    }

    /// <summary>
    /// Rappresenta le informazioni di un OpCode del programma
    /// </summary>
    public class OpCodeInfo
    {
        /// <summary>
        /// Nome dell'operazione (es: "IF_NUM", "INIZ MEM", etc.)
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// ID/OpCode numerico
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Numero di parametri significativi per questo OpCode
        /// </summary>
        public int ParamCount { get; set; }

        /// <summary>
        /// Modalità di indentazione
        /// </summary>
        public IndentMode IndentMode { get; set; } = IndentMode.None;

        /// <summary>
        /// Tipi dei parametri (per future estensioni)
        /// </summary>
        public List<string> ParamTypes { get; set; } = new List<string>();

        /// <summary>
        /// Descrizione dell'OpCode
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Categoria dell'OpCode (es: "Control Flow", "Memory", "IO")
        /// </summary>
        public string Category { get; set; } = "";

        /// <summary>
        /// Versione del comando
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Se il comando è attivo
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Nome dell'immagine associata
        /// </summary>
        public string ImageName { get; set; } = "";

        /// <summary>
        /// Lista dei parametri con dettagli
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
    }

    /// <summary>
    /// Informazioni dettagliate su un parametro
    /// </summary>
    public class ParameterInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double DefaultValue { get; set; }
        public List<string> ResourceGroups { get; set; } = new List<string>();
    }
}