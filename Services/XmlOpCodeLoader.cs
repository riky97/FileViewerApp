using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using FileViewerApp.Interfaces;
using FileViewerApp.Models;

namespace FileViewerApp.Services
{
    /// <summary>
    /// Loader per file XML OpCodes (INFO.XML)
    /// </summary>
    public class XmlOpCodeLoader : IOpCodeLoader
    {
        public string LoaderName => "XML OpCode Loader";

        public bool CanHandle(string filePath)
        {
            return Path.GetExtension(filePath).ToLower() == ".xml" ||
                   Path.GetFileName(filePath).ToUpper().Contains("INFO");
        }

        public async Task<Dictionary<int, OpCodeInfo>> LoadAsync(string filePath)
        {
            var opCodes = new Dictionary<int, OpCodeInfo>();

            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                var doc = XDocument.Parse(content);

                foreach (var cmdElement in doc.Descendants("CMD"))
                {
                    var nameAttr = cmdElement.Attribute("Name");
                    var idAttr = cmdElement.Attribute("ID");
                    var indentModeAttr = cmdElement.Attribute("IndentMode");
                    var activeAttr = cmdElement.Attribute("Active");
                    var versAttr = cmdElement.Attribute("Vers");
                    var descrAttr = cmdElement.Attribute("Descr");
                    var imgNameAttr = cmdElement.Attribute("ImgName");

                    if (nameAttr != null && idAttr != null &&
                        int.TryParse(idAttr.Value, out int opCodeId))
                    {
                        // Parse IndentMode
                        var indentMode = ParseIndentMode(indentModeAttr?.Value ?? "NONE");

                        // Parse parameters
                        var parameters = new List<ParameterInfo>();
                        foreach (var parElement in cmdElement.Elements("PAR"))
                        {
                            var param = ParseParameter(parElement);
                            parameters.Add(param);
                        }

                        opCodes[opCodeId] = new OpCodeInfo
                        {
                            Name = nameAttr.Value,
                            Id = opCodeId,
                            ParamCount = parameters.Count,
                            IndentMode = indentMode,
                            Category = DetermineCategory(nameAttr.Value),
                            Description = descrAttr?.Value ?? "",
                            Version = int.TryParse(versAttr?.Value, out int v) ? v : 1,
                            Active = bool.TryParse(activeAttr?.Value, out bool a) ? a : true,
                            ImageName = imgNameAttr?.Value ?? "",
                            Parameters = parameters
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore parsing XML: {ex.Message}");
                throw;
            }

            return opCodes;
        }

        private IndentMode ParseIndentMode(string indentModeStr)
        {
            return indentModeStr.ToUpper() switch
            {
                "ADD_INDENT" => IndentMode.AddIndent,
                "REMOVE_INDENT" => IndentMode.RemoveIndent,
                "REMOVE_AND_ADD_INDENT" => IndentMode.RemoveAndAddIndent,
                _ => IndentMode.None
            };
        }

        private ParameterInfo ParseParameter(XElement parElement)
        {
            var nameAttr = parElement.Attribute("Name");
            var typeAttr = parElement.Attribute("Type");
            var descrAttr = parElement.Attribute("Descr");
            var valueAttr = parElement.Attribute("Value");
            var minAttr = parElement.Attribute("Min");
            var maxAttr = parElement.Attribute("Max");

            var resourceGroups = new List<string>();
            foreach (var resGroupElement in parElement.Elements("RESGROUP"))
            {
                var resNameAttr = resGroupElement.Attribute("Name");
                if (resNameAttr != null)
                {
                    resourceGroups.Add(resNameAttr.Value);
                }
            }

            return new ParameterInfo
            {
                Name = nameAttr?.Value ?? "",
                Type = typeAttr?.Value ?? "INT",
                Description = descrAttr?.Value ?? "",
                DefaultValue = double.TryParse(valueAttr?.Value, out double def) ? def : 0,
                MinValue = double.TryParse(minAttr?.Value, out double min) ? min : 0,
                MaxValue = double.TryParse(maxAttr?.Value, out double max) ? max : 0,
                ResourceGroups = resourceGroups
            };
        }

        private string DetermineCategory(string name)
        {
            return name.ToUpper() switch
            {
                var n when n.Contains("IF") || n.Contains("ELSE") || n.Contains("END") || n.Contains("CALL") || n.Contains("LABEL") => "Control Flow",
                var n when n.Contains("MEM") || n.Contains("AZZERA") || n.Contains("COPIA") => "Memory",
                var n when n.Contains("MUOVI") || n.Contains("VELOCITA") || n.Contains("ACCEL") => "Movement",
                var n when n.Contains("ASPETTA") || n.Contains("PAUSA") => "Timing",
                var n when n.Contains("FORZA") || n.Contains("AZIONA") || n.Contains("PRESA") || n.Contains("IMPULSO") => "IO",
                var n when n.Contains("ERRORE") => "Error Handling",
                var n when n.Contains("PROCESSO") || n.Contains("JOB") || n.Contains("ATM") || n.Contains("MAGAZZINO") => "System",
                _ => "General"
            };
        }
    }
}