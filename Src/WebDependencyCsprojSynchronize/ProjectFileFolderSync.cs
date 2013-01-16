using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WebDependencyCsprojSynchronize {
    /// <summary>
    /// Used to synchronize files in a folder and a csproj file. 
    /// This is used by the web projects as a get dependencies brings over js and css files, and these
    /// need to be added to the csproj. 
    /// It will only process webXXX.csproj files.
    /// </summary>
    internal class ProjectFileFolderSync {
        public void Synchronize(string dependenciesFolderPath) {
            if (string.IsNullOrWhiteSpace(dependenciesFolderPath)) {
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
                var projFile = Directory.GetFiles(directory, "*.csproj").FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(projFile) && projFile.ToUpperInvariant().Contains("WEB.")) { // we only update web project files
                    SynchronizeProject(projFile, directory);        
                }
            }
        }

        private void SynchronizeProject(string projFilePath, string projFileFolder) {
            string[] folders = {"Content", "Content\\Includes", "Scripts", "Views\\Shared", "ViewModels", "Authentication", "ManualLogon" };
            XDocument projFileDoc = XDocument.Load(projFilePath);
            foreach (string folder in folders.OrderBy((s => s))) {
                string folderPath = Path.Combine(projFileFolder, folder);
                if (!Directory.Exists(folderPath)) {
                    continue;
                }

                XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
                // delete third party and web folders embedded in a folder
                List<string> dependencyfolders = Directory.GetDirectories(folderPath, "ThirdParty.*").Concat(Directory.GetDirectories(folderPath, "Web.*")).OrderBy(s => s).ToList();
                foreach (var dependencyfolder in dependencyfolders) {
                    var tp = dependencyfolder.Split('\\').Last();
                    projFileDoc.Descendants(ns + "ItemGroup").Descendants().Where(d => (d.Name == ns + "Content" || d.Name == ns + "None") && d.Attribute("Include").Value.StartsWith(folder + '\\' + tp)).Remove();
                    // find all files now in these folders and re-add them
                    var folderToAddToProj = Path.Combine(folderPath, tp);
                    AddFilesToProj(projFileDoc, ns, folderToAddToProj, Path.Combine(folder, tp));
                    FileInfo fileInfo2 = new FileInfo(projFilePath);
                    fileInfo2.IsReadOnly = false;
                    fileInfo2.Refresh();
                    projFileDoc.Save(projFilePath);

                }
            }
            FileInfo fileInfo = new FileInfo(projFilePath);
            fileInfo.IsReadOnly = false;
            fileInfo.Refresh();
            projFileDoc.Save(projFilePath);
        }

        private void AddFilesToProj(XDocument projFileDoc, XNamespace ns, string folderToAddToProj, string rootPath) {
            var node = projFileDoc.Descendants(ns + "ItemGroup").First();

            foreach (var file in Directory.GetFiles(folderToAddToProj).OrderBy(s => s)) {
                var index = file.ToUpperInvariant().IndexOf(rootPath.ToUpperInvariant(), StringComparison.Ordinal);
                if (file.EndsWith(".tt")) {
                    string ttName = file.Substring(file.LastIndexOfAny(new char[] { '\\', '/' }) + 1);
                    string jsName = ttName.Replace(".tt", ".js");
                    string path = file.Substring(index, file.Length - index - ttName.Length);
                    node.Add(new XElement(ns + "Content", new XAttribute("Include", path + jsName)
                        , new XElement(ns + "AutoGen", "True")
                        , new XElement(ns + "DesignTime", "True")
                        , new XElement(ns + "DependentUpon", ttName)
                        ));
                    node.Add(new XElement(ns + "None", new XAttribute("Include", path + ttName)
                        , new XElement(ns + "Generator", "TextTemplatingFileGenerator")
                        , new XElement(ns + "LastGenOutput", jsName)
                        ));
                } else {
                    node.Add(new XElement(ns + "Content", new XAttribute("Include", file.Substring(index))));
                }
            }

            foreach (var directory in Directory.GetDirectories(folderToAddToProj).OrderBy(s => s)) {
                AddFilesToProj(projFileDoc, ns, directory, rootPath);
            }

        }
    }
}
