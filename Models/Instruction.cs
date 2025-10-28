using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileViewerApp.Models
{
    namespace FileViewerApp.Models
    {
        /// <summary>
        /// Rappresenta una singola istruzione del programma
        /// </summary>
        public class Instruction
        {
            /// <summary>
            /// Numero progressivo dell'istruzione
            /// </summary>
            public int Number { get; set; }

            /// <summary>
            /// OpCode numerico dell'istruzione
            /// </summary>
            public int OpCode { get; set; }

            /// <summary>
            /// Nome dell'istruzione (es: "IF_NUM", "INIZ MEM")
            /// </summary>
            public string Name { get; set; } = "";

            /// <summary>
            /// Parametri dell'istruzione (massimo 8)
            /// </summary>
            public int[] Parameters { get; set; } = new int[8];

            /// <summary>
            /// Offset nel file binario
            /// </summary>
            public int Offset { get; set; }

            /// <summary>
            /// Livello di indentazione per la visualizzazione
            /// </summary>
            public int IndentLevel { get; set; }
        }
    }
}