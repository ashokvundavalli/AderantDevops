using System.Collections.Generic;
using Aderant.Build;
using Aderant.Build.PipelineService;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class GatherProjectsToBuildTests {

        private Mock<IBuildPipelineService> pipelineServiceMock;

        [TestInitialize]
        public void TestInitialize() {
            this.pipelineServiceMock = new Moq.Mock<IBuildPipelineService>();

            pipelineServiceMock.Setup(s => s.GetContext()).Returns(new BuildOperationContext() {
                Switches = new BuildSwitches() {
                    RestrictToProvidedPaths = false
                }
            });
        }

        [TestMethod]
        public void When_ceiling_and_input_match_expand_is_false() {
            var fileSystem = new Moq.Mock<IFileSystem>();
            fileSystem.Setup(s => s.DirectoryExists(Moq.It.Is<string>(str => str.EndsWith(".git")))).Returns(true).Verifiable();

            var expandTree = new GatherProjectsToBuild() {
                Service = pipelineServiceMock.Object
            }.ExpandTree(new HashSet<string>() { @"C:\Temp\Foo" }, fileSystem.Object);

            Assert.IsFalse(expandTree);

            fileSystem.Verify();
        }


        [TestMethod]
        public void When_ceiling_and_input_do_not_match_expand_is_true() {
            var fileSystem = new Moq.Mock<IFileSystem>();
            fileSystem.Setup(s => s.DirectoryExists(Moq.It.Is<string>(str => str.EndsWith(".git")))).Returns(false).Verifiable();

            var expandTree = new GatherProjectsToBuild() {
                Service = pipelineServiceMock.Object
            }.ExpandTree(new HashSet<string>() { @"C:\Temp\Foo" }, fileSystem.Object);

            Assert.IsTrue(expandTree);

            fileSystem.Verify();
        }

        [TestMethod]
        public void When_ceiling_and_input_do_not_match_expand_is_true_with_multiple_paths() {
            var fileSystem = new Moq.Mock<IFileSystem>();

            var expandTree = new GatherProjectsToBuild() {
                Service = pipelineServiceMock.Object
            }.ExpandTree(new HashSet<string>() { @"C:\Temp\Foo", @"C:\Temp\Foo1" }, fileSystem.Object);

            Assert.IsTrue(expandTree);
        }

    }
}