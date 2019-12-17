using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class SourceTransformationTask : Task {

        public string SourcePath { get; set; }

        public string TransformsDefinitionFile { get; set; }

        public override bool Execute() {
            Log.LogMessage(string.Format("Starting transformation of source at {0} using {1}", SourcePath, TransformsDefinitionFile));

            // Get the list of transformations
            if (!File.Exists(TransformsDefinitionFile)) {
                Log.LogError(string.Format("The transformations file {0} does not exist", TransformsDefinitionFile));
            }

            XDocument transformsDoc = XDocument.Load(TransformsDefinitionFile);
            var replacements = from replacementDefinition in transformsDoc.Root.Descendants("Replacement")
                from file in Directory.GetFiles(SourcePath, replacementDefinition.Attribute("FileFilter").Value, SearchOption.AllDirectories)
                select new {
                    FilePath = file,
                    Regex = new Regex(replacementDefinition.Attribute("Pattern").Value),
                    ReplaceWith = replacementDefinition.Attribute("ReplaceWith").Value
                };

            Log.LogMessage(string.Format("Starting {0} replacement tasks", replacements.Count()));

            foreach (var replacement in replacements) {
                string fileContent = File.ReadAllText(replacement.FilePath);
                fileContent = replacement.Regex.Replace(fileContent, replacement.ReplaceWith);
                File.WriteAllText(replacement.FilePath, fileContent);
            }

            return !Log.HasLoggedErrors;
        }
    }
}