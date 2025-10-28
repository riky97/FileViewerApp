using System.Collections.Generic;
using System.Threading.Tasks;
using FileViewerApp.Models;

namespace FileViewerApp.Interfaces
{
    /// <summary>
    /// Interfaccia per caricare OpCodes da diverse sorgenti
    /// </summary>
    public interface IOpCodeLoader
    {
        /// <summary>
        /// Indica se questo loader può gestire il file specificato
        /// </summary>
        bool CanHandle(string filePath);

        /// <summary>
        /// Carica gli OpCodes dal file
        /// </summary>
        Task<Dictionary<int, OpCodeInfo>> LoadAsync(string filePath);

        /// <summary>
        /// Nome descrittivo del loader
        /// </summary>
        string LoaderName { get; }
    }
}