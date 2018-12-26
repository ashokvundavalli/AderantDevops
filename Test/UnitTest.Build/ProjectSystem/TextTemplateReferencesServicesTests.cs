using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aderant.Build;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.References;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.ProjectSystem {
    [TestClass]
    public class TextTemplateReferencesServicesTests {

        [TestMethod]
        public void Alias_map_is_used_for_resolution_process() {
            var fileSystemMock = new Mock<IFileSystem>();
            var treeMock = new Mock<IProjectTree>();
            var projectMock = new Mock<ConfiguredProject>(treeMock.Object);

            treeMock.Setup(s => s.LoadedConfiguredProjects).Returns(new[] { projectMock.Object });

            projectMock.Setup(s => s.GetItems(It.IsAny<string>())).Returns(new List<ProjectItem>());
            projectMock.Setup(s => s.FullPath).Returns("abc\\def.csproj");

            var service = new TextTemplateReferencesServices(fileSystemMock.Object);
            service.ConfiguredProject = projectMock.Object;

            var unresolved = new IUnresolvedAssemblyReference[] {
                new UnresolvedAssemblyReference(
                    null,
                    new UnresolvedAssemblyReferenceMoniker(
                        new AssemblyName("Bar"),
                        null) { IsFromTextTemplate = true })
            };

            service.UnresolvedReferences = unresolved.ToList();

            var aliasMap = new Dictionary<string, string> { { "Bar", "abc\\def.csproj"}} ;

            var resolvedReferences = service.GetResolvedReferences(
                unresolved,
                aliasMap);

            Assert.IsNotNull(resolvedReferences);
            Assert.AreEqual(1, resolvedReferences.Count);
        }
    }
}