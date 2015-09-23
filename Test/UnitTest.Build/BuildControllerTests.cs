using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Aderant.Build;
using Aderant.Build.Tasks;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using BuildPageSummary = Aderant.Build.BuildSummary;

namespace UnitTest.Build {
    [TestClass]
    public class BuildControllerTests {
        private TeamFoundationMock teamFoundationMock;

        [TestInitialize]
        public void TestInitialize() {
            teamFoundationMock = new TeamFoundationMock();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Create_build_throws_exception_if_no_multi_agent_controllers_are_found() {
            teamFoundationMock.SetAgents(new IBuildAgent[0]);

            var controller = new BuildDetailPublisher("", "ExpertSuite");
            controller.TeamFoundationServiceFactory = teamFoundationMock;

            controller.CreateBuildDefinition(new ExpertBuildConfiguration("Dev\\MyBranch") { ModuleName = "Foo" });
        }

        [TestMethod]
        public void Workspace_is_added_to_definition() {
            teamFoundationMock.SetAgents(new IBuildAgent[] { new Mock<IBuildAgent>().Object, new Mock<IBuildAgent>().Object });

            var controller = new BuildDetailPublisher("", "ExpertSuite");
            controller.TeamFoundationServiceFactory = teamFoundationMock;
            controller.BuildProcessTemplate = new Mock<IBuildProcessTemplate>().Object;

            var definition = controller.CreateBuildDefinition(new ExpertBuildConfiguration("Dev\\MyBranch") { ModuleName = "Foo" });

            Assert.IsNotNull(definition);
            Assert.AreEqual(2, teamFoundationMock.WorkspaceMappings.Length);
        }

        [TestMethod]
        public void Definition_properties_are_configured() {
            teamFoundationMock.SetAgents(new IBuildAgent[] { new Mock<IBuildAgent>().Object, new Mock<IBuildAgent>().Object });

            var controller = new BuildDetailPublisher("", "ExpertSuite");
            controller.TeamFoundationServiceFactory = teamFoundationMock;
            controller.BuildProcessTemplate = new Mock<IBuildProcessTemplate>().Object;

            var definition = controller.CreateBuildDefinition(new ExpertBuildConfiguration("Dev\\MyBranch") {
                ModuleName = "Foo",
                DropLocation = @"\\na.aderant.com\ExpertSuite\"
            });

            Assert.IsNotNull(definition);
            Assert.AreEqual(ContinuousIntegrationType.Individual, definition.ContinuousIntegrationType);
            Assert.AreEqual(@"\\na.aderant.com\ExpertSuite\", definition.DefaultDropLocation);
            Assert.IsNotNull(definition.BuildController);
        }

        [TestMethod]
        public void Build_instance_drop_location() {
            teamFoundationMock.SetAgents(new IBuildAgent[] { new Mock<IBuildAgent>().Object, new Mock<IBuildAgent>().Object });

            var controller = new BuildDetailPublisher("", "ExpertSuite");
            controller.TeamFoundationServiceFactory = teamFoundationMock;
            controller.BuildProcessTemplate = new Mock<IBuildProcessTemplate>().Object;

            ExpertBuildConfiguration buildConfiguration = new ExpertBuildConfiguration("Dev\\MyBranch") {
                ModuleName = "Foo",
                DropLocation = @"\\na.aderant.com\ExpertSuite\"
            };

            var definition = controller.CreateBuildDefinition(buildConfiguration);

            ExpertBuildDetail detail = new ExpertBuildDetail("99.99.99.99", "1.0.0.0", buildConfiguration);

            Assert.AreEqual(@"\\na.aderant.com\ExpertSuite\Dev\MyBranch\Foo\99.99.99.99\1.0.0.0", detail.DropLocation);
        }



        //[TestMethod]
        //public void Integration() {
        //    BuildDetailPublisher publisher = new BuildDetailPublisher("http://tfs:8080/tfs/aderant", "ExpertSuite");

        //    var config = new ExpertBuildConfiguration(@"Releases\803x") {
        //        ModuleName = "Applications.ExpertAssistant",
        //        DropLocation = @"\\na.aderant.com\ExpertSuite"
        //    };

        //    var definition = publisher.CreateBuildDefinition(config);

        //    var detail = new ExpertBuildDetail("1.8.0.0", "99.99.99.99", config) {
        //        CompletedSuccessfully = true,
        //        BuildSummary = new BuildPageSummary { Section = "MySection", Message = "MyMessage" }
        //    };

        //    publisher.CreateNewBuild(definition, detail);
        //}
    }



    internal class TeamFoundationMock : IServiceProvider {
        private IBuildController controller;
        private IBuildServer buildServer;
        private IWorkspaceTemplate workspace;
        private IBuildDefinition definition;

        private List<Tuple<string, string, WorkspaceMappingType>> workspaceItems;

        private IBuildAgent[] agents;

        public TeamFoundationMock() {
            var workspaceMock = new Mock<IWorkspaceTemplate>();

            workspaceItems = new List<Tuple<string, string, WorkspaceMappingType>>();

            workspaceMock
                .Setup(m => m.AddMapping(It.IsAny<string>(), It.IsAny<string>(), WorkspaceMappingType.Map))
                .Callback<string, string, WorkspaceMappingType>((serverItem, localItem, mappingType) => { workspaceItems.Add(Tuple.Create(serverItem, localItem, mappingType)); });

            workspace = workspaceMock.Object;

            var mock = new Mock<IBuildDefinition>();
            mock.Setup(s => s.Workspace).Returns(workspace);
            mock.SetupAllProperties(); // Record property sets

            definition = mock.Object;
        }

        public object[] WorkspaceMappings {
            get {
                return
                    workspaceItems.ToArray();
            }
        }

        public IBuildServer BuildServer {
            get {
                if (buildServer == null) {
                    CreateBuildServer();
                }
                return buildServer;
            }
        }

        public IBuildController BuildController {
            get {
                if (controller == null) {
                    CreateBuildServer();
                }
                return controller;
            }
        }

        public object GetService(Type serviceType) {
            if (serviceType == typeof(IBuildServer)) {
                return BuildServer;
            }

            if (serviceType == typeof(IBuildController)) {
                return BuildController;
            }

            throw new NotImplementedException("No implementation for type " + serviceType);
        }

        public void SetAgents(IBuildAgent[] buildAgents) {
            agents = buildAgents;

            CreateBuildController(agents);
        }

        private void CreateBuildController(IBuildAgent[] buildAgents) {
            var mockBuildController = new Mock<IBuildController>();
            mockBuildController.Setup(s => s.Agents).Returns(new ReadOnlyCollection<IBuildAgent>(buildAgents));

            controller = mockBuildController.Object;
        }

        private void CreateBuildServer() {
            var buildServerMock = new Mock<IBuildServer>();
            buildServerMock
                .Setup(s => s.QueryBuildControllers())
                .Returns(new[] {
                    controller
                });

            buildServerMock.Setup(s => s.CreateBuildDefinition(It.IsAny<string>())).Returns(definition);

            buildServer = buildServerMock.Object;
        }
    }
}