using System;
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
                    new TrackedProject {
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
}