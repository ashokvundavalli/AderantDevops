using System;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;

namespace GetWebProjectDependencies {
    internal class WebPackageExtract {
        private const string FolderNameToExtract = "PackageTmp";

        public void ExtractWebPackage(string sourceFolder, string dependencyFolderName) {
            if (!Directory.Exists(sourceFolder)) {
                throw new DirectoryNotFoundException(sourceFolder);
            }
            if (!Directory.Exists(dependencyFolderName)) {
                Directory.CreateDirectory(dependencyFolderName);
            }

            Console.WriteLine(string.Format("Getting Web Dependencies from {0} to {1}", sourceFolder, dependencyFolderName));

            // these are the folders that get copied to src, rather than just to dependencies. 
            // things like images, scripts, and css that physiically need to be in the source folder.
            string[] folderNamesToCopyToSrc = new[] { "Scripts", "Views", "Content", "ViewModels", "Views", "Authentication", "ManualLogon" };
            string[] folderNamesToCopyToTest = new[] { "TestAssets" };


            string zipFileName = sourceFolder.Split('\\').First(p => p.StartsWith("Web.")) + ".zip"; // e.g. web.workflow.zip
            string moduleName = zipFileName.Split('\\').Last().Replace(".zip", ""); // e.g. web.workflow
            FastZip fz = new FastZip();

            // extract zip to a temporary folder.  32 digits no special chars , 00000000000000000000000000000000
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

            // Read dependencies file of thid dependency - 
            // we look for a Dependencies.txt file which should contain all files from the dependencies folder of the source project
            // we don't want those files (i.e. the dependencies of this dependency) 
            FileInfo dependenciesFile = directoryInfo.GetFiles("Dependencies.txt", SearchOption.AllDirectories).FirstOrDefault();
            string[] dependencies = new string[0];
            if (dependenciesFile != null) {
                dependencies = File.ReadAllLines(dependenciesFile.FullName);
            }

            // copy entire zip into dependencies folder
            string localDependencyFolderName = Path.Combine(dependencyFolderName, moduleName);
            CopyDirectoryContentsRecursively(folderToDeploy, localDependencyFolderName , dependencies);

            // move dll files to dependency folder, not this sub folder
            var dlls = Directory.GetFiles(Path.Combine(localDependencyFolderName, "bin"), "web*.dll");
            foreach (string dll in dlls) {
                string destinationDllName = Path.Combine(dependencyFolderName, Path.GetFileName(dll));
                if (File.Exists(destinationDllName)) {
                    File.Delete(destinationDllName);
                }
                File.Move(dll, destinationDllName);
            }
            

            // copy selected folders into src (Images, Scripts, CSS etc)
            DirectoryInfo srcInfo = new DirectoryInfo(Path.Combine(dependencyFolderName, "..\\src"));
            DirectoryInfo[] srcProjectFolders = srcInfo.GetDirectories("*.*", SearchOption.TopDirectoryOnly);
            foreach (var srcProjectFolder in srcProjectFolders) {
                foreach (var srcFolderName in folderNamesToCopyToSrc) {
                    string destinationFolderName = Path.Combine(srcProjectFolder.FullName, srcFolderName);
                    if (srcFolderName != "ManualLogon") {
                        destinationFolderName = Path.Combine(destinationFolderName, moduleName);
                    }

                    if (srcFolderName == "Views") {
                        // Views are different - Web.Presentation\Views\Shared => Web.Time\Views\Shared\Web.Presentation\
                        destinationFolderName = Path.Combine(srcProjectFolder.FullName, "Views\\Shared\\" + moduleName);
                        CopyDirectoryContentsRecursively(Path.Combine(folderToDeploy, srcFolderName + "\\Shared"), destinationFolderName, dependencies);
                    } else {
                        CopyDirectoryContentsRecursively(Path.Combine(folderToDeploy, srcFolderName), destinationFolderName, dependencies);
                    }
                }
            }

            // copy selected folders into test (Images, Scripts, CSS etc)
            DirectoryInfo testInfo = new DirectoryInfo(Path.Combine(dependencyFolderName, "..\\Test"));
            DirectoryInfo[] testProjectFolders = testInfo.GetDirectories("*.*", SearchOption.TopDirectoryOnly);
            foreach (var testProjectFolder in testProjectFolders) {
                foreach (var testFolderName in folderNamesToCopyToTest) {
                    string destinationFolderName = Path.Combine(testProjectFolder.FullName, "Scripts");
                    destinationFolderName = Path.Combine(destinationFolderName, testFolderName);
                    CopyDirectoryContentsRecursively(Path.Combine(folderToDeploy, testFolderName), destinationFolderName, dependencies);
                }
            }

            DeleteFolder(new DirectoryInfo(temporaryFolderWithZip)); // clean out zip folder
        }

        private void CopyDirectoryContentsRecursively(string sourceDirectory, string destinationFolder, string[] dependencies) {
            bool folderExists = false;
            if (!Directory.Exists(sourceDirectory)) {
                return; 
            }
            DirectoryInfo root = new DirectoryInfo(sourceDirectory);

            if (Directory.Exists(destinationFolder)) {
                DeleteFolder(new DirectoryInfo(destinationFolder));
            }

            string currentFolderName = destinationFolder.Split('\\').Last();
            FileInfo[] files = root.GetFiles("*.*");
            foreach (FileInfo fi in files) {
                string destinationFileName = Path.Combine(destinationFolder, fi.Name);
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

            // Now recursively call for all the subdirectories under this directory.
            foreach (DirectoryInfo dirInfo in root.GetDirectories()) {
                CopyDirectoryContentsRecursively(dirInfo.FullName, Path.Combine(destinationFolder, dirInfo.Name), dependencies);
            }
        }

        /// <summary>
        /// Deletes a folder making files writable first.
        /// </summary>
        /// <param name="fsi">The fsi.</param>
        private static void DeleteFolder(FileSystemInfo fsi) {
            fsi.Attributes = FileAttributes.Normal;
            var di = fsi as DirectoryInfo;

            if (di != null) {
                foreach (var dirInfo in di.GetFileSystemInfos()) {
                    DeleteFolder(dirInfo);
                }
            }

            fsi.Delete();
        }
    }
}

