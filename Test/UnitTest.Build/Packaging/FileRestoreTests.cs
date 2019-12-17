using System.Collections.Generic;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class FileRestoreTests {
        [TestMethod]
        public void RestoreWithMatchingOutputPath() {
            string path = @"..\..\Bin\Module\";
            string restorePath = FileRestore.CalculateRestorePath(@"..\..\Bin\Module\Fancy.Assembly.dll", ref path);

            Assert.AreEqual("Fancy.Assembly.dll", restorePath);
        }

        /// <summary>
        /// Scatter files are defined as files that are written to locations other than the output path.
        /// This is typically done by projects that do not produce and intermediate assembly or by projects that produce and
        /// intermediate
        /// and also have an additional packaging process like web projects.
        /// </summary>
        [TestMethod]
        public void Scatter_files_are_restored() {
            List<LocalArtifactFile> localArtifactFiles = new List<LocalArtifactFile>();
            localArtifactFiles.Add(new LocalArtifactFile("C:\\temp\\web.core.zip"));

            var service = new Mock<IBuildPipelineService>();
            var fs = new Mock<IFileSystem>();
            fs.Setup(s => s.FileExists(@"C:\MyApp\Src\Web.Core\Web.Core.csproj")).Returns(true);

            var restore = new FileRestore(localArtifactFiles, service.Object, fs.Object, new NullLogger());

            var stateFile = new BuildStateFile();
            string[] fileWrites = new[] {
                "..\\..\\Bin\\Module\\Web.Core.SourceManifest.xml",
                "..\\..\\Bin\\Module\\Web.Core.zip",
                "..\\..\\packages\\javascript\\ThirdParty.Bootstrap\\lib\\accordion.less"
            };
            stateFile.Outputs = new ProjectTreeOutputSnapshot();
            stateFile.Outputs["Src\\Web.Core\\Web.Core.csproj"] = new ProjectOutputSnapshot {
                FilesWritten = fileWrites,
                Directory = "MyApp",
                OutputPath = "bin\\"
            };

            IList<PathSpec> restoreOperations = restore.BuildRestoreOperations(@"C:\MyApp", "MyApp", stateFile);

            Assert.AreEqual(2, restoreOperations.Count);
            Assert.AreEqual("C:\\MyApp\\Src\\Web.Core\\bin\\Web.Core.zip", restoreOperations[0].Destination);
            Assert.AreEqual("C:\\MyApp\\Bin\\Module\\Web.Core.zip", restoreOperations[1].Destination);
        }
    }
}