using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ProjectReferenceTool.ViewModels;

namespace ProjectReferenceTool {

    /// <summary>
    /// Resolver class for working with project references.
    /// </summary>
    public static class Resolver {

        /// <summary>
        /// Gets all references in the project files.
        /// </summary>
        public static IEnumerable<ReferenceViewModel> GetReferences(string moduleRoot) {

            // find all referenced files in "packages" folder
            List<PackageAssemblyInfo> packageLibraries = new List<PackageAssemblyInfo>();

            string packagesRoot = Path.Combine(moduleRoot, "packages");

            var libFolders = Directory.GetDirectories(packagesRoot, "*lib*", SearchOption.AllDirectories);

            foreach (var libFolder in libFolders) {
                foreach (var file in Directory.GetFileSystemEntries(libFolder, "*.dll", SearchOption.AllDirectories)) {
                    var fileName = Path.GetFileName(file);
                    if (fileName == null) {
                        continue;
                    }
                    var rel = file.Replace(packagesRoot, "..\\..\\packages");

                    var dep = file.Replace(packagesRoot + @"\", "");
                    var folderDepth = dep.Split('\\').Length;

                    packageLibraries.Add(new PackageAssemblyInfo() {
                        FileName = fileName,
                        RelativeFileName = rel,
                        FolderDepth = folderDepth
                    });
                }
            }

            var orderedLibraries = packageLibraries
                .OrderBy(v => v.FolderDepth).ToList();

            // process all project files
            var projectFiles = Directory.GetFileSystemEntries(moduleRoot, "*.csproj", SearchOption.AllDirectories).ToList();

            foreach (var projectFile in projectFiles) {

                var projectDoc = XDocument.Load(projectFile);
                var hps = projectDoc.Root.Descendants("{http://schemas.microsoft.com/developer/msbuild/2003}HintPath").ToList();

                foreach (var hp in hps) {

                    var referenceViewModel = new ReferenceViewModel() {
                        Project = projectFile,
                        Assembly = Path.GetFileName(hp.Value),
                        CurrentValue = hp.Value,
                    };

                    referenceViewModel.Options = new ObservableCollection<PackageAssemblyInfo>(
                        orderedLibraries.Where(v => v.FileName == referenceViewModel.Assembly));

                    if (referenceViewModel.Options.Any()) {
                        referenceViewModel.BestMatch();
                        yield return referenceViewModel;
                    }
                    
                }
            }

        }

        /// <summary>
        /// Applies all changes.
        /// </summary>
        public static void ApplyChanges(IEnumerable<ReferenceViewModel> references) {

            foreach (var group in references.GroupBy(v => v.Project)) {

                string projectFile = group.Key;

                var projectDoc = XDocument.Load(projectFile);
                var hps = projectDoc.Root.Descendants("{http://schemas.microsoft.com/developer/msbuild/2003}HintPath").ToList();

                foreach (var hp in hps) {
                    var assemblyName = Path.GetFileName(hp.Value);

                    foreach (var reference in group) {
                        if (reference.Assembly == assemblyName && reference.Update) {
                            hp.Value = reference.NewValue;
                        }
                    }
                }
                
                if (group.Any(v => v.Update)) {
                    projectDoc.Save(projectFile);    
                }

                foreach (var item in group) {
                    item.IsSaved = true;
                }
            }
        }
    }
}
