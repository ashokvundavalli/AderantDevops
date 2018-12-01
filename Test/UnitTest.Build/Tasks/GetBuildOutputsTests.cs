using System;
using Aderant.Build;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class GetBuildOutputsTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void GetBuildOutputs_returns_tracked_projects() {
            var mock = new Mock<IBuildPipelineService>();
            mock.Setup(s => s.GetTrackedProjects()).Returns(
                new[] {
                    new OnDiskProjectInfo {
                        FullPath = TestContext.DeploymentDirectory,
                        OutputPath = @"..\..\bin",
                        ProjectGuid = Guid.NewGuid(),
                        SolutionRoot = "abc"
                    }
                });

            var output = new GetBuildOutputs();
            output.Service = mock.Object;
            output.ExecuteTask();

            ITaskItem[] outputTrackedProjects = output.TrackedProjects;

            Assert.AreEqual(1, outputTrackedProjects.Length);
        }
    }

    [TestClass]
    public class CreateDependencyLinkTests {

        [TestMethod]
        public void Link_considers_items_in_a_subdirectory() {
            var createLinks = new CreateDependencyLinks();
            createLinks.SolutionRoot = @"C:\Source\1";

            var projectWithPath = new ProjectOutputSnapshotWithFullPath(new ProjectOutputSnapshot());
            projectWithPath.ProjectFileAbsolutePath = @"C:\Source\2\Project2\Project2.csproj";
            projectWithPath.OutputPath = @"..\..\Bin\Module";
            projectWithPath.FilesWritten = new[] { "..\\..\\Bin\\Module\\Subdir1\\File.xml" };

            var dependentFile = @"C:\Source\1\packages\SomePackage\lib\Subdir1\File.xml";

            string locationForSymlink;
            string targetForSymlink;

            createLinks.CalculateSymlinkTarget(projectWithPath, dependentFile, out locationForSymlink, out targetForSymlink);

            Assert.IsTrue(targetForSymlink.EndsWith(@"Subdir1\File.xml"));
        }
    }

}