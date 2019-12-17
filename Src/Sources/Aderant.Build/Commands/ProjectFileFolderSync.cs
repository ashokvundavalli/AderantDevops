using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Aderant.Build.Commands {
    /// <summary>
    /// Used to synchronize files in a folder and a csproj file. 
    /// This is used by the web projects as a get dependencies brings over js and css files, and these
    /// need to be added to the csproj. 
    /// It will only process webXXX.csproj files.
    /// </summary>
    [Cmdlet(VerbsData.Sync, "WebProject")]
    public class ProjectFileFolderSync : PSCmdlet {

        [Parameter(Mandatory = true, Position = 0, HelpMessage = "Specifies the dependencies directory to use for synchronization")]
        public string ModuleDependenciesDirectory {
            get; set; 
        }

        protected override void ProcessRecord() {
            Synchronize(ModuleDependenciesDirectory);
        }

        public void Synchronize(string dependenciesFolderPath) {
            if (IsNullOrWhiteSpace(dependenciesFolderPath)) {
                throw new ArgumentNullException("dependenciesFolderPath");
            }

            if (!Directory.Exists(dependenciesFolderPath)) {
                throw new ArgumentException("Path does not exist " + dependenciesFolderPath);
            }

            dependenciesFolderPath = dependenciesFolderPath.TrimEnd(new[] { '\\', '/' });
            string modulePath = dependenciesFolderPath.Substring(0, dependenciesFolderPath.LastIndexOfAny(new[] { '\\', '/' }));
            string srcPath = Path.Combine(modulePath, "src");
            string[] directories = Directory.GetDirectories(srcPath);
            foreach (var directory in directories) {
                var projectFile = Directory.GetFiles(directory, "*.csproj").FirstOrDefault();
                if (!IsNullOrWhiteSpace(projectFile) && projectFile.ToUpperInvariant().Contains("WEB.")) { // we only update web project files
                    SynchronizeProject(projectFile, directory);
                }
            }
        }

        /// <summary>
        /// IsNullOrWhiteSpace only exists in .Net 4.5.  
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool IsNullOrWhiteSpace(String value) {
            return String.IsNullOrEmpty(value) || value.All(Char.IsWhiteSpace);
        }

        private void SynchronizeProject(string projectFilePath, string projectFileFolder) {
            string[] folders = { "Content", "Content\\Includes", "Scripts", "Views\\Shared", "ViewModels", "Authentication", "ManualLogon" };
            XDocument projectFileDoc = XDocument.Load(projectFilePath);
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            foreach (string folder in folders.OrderBy((s => s))) {
                string folderPath = Path.Combine(projectFileFolder, folder);
                if (!Directory.Exists(folderPath)) {
                    continue;
                }

                List<string> dependencyFolders = Directory
                    .GetDirectories(folderPath, "ThirdParty.*")
                    .Concat(Directory.GetDirectories(folderPath, "Web.*"))
                    .OrderBy(s => s).ToList();
                foreach (var dependencyFolder in dependencyFolders) {
                    var tp = dependencyFolder.Split('\\').Last();
                    string folder1 = folder;
                    projectFileDoc.Descendants(ns + "ItemGroup").Descendants().Where(d => (d.Name == ns + "Content" || d.Name == ns + "None" || d.Name == ns + "TypeScriptCompile")
                        &&
                        (d.Attribute("Include").Value.StartsWith(folder1 + '\\' + tp) || d.Attribute("Include").Value.Contains('\\' + folder1 + '\\' + tp))
                        ).Remove();
                    // find all files now in these folders and re-add them
                    var folderToAddToProject = Path.Combine(folderPath, tp);
                    AddFilesToProject(projectFileDoc, ns, folderToAddToProject, Path.Combine(folder, tp));
                    FileInfo fileInfo2 = new FileInfo(projectFilePath) { IsReadOnly = false };
                    fileInfo2.Refresh();
                    projectFileDoc.Save(projectFilePath);

                }
            }

            //if (Directory.Exists(Path.Combine(projectFileFolder, "ManualLogon"))) {
            //    AddFilesToProject(projectFileDoc, ns, Path.Combine(projectFileFolder, "ManualLogon"), projectFileFolder);
            //}

            FileInfo fileInfo = new FileInfo(projectFilePath) { IsReadOnly = false };
            fileInfo.Refresh();
            projectFileDoc.Save(projectFilePath);
        }

        private void AddFilesToProject(XDocument projectFileDoc, XNamespace ns, string folderToAddToProject, string rootPath) {
            var node = projectFileDoc.Descendants(ns + "ItemGroup").First();
            var files = Directory.GetFiles(folderToAddToProject).OrderBy(s => s);
            foreach (var file in files) {
                if (file.ToUpperInvariant().EndsWith("TYPEDECLARATIONS.D.TS") || file.ToUpperInvariant().EndsWith("TYPEDECLARATIONS.JS")) { // these files are generated, and we don't want the one from the build.
                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                    File.Delete(file);
                    continue;
                }

                var index = file.ToUpperInvariant().IndexOf(rootPath.ToUpperInvariant(), StringComparison.Ordinal);
                string fileName = file.Substring(file.LastIndexOfAny(new[] { '\\', '/' }) + 1);
                string path = file.Substring(index, file.Length - index - fileName.Length);

                if (file.EndsWith(".tt")) { // .tt file, we need to add the project stuff that connects the files so they are "joined" in solution explorer
                    SetProjectFileDependencyAttributes(file, fileName, path, node, ns);
                    continue;

                }

                if (file.EndsWith(".ts")) {
                    UpdateTsFileReferences(file, node, ns, index);
                    continue;
                }

                if (file.EndsWith(".js") || file.EndsWith(".map")) { 
                    // a map or js file, and there is a matching ts file, mark it as dependent upon, e.g. we find a foo.js and there is a foo.ts, then foo.js is DependentUpon the foo.ts file
                    // This is probably redundant now, as we do not have the ts files.  But when we kill MsDeploy it will probalby be required, so i am leaving it here.
                    var matchingTsFile = Path.GetFileNameWithoutExtension(file) + ".ts";
                    var match = files.FirstOrDefault(f => f.EndsWith(matchingTsFile));
                    if (!IsNullOrWhiteSpace(match)) {
                        node.Add(new XElement(ns + "Content", new XAttribute("Include", path + fileName)
                            , new XElement(ns + "DependentUpon", fileName.Replace(".js", ".ts").Replace(".map", ".ts"))
                            ));
                        continue;
                    }
                }

                node.Add(new XElement(ns + "Content", new XAttribute("Include", file.Substring(index))));
            }

            foreach (var directory in Directory.GetDirectories(folderToAddToProject).OrderBy(s => s)) {
                AddFilesToProject(projectFileDoc, ns, directory, rootPath);
            }

        }

        private void SetProjectFileDependencyAttributes(string file, string fileName, string path, XElement node, XNamespace ns) {
            string extension = ".js";
            string outputName = fileName.Replace(".tt", extension);
            var outputLine = File.ReadAllLines(file).FirstOrDefault(l => l.Contains("output extension")); // look for a specified output e.g. .js or .d.ts etc.
            if (!IsNullOrWhiteSpace(outputLine)) {
                Regex reg = new Regex(@"""[^""\\]*(?:\\.[^""\\]*)*"""); // pull extension out of the text
                var match = reg.Match(outputLine);
                extension = match.Value.Replace(@"""", "");
                outputName = fileName.Replace(".tt", extension);
            }

            node.Add(new XElement(ns + "Content", new XAttribute("Include", path + outputName)
                , new XElement(ns + "AutoGen", "True")
                , new XElement(ns + "DesignTime", "True")
                , new XElement(ns + "DependentUpon", fileName)
                ));
            node.Add(new XElement(ns + "None", new XAttribute("Include", path + fileName)
                , new XElement(ns + "Generator", "TextTemplatingFileGenerator")
                , new XElement(ns + "LastGenOutput", outputName)
                ));
        }

        /// <summary>
        /// Updates the ts file references.
        /// If you referenced ../scripts in a module, web.foundation for example, and it is not in ViewModels/Web.Foundation 
        /// in a dependent module, we need to change the reference to ../../scripts
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="node">The node.</param>
        /// <param name="ns">The ns.</param>
        /// <param name="rootPathIndex">Index of the root path.</param>
        private void UpdateTsFileReferences(string file, XElement node, XNamespace ns, int rootPathIndex) {
            // typescript file
            node.Add(new XElement(ns + "TypeScriptCompile", new XAttribute("Include", file.Substring(rootPathIndex))));
            bool changedFile = false;
            string[] tsFile = File.ReadAllLines(file);
            for (int i = 0; i < tsFile.Length; i++) {
                string referenceLine = tsFile[i];
                if (!referenceLine.Contains("/// <reference path")) {
                    // e.g. /// <reference path="..\Scripts\Thirdparty.Knockout\knockout.d.ts" />
                    continue;
                }
                Regex reg = new Regex(@"""[^""\\]*(?:\\.[^""\\]*)*"""); // pull reference path text
                var match = reg.Match(referenceLine);
                var referencedFile = match.Value.Replace(@"""", "");
                string folder = Path.GetFullPath(Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(file), referencedFile)));
                string upOneFolder = Path.GetFullPath(Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(file), "..\\" + referencedFile)));
                if (!Directory.Exists(folder) && Directory.Exists(upOneFolder)) {
                    tsFile[i] = tsFile[i].Replace("\"..\\", "\"..\\..\\");
                    tsFile[i] = tsFile[i].Replace("\"../", "\"../../");
                    changedFile = true;
                }
            }

            if (changedFile) {
                FileInfo fileInfo = new FileInfo(file);
                fileInfo.IsReadOnly = false;
                fileInfo.Refresh();
                File.WriteAllLines(file, tsFile);
            }
        }
    }
}
