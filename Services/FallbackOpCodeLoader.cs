using System.Collections.Generic;
using System.Threading.Tasks;
using FileViewerApp.Interfaces;
using FileViewerApp.Models;

namespace FileViewerApp.Services
{
    /// <summary>
    /// Loader di fallback con OpCodes hardcoded e IndentMode
    /// </summary>
    public class FallbackOpCodeLoader : IOpCodeLoader
    {
        public string LoaderName => "Fallback Loader";

        public bool CanHandle(string filePath)
        {
            return true; // Fallback gestisce sempre tutto
        }

        public Task<Dictionary<int, OpCodeInfo>> LoadAsync(string filePath)
        {
            var opCodes = new Dictionary<int, OpCodeInfo>
            {
                [0] = new OpCodeInfo { Name = "FINE", ParamCount = 0, IndentMode = IndentMode.None, Category = "Control Flow" },
                [1] = new OpCodeInfo { Name = "NULLA", ParamCount = 0, IndentMode = IndentMode.None, Category = "General" },
                [3] = new OpCodeInfo { Name = "AMUOVI", ParamCount = 3, IndentMode = IndentMode.None, Category = "Movement" },
                [4] = new OpCodeInfo { Name = "RMUOVI", ParamCount = 3, IndentMode = IndentMode.None, Category = "Movement" },
                [5] = new OpCodeInfo { Name = "INIZ MEM", ParamCount = 2, IndentMode = IndentMode.None, Category = "Memory" },
                [7] = new OpCodeInfo { Name = "LOGICA", ParamCount = 8, IndentMode = IndentMode.None, Category = "General" },
                [9] = new OpCodeInfo { Name = "AMUOVI MEM", ParamCount = 3, IndentMode = IndentMode.None, Category = "Movement" },
                [10] = new OpCodeInfo { Name = "VELOCITA", ParamCount = 2, IndentMode = IndentMode.None, Category = "Movement" },
                [12] = new OpCodeInfo { Name = "AZZERA", ParamCount = 1, IndentMode = IndentMode.None, Category = "Memory" },
                [13] = new OpCodeInfo { Name = "IF", ParamCount = 1, IndentMode = IndentMode.AddIndent, Category = "Control Flow" },
                [14] = new OpCodeInfo { Name = "ELSE", ParamCount = 0, IndentMode = IndentMode.RemoveAndAddIndent, Category = "Control Flow" },
                [15] = new OpCodeInfo { Name = "END IF", ParamCount = 0, IndentMode = IndentMode.RemoveIndent, Category = "Control Flow" },
                [16] = new OpCodeInfo { Name = "FORZA", ParamCount = 2, IndentMode = IndentMode.None, Category = "IO" },
                [17] = new OpCodeInfo { Name = "ASPETTA", ParamCount = 4, IndentMode = IndentMode.None, Category = "Timing" },
                [19] = new OpCodeInfo { Name = "PIU", ParamCount = 0, IndentMode = IndentMode.None, Category = "General" },
                [20] = new OpCodeInfo { Name = "PAUSA", ParamCount = 1, IndentMode = IndentMode.None, Category = "Timing" },
                [21] = new OpCodeInfo { Name = "ACCEL", ParamCount = 2, IndentMode = IndentMode.None, Category = "Movement" },
                [36] = new OpCodeInfo { Name = "AZIONA", ParamCount = 6, IndentMode = IndentMode.None, Category = "IO" },
                [37] = new OpCodeInfo { Name = "LABEL", ParamCount = 1, IndentMode = IndentMode.None, Category = "Control Flow" },
                [38] = new OpCodeInfo { Name = "CALL", ParamCount = 3, IndentMode = IndentMode.AddIndent, Category = "Control Flow" },
                [39] = new OpCodeInfo { Name = "ENDCALL", ParamCount = 0, IndentMode = IndentMode.RemoveIndent, Category = "Control Flow" },
                [40] = new OpCodeInfo { Name = "STOP", ParamCount = 0, IndentMode = IndentMode.None, Category = "Control Flow" },
                [49] = new OpCodeInfo { Name = "IMPULSO", ParamCount = 2, IndentMode = IndentMode.None, Category = "IO" },
                [54] = new OpCodeInfo { Name = "SET PRESA", ParamCount = 8, IndentMode = IndentMode.None, Category = "IO" },
                [57] = new OpCodeInfo { Name = "RIL. PEZZO", ParamCount = 0, IndentMode = IndentMode.None, Category = "IO" },
                [92] = new OpCodeInfo { Name = "WR_EVENT", ParamCount = 8, IndentMode = IndentMode.None, Category = "System" },
                [94] = new OpCodeInfo { Name = "G_MISSIONE", ParamCount = 0, IndentMode = IndentMode.None, Category = "System" },
                [95] = new OpCodeInfo { Name = "JOB", ParamCount = 1, IndentMode = IndentMode.None, Category = "System" },
                [96] = new OpCodeInfo { Name = "ERRORE", ParamCount = 1, IndentMode = IndentMode.None, Category = "Error Handling" },
                [97] = new OpCodeInfo { Name = "PROCESSO", ParamCount = 2, IndentMode = IndentMode.None, Category = "System" },
                [104] = new OpCodeInfo { Name = "IF_MEM", ParamCount = 4, IndentMode = IndentMode.AddIndent, Category = "Control Flow" },
                [105] = new OpCodeInfo { Name = "IF_NUM", ParamCount = 3, IndentMode = IndentMode.AddIndent, Category = "Control Flow" },
                [106] = new OpCodeInfo { Name = "SET TERNA", ParamCount = 0, IndentMode = IndentMode.None, Category = "General" },
                [107] = new OpCodeInfo { Name = "OK PRESA", ParamCount = 0, IndentMode = IndentMode.None, Category = "IO" },
                [113] = new OpCodeInfo { Name = "CLIENT", ParamCount = 1, IndentMode = IndentMode.None, Category = "System" },
                [114] = new OpCodeInfo { Name = "ATM", ParamCount = 0, IndentMode = IndentMode.None, Category = "System" },
                [115] = new OpCodeInfo { Name = "MAGAZZINO", ParamCount = 0, IndentMode = IndentMode.None, Category = "System" },
                [116] = new OpCodeInfo { Name = "VERIF NCP", ParamCount = 0, IndentMode = IndentMode.None, Category = "System" },
                [117] = new OpCodeInfo { Name = "COPIA ZONA", ParamCount = 2, IndentMode = IndentMode.None, Category = "Memory" },
                [119] = new OpCodeInfo { Name = "GET EDGE", ParamCount = 7, IndentMode = IndentMode.None, Category = "System" },
                [120] = new OpCodeInfo { Name = "CHECKRUN ASPS", ParamCount = 0, IndentMode = IndentMode.None, Category = "System" },
                [121] = new OpCodeInfo { Name = "RESET MAGAZ", ParamCount = 0, IndentMode = IndentMode.None, Category = "System" },
                [122] = new OpCodeInfo { Name = "CASS ASPS", ParamCount = 0, IndentMode = IndentMode.None, Category = "System" }
            };

            return Task.FromResult(opCodes);
        }
    }
}