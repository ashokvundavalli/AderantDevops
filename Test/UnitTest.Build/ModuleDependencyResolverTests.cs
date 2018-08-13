using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.DependencyResolver.Resolvers;
using Aderant.Build.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build {
    [TestClass]
    public class ModuleDependencyResolverTests {
        [TestMethod]
        [Ignore]
        public void ModuleDependencyResolver_can_load_dependency_manifest() {
            var mock = new Mock<IFileSystem2>();

            var resolver = new ExpertModuleResolver(mock.Object);

            resolver.ManifestFinder = s => new MemoryStream(Encoding.UTF8.GetBytes(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='Module0' AssemblyVersion='1.8.0.0' />    
    </ReferencedModules>
</DependencyManifest>"));

            var requirements = ((IDependencyResolver)resolver).GetDependencyRequirements(null, new ExpertModule("Foo"));

            var requirement = requirements.First();

            Assert.AreEqual("Module0", requirement.Name);
        }

        [TestMethod]
        public void GetDependenciesRequiredForBuild_all_dependencies_being_built() {
            List<ExpertModule> modules = new List<ExpertModule>();
            modules.Add(new ExpertModule("Foo"));
            modules.Add(new ExpertModule("Bar"));

            List<ModuleDependency> dependencies = new List<ModuleDependency>();
            dependencies.Add(new ModuleDependency {
                Consumer = modules[0],
                Provider = modules[0]
            });

            dependencies.Add(new ModuleDependency {
                Consumer = modules[1],
                Provider = modules[1]
            });

            dependencies.Add(new ModuleDependency {
                Consumer = modules[0],
                Provider = modules[1]
            });

            ICollection<ExpertModule> build = ResolverRequest.GetDependenciesRequiredForBuild(modules, dependencies, modules);

            Assert.AreEqual(0, build.Count, "Did not expect any modules as all modules and their dependencies are being built");
        }

        [TestMethod]
        public void GetDependenciesRequiredForBuild() {
            List<ExpertModule> modules = new List<ExpertModule>();
            modules.Add(new ExpertModule("Foo"));
            modules.Add(new ExpertModule("Bar"));

            List<ModuleDependency> dependencies = new List<ModuleDependency>();
            dependencies.Add(new ModuleDependency {
                Consumer = modules[0],
                Provider = modules[0]
            });

            dependencies.Add(new ModuleDependency {
                Consumer = modules[1],
                Provider = modules[1]
            });

            dependencies.Add(new ModuleDependency {
                Consumer = modules[0],
                Provider = modules[1]
            });

            ICollection<ExpertModule> build = ResolverRequest.GetDependenciesRequiredForBuild(modules, dependencies, new List<ExpertModule>() { modules[0] });

            Assert.AreEqual(1, build.Count);
        }

        [TestMethod]
        [Ignore]
        public void ModuleDependencyResolver_gets_subset_from_drop() {
            var dependencyManifest1 = new DependencyManifest("Module1", XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='Module0' AssemblyVersion='1.8.0.0' />    
    </ReferencedModules>
</DependencyManifest>"));

            var dependencyManifest2 = new DependencyManifest("Module2", XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='Module1' AssemblyVersion='1.8.0.0' />    
    </ReferencedModules>
</DependencyManifest>"));

            var expertManifest = new TestExpertManifest(@"<?xml version='1.0' encoding='utf-8'?>
<ProductManifest Name='Expert' ExpertVersion='802'>
    <Modules>
        <Module Name='Module0' AssemblyVersion='1.8.0.0' />
        <Module Name='Module1' AssemblyVersion='1.8.0.0' />
        <Module Name='Module2' AssemblyVersion='1.8.0.0' />
   </Modules>
</ProductManifest>", new[] { dependencyManifest1, dependencyManifest2 });

            Mock<IFileSystem2> fs = new Mock<IFileSystem2>();
            var buildFolders = new Mock<FolderDependencySystem>(fs.Object);

            buildFolders.Setup(s => s.GetBinariesPath(It.IsAny<string>(), It.IsAny<IDependencyRequirement>())).Returns("1.0.0.0\\Bin\\Module");

            IEnumerable<IDependencyRequirement> requirements = expertManifest.GetAll().Select(DependencyRequirement.Create);

            ExpertModuleResolver resolver = new ExpertModuleResolver(fs.Object);
            resolver.FolderDependencySystem = buildFolders.Object;
            resolver.AddDependencySource("Foo", ExpertModuleResolver.DropLocation);

            ResolverRequest request = new ResolverRequest(new NullLogger(), (IFileSystem2)null, expertManifest.GetAll().ToArray());

            ((IDependencyResolver)resolver).Resolve(request, requirements);

            var items = request.GetResolvedRequirements().ToList();

            Assert.AreEqual(3, items.Count);
            Assert.AreEqual("Module0", items[0].Name);
            Assert.AreEqual("Module1", items[1].Name);
            Assert.AreEqual("Module2", items[2].Name);
        }
    }

    internal class TestExpertManifest : ExpertManifest {
        public TestExpertManifest(string manifest, DependencyManifest[] manifests)
            : base(XDocument.Parse(manifest)) {
            DependencyManifests = manifests;
        }
    }
}
