using System.Collections.Generic;
using System.Threading.Tasks;
using FileViewerApp.Interfaces;
using FileViewerApp.Models;

namespace FileViewerApp.Services
{
    /// <summary>
    /// Loader di fallback con OpCodes hardcoded
    /// </summary>
    public class FallbackOpCodeLoader : IOpCodeLoader
    {
        public string LoaderName => "Fallback Loader";

        public bool CanHandle(string filePath)
        {
            // Il fallback gestisce sempre tutto come ultima risorsa
            return true;
        }

        public Task<Dictionary<int, OpCodeInfo>> LoadAsync(string filePath)
        {
            var opCodes = new Dictionary<int, OpCodeInfo>
            {
                [0] = new OpCodeInfo { Name = "FINE", ParamCount = 0 },
                [1] = new OpCodeInfo { Name = "NULLA", ParamCount = 0 },
                [3] = new OpCodeInfo { Name = "AMUOVI", ParamCount = 3 },
                [4] = new OpCodeInfo { Name = "RMUOVI", ParamCount = 3 },
                [5] = new OpCodeInfo { Name = "INIZ MEM", ParamCount = 2 },
                [7] = new OpCodeInfo { Name = "LOGICA", ParamCount = 8 },
                [9] = new OpCodeInfo { Name = "AMUOVI MEM", ParamCount = 3 },
                [10] = new OpCodeInfo { Name = "VELOCITA", ParamCount = 2 },
                [12] = new OpCodeInfo { Name = "AZZERA", ParamCount = 1 },
                [13] = new OpCodeInfo { Name = "IF", ParamCount = 1 },
                [14] = new OpCodeInfo { Name = "ELSE", ParamCount = 0 },
                [15] = new OpCodeInfo { Name = "END IF", ParamCount = 0 },
                [16] = new OpCodeInfo { Name = "FORZA", ParamCount = 2 },
                [17] = new OpCodeInfo { Name = "ASPETTA", ParamCount = 4 },
                [19] = new OpCodeInfo { Name = "PIU", ParamCount = 0 },
                [20] = new OpCodeInfo { Name = "PAUSA", ParamCount = 1 },
                [21] = new OpCodeInfo { Name = "ACCEL", ParamCount = 2 },
                [36] = new OpCodeInfo { Name = "AZIONA", ParamCount = 6 },
                [37] = new OpCodeInfo { Name = "LABEL", ParamCount = 1 },
                [38] = new OpCodeInfo { Name = "CALL", ParamCount = 3 },
                [39] = new OpCodeInfo { Name = "ENDCALL", ParamCount = 0 },
                [40] = new OpCodeInfo { Name = "STOP", ParamCount = 0 },
                [49] = new OpCodeInfo { Name = "IMPULSO", ParamCount = 2 },
                [54] = new OpCodeInfo { Name = "SET PRESA", ParamCount = 8 },
                [57] = new OpCodeInfo { Name = "RIL. PEZZO", ParamCount = 0 },
                [92] = new OpCodeInfo { Name = "WR_EVENT", ParamCount = 8 },
                [94] = new OpCodeInfo { Name = "G_MISSIONE", ParamCount = 0 },
                [95] = new OpCodeInfo { Name = "JOB", ParamCount = 1 },
                [96] = new OpCodeInfo { Name = "ERRORE", ParamCount = 1 },
                [97] = new OpCodeInfo { Name = "PROCESSO", ParamCount = 2 },
                [104] = new OpCodeInfo { Name = "IF_MEM", ParamCount = 4 },
                [105] = new OpCodeInfo { Name = "IF_NUM", ParamCount = 3 },
                [106] = new OpCodeInfo { Name = "SET TERNA", ParamCount = 0 },
                [107] = new OpCodeInfo { Name = "OK PRESA", ParamCount = 0 },
                [113] = new OpCodeInfo { Name = "CLIENT", ParamCount = 1 },
                [114] = new OpCodeInfo { Name = "ATM", ParamCount = 0 },
                [115] = new OpCodeInfo { Name = "MAGAZZINO", ParamCount = 0 },
                [116] = new OpCodeInfo { Name = "VERIF NCP", ParamCount = 0 },
                [117] = new OpCodeInfo { Name = "COPIA ZONA", ParamCount = 2 },
                [119] = new OpCodeInfo { Name = "GET EDGE", ParamCount = 7 },
                [120] = new OpCodeInfo { Name = "CHECKRUN ASPS", ParamCount = 0 },
                [121] = new OpCodeInfo { Name = "RESET MAGAZ", ParamCount = 0 },
                [122] = new OpCodeInfo { Name = "CASS ASPS", ParamCount = 0 }
            };

            return Task.FromResult(opCodes);
        }
    }
}