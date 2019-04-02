using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class ZipBuildScripts : Task {
        static ZipBuildScripts() {
            DotNetQuriks.ZipFileUseForwardSlash();
        }

        [Required]
        public string TargetDirectory { get; set; }

        [Required]
        public string BuildAssemblyLocation { get; set; }

        public override bool Execute() {
            ErrorUtilities.IsNotNull(TargetDirectory, nameof(TargetDirectory));
            ErrorUtilities.IsNotNull(BuildAssemblyLocation, nameof(BuildAssemblyLocation));

            try {
                using (var client = new WebClient()) {
                    client.UseDefaultCredentials = true;

                    var sourcePath = "http://tfs.ap.aderant.com:8080/tfs/ADERANT/44f228f7-b636-4bd3-99ee-eb2f1570d768/_api/_versioncontrol/itemContentZipped?repositoryId=a0f7d5b4-270c-450d-9f90-d8b8e31d4a2e&path=%2FSrc%2FBuild&version=GBmaster&__v=5";

                    var targetPath = Path.Combine(TargetDirectory, "test.zip");
                    client.DownloadFile(sourcePath, targetPath);

                    ZipFile.ExtractToDirectory(targetPath,TargetDirectory);

                    var scriptZipPath = Path.Combine(TargetDirectory, "BuildScripts.zip");
                    if (File.Exists(scriptZipPath)) {
                        File.Delete(scriptZipPath);
                    }

                    var buildFolder = Path.Combine(TargetDirectory, "Build");
                    if (Directory.Exists(buildFolder)) {
                        var filesToRemove = Directory.GetFiles(buildFolder, "Aderant.CodeSigning*").ToList();
                        foreach (var file in filesToRemove) {
                            if (File.Exists(file)) {
                                File.Delete(file);
                            }
                        }

                        if (File.Exists(BuildAssemblyLocation)) {
                            var buildToolsFolder = Path.Combine(buildFolder, "Build.Tools");
                            Directory.CreateDirectory(buildToolsFolder);
                            File.Copy(BuildAssemblyLocation, Path.Combine(buildToolsFolder, Path.GetFileName(BuildAssemblyLocation)));
                        } else {
                            Log.LogError("Aderant.Build.dll was not found at {0}", BuildAssemblyLocation);
                            return false;
                        }

                        ZipFile.CreateFromDirectory(buildFolder, scriptZipPath);
                        Directory.Delete(buildFolder, true);
                    }

                    if (File.Exists(targetPath)) {
                        File.Delete(targetPath);
                    }

                    return true;
                }
            }
            catch (Exception e) {
                Log.LogError(e.Message, e);
            }

            return Log.HasLoggedErrors;
        }
    }
}
