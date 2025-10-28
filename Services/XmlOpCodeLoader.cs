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
    /// Loader per file XML OpCodes (formato futuro)
    /// </summary>
    public class XmlOpCodeLoader : IOpCodeLoader
    {
        public string LoaderName => "XML File Loader";

        public bool CanHandle(string filePath)
        {
            return Path.GetExtension(filePath).ToLower() == ".xml";
        }

        public async Task<Dictionary<int, OpCodeInfo>> LoadAsync(string filePath)
        {
            var opCodes = new Dictionary<int, OpCodeInfo>();

            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                var doc = XDocument.Parse(content);

                // Esempio di struttura XML prevista:
                // <OpCodes>
                //   <OpCode id="105" name="IF_NUM" paramCount="3" category="Control Flow">
                //     <Description>Conditional statement with numeric comparison</Description>
                //     <Parameters>
                //       <Parameter name="value" type="int"/>
                //       <Parameter name="comparison" type="int"/>
                //       <Parameter name="target" type="int"/>
                //     </Parameters>
                //   </OpCode>
                // </OpCodes>

                foreach (var opElement in doc.Descendants("OpCode"))
                {
                    var idAttr = opElement.Attribute("id");
                    var nameAttr = opElement.Attribute("name");

                    if (idAttr != null && nameAttr != null &&
                        int.TryParse(idAttr.Value, out int opCode))
                    {
                        var paramCountAttr = opElement.Attribute("paramCount");
                        var categoryAttr = opElement.Attribute("category");
                        var descriptionElement = opElement.Element("Description");

                        var paramTypes = new List<string>();
                        var parametersElement = opElement.Element("Parameters");
                        if (parametersElement != null)
                        {
                            foreach (var paramElement in parametersElement.Elements("Parameter"))
                            {
                                var typeAttr = paramElement.Attribute("type");
                                paramTypes.Add(typeAttr?.Value ?? "unknown");
                            }
                        }

                        opCodes[opCode] = new OpCodeInfo
                        {
                            Name = nameAttr.Value,
                            ParamCount = paramCountAttr != null && int.TryParse(paramCountAttr.Value, out int pc) ? pc : 0,
                            Category = categoryAttr?.Value ?? "General",
                            Description = descriptionElement?.Value ?? "",
                            ParamTypes = paramTypes
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
    }
}