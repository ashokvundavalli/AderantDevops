using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class ModuleDependencyResolverTests {
        [TestMethod]
        public void GetDependenciesRequiredForBuild_all_dependencies_being_built() {
            var resolver = new ModuleDependencyResolver(null, "", new FakeLogger());

            List<ExpertModule> modules = new List<ExpertModule>();
            modules.Add(new ExpertModule {Name = "Foo"});
            modules.Add(new ExpertModule {Name = "Bar"});

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

            ICollection<ExpertModule> build = resolver.GetDependenciesRequiredForBuild(modules, dependencies, new string[] {"Foo", "Bar"});

            Assert.AreEqual(0, build.Count, "Did not expect any modules as all modules and their dependencies are being built");
        }

        [TestMethod]
        public void GetDependenciesRequiredForBuild() {
            var resolver = new ModuleDependencyResolver(null, "", new FakeLogger());

            List<ExpertModule> modules = new List<ExpertModule>();
            modules.Add(new ExpertModule {Name = "Foo"});
            modules.Add(new ExpertModule {Name = "Bar"});

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

            ICollection<ExpertModule> build = resolver.GetDependenciesRequiredForBuild(modules, dependencies, new string[] {"Foo"});

            Assert.AreEqual(1, build.Count);
        }


        [TestMethod]
        public async Task ModuleDependencyResolver_gets_subset_from_drop() {
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
</ProductManifest>", new[] {dependencyManifest1, dependencyManifest2});


            var resolver = new ModuleDependencyResolver(expertManifest, "", new FakeLogger());
            resolver.SetModulesInBuild(new[] {"Module1"});

            await resolver.Resolve(string.Empty);

            Assert.AreEqual(1, expertManifest.modulesFetched.Count);
            Assert.AreEqual("Module0", expertManifest.modulesFetched[0].Name);
        }
    }

    internal class TestExpertManifest : ExpertManifest {
        internal IList<ExpertModule> modulesFetched = new List<ExpertModule>();

        public TestExpertManifest(string manifest, DependencyManifest[] manifests) : base(XDocument.Parse(manifest)) {
            base.DependencyManifests = manifests;
        }

        public override string GetPathToBinaries(ExpertModule expertModule, string dropPath) {
            modulesFetched.Add(expertModule);

            return null;
        }
    }
}