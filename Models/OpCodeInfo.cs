using System.Collections.Generic;

namespace FileViewerApp.Models
{
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
        /// Numero di parametri significativi per questo OpCode
        /// </summary>
        public int ParamCount { get; set; }

        /// <summary>
        /// Tipi dei parametri (per future estensioni)
        /// </summary>
        public List<string> ParamTypes { get; set; } = new List<string>();

        /// <summary>
        /// Descrizione dell'OpCode (opzionale)
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Categoria dell'OpCode (es: "Control Flow", "Memory", "IO")
        /// </summary>
        public string Category { get; set; } = "";
    }
}