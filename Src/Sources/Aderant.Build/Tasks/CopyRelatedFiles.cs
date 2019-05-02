using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class CopyRelatedFiles : BuildOperationContextTask {
        [Required]
        public string SourceLocation { get; set; }

        [Required]
        public string Destination { get; set; }
        
        private HashSet<string> ProcessedFiles { get; set; }

        public override bool ExecuteTask() {
            ProcessedFiles = new HashSet<string>();
            try {
                var files = PipelineService.GetRelatedFiles();
                foreach (var key in files.Keys) {
                    var filePath = Path.Combine(Destination, key);
                    if (File.Exists(filePath)) {
                        CopyFiles(key, files[key]);
                        continue;
                    }

                    if (WildcardPattern.ContainsWildcardCharacters(key)) {
                        var pattern = WildcardPattern.Get(key, WildcardOptions.IgnoreCase);
                        foreach (var file in Directory.GetFiles(SourceLocation, "*", SearchOption.AllDirectories)) {
                            if (pattern.IsMatch(file)) {
                                CopyFiles(file, files[key]);
                            }
                        }
                    }
                }
                return !Log.HasLoggedErrors;
            } catch (Exception ex) {
                Log.LogErrorFromException(ex);
                return false;
            }
        }

        internal void CopyFiles(string triggeringFile, List<string> relatedFiles) {
            Parallel.ForEach(relatedFiles, relatedFile => {
                var relatedFilePath = Path.Combine(SourceLocation, relatedFile);
                var destinationPath = Path.Combine(Destination, relatedFile);

                if (relatedFile.IndexOf(SourceLocation, StringComparison.Ordinal) >= 0) {
                    var relativePath = relatedFile.Replace(SourceLocation, "");
                    destinationPath = Path.Combine(Destination, relativePath);
                    var relativeDirectory = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(relativeDirectory)) {
                        Directory.CreateDirectory(relativeDirectory);
                    }
                }

                if (File.Exists(relatedFilePath) && relatedFilePath != triggeringFile && !ProcessedFiles.Contains(relatedFilePath)) {
                    Log.LogMessage($"Copying related file for {Path.GetFileName(triggeringFile)} to {destinationPath}");
                    File.Copy(relatedFilePath, destinationPath, true);
                    ProcessedFiles.Add(relatedFilePath);
                }

                if (WildcardPattern.ContainsWildcardCharacters(relatedFile)) {
                    var pattern = WildcardPattern.Get(relatedFile, WildcardOptions.IgnoreCase);
                    foreach (var file in Directory.GetFiles(SourceLocation, "*", SearchOption.AllDirectories)) {
                        if (pattern.IsMatch(file)) {
                            CopyFiles(triggeringFile, new List<string> { file });
                        }
                    }
                }
            });
        }
    }
}
