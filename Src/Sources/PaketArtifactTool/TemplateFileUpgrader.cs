using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Aderant.Build;
using PaketArtifactTool.ViewModels;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.Packaging.Parsing;

namespace PaketArtifactTool {
    
    internal class TemplateFileUpgrader {
        private readonly IFileSystem2 fs;
        private readonly string templateFile;
        private readonly ILogger logger;

        internal TemplateFileUpgrader(IFileSystem2 fs, string templateFile, ILogger logger) {
            this.fs = fs;
            this.templateFile = templateFile;
            this.logger = logger;

            var contents = fs.ReadAllText(templateFile);
            var parser = new IndendedFileParser();
            parser.Parse(contents);

            Name = parser["id"].GetValueWithoutKey();
            Files = new List<string>(parser["files"].Values.Select(v => v.Trim()));
        }


        public string Name { get; set; }

        public List<string> Files { get; set; }

        public void Write() {
            
            // find the target .proj file

            var module = Path.GetDirectoryName(this.templateFile);
            if (string.IsNullOrWhiteSpace(module)) {
                logger.Error("Unable to find module path.");
                return;
            }

            var proj = Path.Combine(module, "Build", "TFSBuild.proj");
            if (!fs.FileExists(proj)) {
                logger.Error($"Target was not found.");
                return;
            }

            // write to the file
            logger.Info($"Writing section \"{Name}\" to \"{proj}\".");

            var xmlContents = fs.ReadAllText(proj);
            XDocument document = XDocument.Parse(xmlContents, LoadOptions.PreserveWhitespace);
            
            if (document?.Root == null) {
                logger.Error("Unable to parse document");
                return;
            }
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            var targets = document.Root.Descendants(ns + "Target").ToList();
            XElement pa = targets.FirstOrDefault(v => v.Attribute("Name")?.Value == "PackageArtifacts");

            if (pa == null) {
                pa = new XElement(ns + "Target", new XAttribute("Name", "PackageArtifacts"));
                document.Root.Add(pa);
            }

            var itemGroup = pa.Descendants(ns + "ItemGroup").FirstOrDefault();
            if (itemGroup == null) {
                itemGroup = new XElement(ns + "ItemGroup");
                pa.Add(itemGroup);
            }

            XElement artifact = null;
            foreach (var currentArtifact in itemGroup.Descendants(ns + "PackageArtifact")) {
                var name = currentArtifact.Elements(ns + "ArtifactId").FirstOrDefault()?.Value;
                if (name == Name) {
                    artifact = currentArtifact;
                    break;
                }
            }

            if (artifact != null) {
                logger.Info($"Replacing existing artifact for {Name}");
                artifact.Remove();
            } else {
                logger.Info($"Adding new artifact for {Name}");
            }
          
            artifact = new XElement(
                ns + "PackageArtifact",
                new XElement(ns + "ArtifactId", Name),
                new XAttribute("Include", Convert(Files)));

            itemGroup.Add(artifact);

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true;
            settings.NewLineHandling = NewLineHandling.Replace;
            settings.Indent = true;
            settings.IndentChars = "  ";
            settings.DoNotEscapeUriAttributes = true;
            
            using (XmlWriter xw = XmlWriter.Create(proj, settings)) {
                document.Save(xw);
            }
        }

        private string Convert(List<string> files) {
            
            StringBuilder sb = new StringBuilder();

            foreach (var file in files) {
                if (sb.Length > 0) {
                    sb.Append(";");
                }

                string[] parts = file.Split(' ');
                if (parts.Length > 0) {
                    sb.AppendLine(SanitizePath("$(SolutionRoot)/" + parts[0].Trim()));
                }
            }

            return sb.ToString();
        }

        private string SanitizePath(string path) {
            return path.Replace("/", "\\");
        }
    }

}
