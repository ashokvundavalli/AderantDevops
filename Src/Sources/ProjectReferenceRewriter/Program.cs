using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ProjectReferenceRewriter {
    class Program {

        /// <summary>
        /// This utility will go through all .csproj files in the "target" folder and change any reference "hint paths" currently targeting "Dependencies" to target the "packages" folder instead. 
        /// </summary>
        static void Main(string[] args) {

            Console.WriteLine("************************************************");
            Console.WriteLine("**        Project Reference Rewriter          **");
            Console.WriteLine("**  Copyright (c) 2016 ADERANT Holdings, Inc. **");
            Console.WriteLine("************************************************");
            Console.WriteLine("");

            if (!args.Any()) {
                Console.WriteLine(@"Usage: ProjectReferenceRewriter.exe c:\path\to\solution.");
                Console.WriteLine("");
                return;
            }

            string solutionRoot = Path.GetFullPath(args[0]);

            if (!Directory.Exists(solutionRoot)) {
                Console.WriteLine("Solution folder does not exist or is not valid");
                return;
            }

            Console.WriteLine("Solution: " + solutionRoot);

            RewriteReferences(solutionRoot);

            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();

        }

        private static void RewriteReferences(string solutionRoot) {
            
            // find all referenced files in "packages" folder
            Dictionary<string, string> packageLibraries = new Dictionary<string, string>();

            string packagesRoot = Path.Combine(solutionRoot, "packages");
            var libFolders = Directory.GetDirectories(packagesRoot, "*lib*", SearchOption.AllDirectories);

            foreach (var libFolder in libFolders) {
                foreach (var file in Directory.GetFileSystemEntries(libFolder, "*.dll", SearchOption.AllDirectories)) {
                    var fileName = Path.GetFileName(file);
                    if (fileName == null) {
                        continue;
                    }
                    var rel = file.Replace(packagesRoot, "..\\..\\packages");

                    if (!packageLibraries.ContainsKey(fileName)) {
                        packageLibraries.Add(fileName, rel);
                    }
                }
            }

            var projectFiles = Directory.GetFileSystemEntries(solutionRoot, "*.csproj", SearchOption.AllDirectories).ToList();

            foreach (var projectFile in projectFiles) {
                bool hasChanges = false;
                Console.WriteLine("Processing: " + Path.GetFileName(projectFile));

                var projectDoc = XDocument.Load(projectFile);
                var hps = projectDoc.Root.Descendants("{http://schemas.microsoft.com/developer/msbuild/2003}HintPath").ToList();

                foreach (var hp in hps) {
                    var oldValue = Path.GetFileName(hp.Value);

                    // see if there is a replacement
                    if (packageLibraries.ContainsKey(oldValue)) {
                        var newValue = packageLibraries[oldValue];

                        if (newValue != hp.Value) {
                            Console.WriteLine($"Replacing \"{hp.Value}\" with \"{newValue}\"" + projectFile);
                            hp.Value = newValue;
                            hasChanges = true;
                        }
                    }
                }

                if (hasChanges) {
                    projectDoc.Save(projectFile);
                }
            }
        }
    }
}
