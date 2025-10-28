using System.Threading.Tasks;
using FileViewerApp.Enums;
using FileViewerApp.Models;

namespace FileViewerApp.Interfaces
{
    /// <summary>
    /// Interfaccia per processori di file specifici per tipo
    /// </summary>
    public interface IFileProcessor
    {
        /// <summary>
        /// Tipo di file supportato da questo processor
        /// </summary>
        FileType SupportedType { get; }

        /// <summary>
        /// Verifica se questo processor può gestire il file
        /// </summary>
        bool CanProcess(string filePath, byte[] content);

        /// <summary>
        /// Processa un file e restituisce le informazioni estratte
        /// </summary>
        Task<ProcessedFile> ProcessAsync(string filePath, byte[] content);

        /// <summary>
        /// Salva un file processato nel formato appropriato
        /// </summary>
        Task<byte[]> SaveAsync(ProcessedFile file, string outputPath);
    }
}