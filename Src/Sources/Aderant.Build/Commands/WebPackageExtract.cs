using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using ICSharpCode.SharpZipLib.Zip;

namespace Aderant.Build.Commands {

    /// <summary>
    /// Used to deploy web assets into the project folders.
    /// </summary>
    [Cmdlet(VerbsData.Update, "WebProjectAssets")]
    public class WebPackageExtract : PSCmdlet {
        
        private const string FolderNameToExtract = "PackageTmp";

        /// <summary>
        /// Gets or sets the module binaries path.
        /// </summary>
        /// <value>
        /// The module binaries path.
        /// </value>
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "Specifies the binaries directory to use for extraction")]
        public string ModuleBinariesPath { get; set; }

        /// <summary>
        /// Gets or sets the module dependencies directory.
        /// </summary>
        /// <value>
        /// The module dependencies directory.
        /// </value>
        [Parameter(Mandatory = true, Position = 1, HelpMessage = "Specifies the dependencies directory to use for extraction")]
        public string ModuleDependenciesDirectory { get; set; }

        protected override void ProcessRecord() {
            ExtractWebPackage(ModuleBinariesPath, ModuleDependenciesDirectory, true);
        }

        public void ExtractWebPackage(string sourceFolder, string destinationDependencyFolder, Boolean copyDlls) {
            if (!Directory.Exists(sourceFolder)) {
                throw new DirectoryNotFoundException(sourceFolder);
            }
            if (!Directory.Exists(destinationDependencyFolder)) {
                Directory.CreateDirectory(destinationDependencyFolder);
            }

            Host.UI.WriteDebugLine(string.Format("Getting Web Dependencies from {0} to {1}", sourceFolder, destinationDependencyFolder));

            string zipFileName = sourceFolder.Split('\\').First(p => p.StartsWith("Web.")) + ".zip"; // e.g. web.workflow.zip
            string moduleName = zipFileName.Split('\\').Last().Replace(".zip", ""); // e.g. web.workflow
            FastZip fz = new FastZip();

            // extract zip to a temporary folder (temporaryFolderWithZip).  32 digits no special chars , 00000000000000000000000000000000
            const string tempFolderPath = @"C:\temp"; // not using Path.GetTempPath as that is too long a path and I can't extract it (>260 chars)
            if (!Directory.Exists(tempFolderPath)) {
                Directory.CreateDirectory(tempFolderPath);
            }
            string temporaryFolderWithZip = Path.Combine(tempFolderPath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryFolderWithZip);
            fz.ExtractZip(Path.Combine(sourceFolder, zipFileName), temporaryFolderWithZip, FastZip.Overwrite.Always, null, String.Empty, String.Empty, false);

            DirectoryInfo directoryInfo = new DirectoryInfo(temporaryFolderWithZip);
            var matchingFolders = directoryInfo.GetDirectories(FolderNameToExtract, SearchOption.AllDirectories);
            if (matchingFolders.Length != 1) {
                throw new ArgumentException(@"there must be one and only one of the folder name requested - " + FolderNameToExtract);
            }
            string folderToDeploy = matchingFolders[0].FullName;
            string[] dependencies = new string[0];

            DeployWebDependenciesToProject(destinationDependencyFolder, folderToDeploy, moduleName, dependencies, copyDlls);
            DeleteFolder(new DirectoryInfo(temporaryFolderWithZip)); // clean out zip folder
        }

        // Public method so it can be used by GetDependenciesFrom, when user has been doing local builds.
        public void DeployWebDependenciesToProject(string destinationDependencyFolder, string folderToDeploy, string moduleName, string[] dependencies, Boolean copyDlls) {
            // copy entire zip into dependencies folder
            string localDependencyFolderName = Path.Combine(destinationDependencyFolder, moduleName);

            if (copyDlls) {
                CopyDirectoryContentsRecursively(folderToDeploy, localDependencyFolderName, dependencies);

                // move dll files to dependency folder, not this sub folder
                var dlls = Directory.GetFiles(Path.Combine(localDependencyFolderName, "bin"), "web.*.dll");
                foreach (string dll in dlls) {
                    string destinationDllName = Path.Combine(destinationDependencyFolder, Path.GetFileName(dll) ?? "");
                    if (File.Exists(destinationDllName)) {
                        File.Delete(destinationDllName);
                    }
                    File.Move(dll, destinationDllName);
                }

                // move pdb files to dependency folder, not this sub folder
                var pdbs = Directory.GetFiles(Path.Combine(localDependencyFolderName, "bin"), "web.*.pdb");
                foreach (string pdb in pdbs) {
                    string destinationPdbName = Path.Combine(destinationDependencyFolder, Path.GetFileName(pdb) ?? "");
                    if (File.Exists(destinationPdbName)) {
                        File.Delete(destinationPdbName);
                    }
                    File.Move(pdb, destinationPdbName);
                }
            }
            // these are the folders that get copied to src, rather than just to dependencies. 
            // things like images, scripts, and css that physically need to be in the source folder.
            string[] folderNamesToCopyToSrc = { "Scripts", "Views", "Content", "ViewModels", "Views", "Authentication", "ManualLogon","Helpers" };
            string[] folderNamesToCopyToTest = { "TestAssets" };

            // copy selected folders into src (Images, Scripts, CSS etc)
            DirectoryInfo srcInfo = new DirectoryInfo(Path.Combine(destinationDependencyFolder, "..\\src"));
            DirectoryInfo[] srcProjectFolders = srcInfo.GetDirectories("*.*", SearchOption.TopDirectoryOnly);
            foreach (var srcProjectFolder in srcProjectFolders.Where(s => s.Name.StartsWith("Web"))) {
                foreach (var srcFolderName in folderNamesToCopyToSrc) {
                    string destinationFolderName = Path.Combine(srcProjectFolder.FullName, srcFolderName);
                    if (srcFolderName != "ManualLogon") {
                        destinationFolderName = Path.Combine(destinationFolderName, moduleName);
                    }

                    var info = new DirectoryInfo(Path.Combine(folderToDeploy, srcFolderName));

                    dependencies = info.Exists ? info.GetDirectories("ThirdParty*").Union(info.GetDirectories("Web.*")).Select(i => i.Name).ToArray() : new string[0];

                    if (srcFolderName == "Views") {
                        // Views are different - Web.Presentation\Views\Shared => Web.Time\Views\Shared\Web.Presentation\
                        destinationFolderName = Path.Combine(srcProjectFolder.FullName, "Views\\Shared\\" + moduleName);
                        CopyDirectoryContentsRecursively(
                            Path.Combine(folderToDeploy, srcFolderName + "\\Shared"),
                            destinationFolderName,
                            dependencies);
                    } else {
                        CopyDirectoryContentsRecursively(Path.Combine(folderToDeploy, srcFolderName), destinationFolderName, dependencies);
                    }
                }
            }

            // copy selected folders into test (Images, Scripts, CSS etc)
            DirectoryInfo testInfo = new DirectoryInfo(Path.Combine(destinationDependencyFolder, "..\\Test"));
            DirectoryInfo[] testProjectFolders = testInfo.GetDirectories("*.*", SearchOption.TopDirectoryOnly);
            foreach (var testProjectFolder in testProjectFolders) {
                foreach (var testFolderName in folderNamesToCopyToTest) {
                    string destinationFolderName = Path.Combine(testProjectFolder.FullName, "Scripts");
                    destinationFolderName = Path.Combine(destinationFolderName, testFolderName);
                    CopyDirectoryContentsRecursively(Path.Combine(folderToDeploy, testFolderName), destinationFolderName, dependencies);
                }
            }
        }

        private void CopyDirectoryContentsRecursively(string sourceDirectory, string destinationFolder, string[] dependencies) {
            bool folderExists = false;
            if (!Directory.Exists(sourceDirectory) || dependencies.Any(sourceDirectory.EndsWith)) {
                return;
            }
            DirectoryInfo root = new DirectoryInfo(sourceDirectory);

            if (Directory.Exists(destinationFolder)) {
                DeleteFolder(new DirectoryInfo(destinationFolder));
            }

            string currentFolderName = destinationFolder.Split('\\').Last();
            FileInfo[] files = root.GetFiles("*.*");
            foreach (FileInfo fi in files) {
                if (fi.Extension.ToLowerInvariant() == ".map" || fi.Name.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)) {
                    if (fi.Name.EndsWith("Kendo.d.ts", StringComparison.OrdinalIgnoreCase)
                        || !fi.Name.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    // Ignore all .js.map and .ts files
                }
                if (fi.Extension.ToLowerInvariant() == ".bak") {
                    continue;
                    // Ignore all .bak files
                }
                string destinationFileName = Path.Combine(destinationFolder, fi.Name);
                if (fi.Extension.ToLowerInvariant() == ".tt" && fi.Name != "Aderant.Deployment.tt") {
                    continue;
                    // Ignore all .tt files except for the deployment info one that sets the build date and time
                }
                if (fi.Extension.ToLowerInvariant() != ".config" // ignore web.configs etc.
                    // and ignore anything that this zip has that was in it's own dependencies folder
                    && !dependencies.Any(
                        d => String.Compare(d, fi.Name, StringComparison.OrdinalIgnoreCase) == 0
                             || (d.Contains(currentFolderName) && d.EndsWith(fi.Name)
                                 )
                        )) {
                    // do not copy file in dependency txt file.
                    if (!folderExists && !Directory.Exists(destinationFolder)) {
                        Directory.CreateDirectory(destinationFolder); // lazy create so as not to make empty folders
                    }

                    folderExists = true;
                    try {
                        File.Copy(fi.FullName, destinationFileName, true);
                    } catch (Exception) {
                        FileAttributes attributes = File.GetAttributes(destinationFileName);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) {
                            File.SetAttributes(destinationFileName, attributes ^ FileAttributes.ReadOnly);
                        }
                        File.Copy(fi.FullName, destinationFileName, true);
                    }
                }
            }

            // Now recursively call for all the sub-directories under this directory.
            foreach (DirectoryInfo dirInfo in root.GetDirectories()) {
                CopyDirectoryContentsRecursively(dirInfo.FullName, Path.Combine(destinationFolder, dirInfo.Name), dependencies);
            }
        }

        /// <summary>
        /// Deletes a folder making files writable first.
        /// </summary>
        /// <param name="fileSystemInfo">The file system info.</param>
        private static void DeleteFolder(FileSystemInfo fileSystemInfo) {
            fileSystemInfo.Attributes = FileAttributes.Normal;
            var di = fileSystemInfo as DirectoryInfo;

            if (di != null) {
                foreach (var dirInfo in di.GetFileSystemInfos()) {
                    DeleteFolder(dirInfo);
                }
            }

            fileSystemInfo.Delete();
        }
    }
}