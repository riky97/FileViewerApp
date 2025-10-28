using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileViewerApp.Enums;


namespace FileViewerApp.Interfaces
{
    public interface IFileTypeDetectionService
    {
        FileType DetectFileType(string filePath, byte[] content);
        IFileProcessor GetProcessor(FileType fileType);
    }
}