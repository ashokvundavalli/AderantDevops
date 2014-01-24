using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using DependencyAnalyzer;
using DependencyAnalyzer.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.DependencyAnalyzer {
    [TestClass]
    public class DependencyBuilderTests {
        #region Test Context

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext {
            get {
                return testContextInstance;
            }
            set {
                testContextInstance = value;
            }
        }

        #endregion

        [TestMethod]
        public void GetModulesReturnsDistinctModules() {
            DependencyBuilder builder = new DependencyBuilder(@"c:\tfs\ExpertSuite\Dev\Rules");
            IEnumerable<ExpertModule> modules = builder.GetAllModules();
            Assert.IsNotNull(modules);
            Assert.AreNotEqual(0, modules.Count());

            TestContext.WriteLine(string.Join(Environment.NewLine, modules.OrderBy(x => x.Name).Select(XName => XName.Name).ToArray()));
        }

        [TestMethod]
        public void GetModuleDependenciesReturnsCorrectDependencies() {
            DependencyBuilder builder = new DependencyBuilder(@"c:\tfs\ExpertSuite\Dev\Rules");
            IEnumerable<ModuleDependency> modulesDependencies = builder.GetModuleDependencies();
            Assert.IsNotNull(modulesDependencies);
            Assert.AreNotEqual(0, modulesDependencies.Count());

            modulesDependencies.GroupBy(x => x.Consumer).ToList().ForEach(dependencyGroup => {
                TestContext.WriteLine(dependencyGroup.Key.ToString());
                dependencyGroup.ToList().ForEach(x => TestContext.WriteLine(string.Format(" --> {0}", x.Provider)));
            });
        }

        [TestMethod]
        public void BuildMGraphDocumentReturnsCorrectDocument() {
            DependencyBuilder builder = new DependencyBuilder(@"c:\tfs\ExpertSuite\Dev\Rules");
            XDocument doc = builder.BuildMGraphDocument();
            Assert.IsNotNull(doc);
            TestContext.WriteLine(doc.ToString(SaveOptions.None));
        }

        [TestMethod]
        public void BuildDGMLDocumentReturnsCorrectDocument() {
            DependencyBuilder builder = new DependencyBuilder(@"c:\tfs\ExpertSuite\Dev\Rules");
            XDocument doc = builder.BuildDgmlDocument(true, false);
            Assert.IsNotNull(doc);
            TestContext.WriteLine(doc.ToString(SaveOptions.None));
        }

        [TestMethod]
        public void BuildDependencyTree() {
            DependencyBuilder builder = new DependencyBuilder(@"c:\tfs\ExpertSuite\Dev\Rules");
            IEnumerable<Build> tree = builder.GetTree(false);


            tree.ToList().ForEach(build => TestContext.WriteLine(string.Join(" - ", build.Modules.Select(x => x.Name).ToArray()) + Environment.NewLine + Environment.NewLine));

            TestContext.WriteLine(string.Format("Count in All Modules: {0}", builder.GetAllModules().Count()));
            TestContext.WriteLine(string.Format("Count in Tree: {0}", (from level in tree
                                                                       from item in level.Modules
                                                                       select item).Count()));

            var itemsNotInTree = builder.GetAllModules().Except(from level in tree
                                                                from item in level.Modules
                                                                select item);

            TestContext.WriteLine("");
            TestContext.WriteLine("Items not in tree:");
            itemsNotInTree.ToList().ForEach(item => TestContext.WriteLine(item.ToString()));
        }

        [TestMethod]
        public void When_C_Depends_On_B_Depends_On_A() {
            var provider = new FakeProvider();

            DependencyBuilder builder = new DependencyBuilder(provider);
            var builds = builder.GetTree(true).ToList();

            Assert.AreEqual(3, builds.Count());
            CollectionAssert.AllItemsAreUnique(builds);
        }

        [TestMethod]
        public void When_Bottom_Of_Stack_Is_Equivalent_Can_Be_Built_In_Parallel() {
            var provider = new ParallelFakeProvider();

            DependencyBuilder builder = new DependencyBuilder(provider);
            var builds = builder.GetTree(true).ToList();

            Assert.AreEqual(3, builds.Count());
            CollectionAssert.AllItemsAreUnique(builds);

            Build build = builds.Last();
            Assert.AreEqual(2, build.Modules.Count());
            Assert.AreEqual("C", build.Modules.First().Name);
            Assert.AreEqual("D", build.Modules.Last().Name);
        }

        [TestMethod]
        [ExpectedException(typeof(CircularDependencyException))]
        public void WhenDependencyChainIsCircularAnExceptionIsThrown() {
            var provider = new CircularReferenceProvider();

            DependencyBuilder builder = new DependencyBuilder(provider);
            builder.GetTree(true);
        }
    }

    internal class FakeProvider : IModuleProvider {
        public XDocument ProductManifest {
            get {
                return XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<ProductManifest Name='Expert' ExpertVersion='802'>
    <Modules>
        <Module Name='A' AssemblyVersion='1.8.0.0' />
        <Module Name='B' AssemblyVersion='1.8.0.0' />
        <Module Name='C' AssemblyVersion='1.8.0.0' />
    </Modules>
</ProductManifest>");
            }
        }

        public string ProductManifestPath {
            get;
            private set;
        }

        public string Branch {
            get {
                return @"Dev\Foo";
            }
        }

        public IEnumerable<ExpertModule> GetAll() {
            return new ExpertModule[] {
                new ExpertModule() {
                    Name = "A",
                    Branch = Branch
                },
                new ExpertModule() {
                    Name = "B",
                    Branch = Branch
                },
                new ExpertModule() {
                    Name = "C",
                    Branch = Branch
                }
            };
        }

        public bool TryGetDependencyManifest(string moduleName, out XDocument manifest) {
            if (moduleName == "A") {
                manifest = XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>        
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            if (moduleName == "B") {
                manifest = XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='A' />
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            if (moduleName == "C") {
                manifest = XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='A' />
        <ReferencedModule Name='B' />
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            manifest = null;
            return false;
        }

        public bool TryGetDependencyManifestPath(string moduleName, out string manifestPath) {
            throw new NotImplementedException();
        }

        public bool IsAvailable(string moduleName) {
            return true;
        }
    }

    internal class ParallelFakeProvider : IModuleProvider {
        public XDocument ProductManifest {
            get {
                return XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<ProductManifest Name='Expert' ExpertVersion='802'>
    <Modules>
        <Module Name='A' AssemblyVersion='1.8.0.0' />
        <Module Name='B' AssemblyVersion='1.8.0.0' />
        <Module Name='C' AssemblyVersion='1.8.0.0' />
        <Module Name='D' AssemblyVersion='1.8.0.0' />
    </Modules>
</ProductManifest>");
            }
        }

        public string ProductManifestPath {
            get;
            private set;
        }

        public string Branch {
            get {
                return @"Dev\Foo";
            }
        }

        public IEnumerable<ExpertModule> GetAll() {
            return new ExpertModule[] {
                new ExpertModule() {
                    Name = "A",
                    Branch = Branch
                },
                new ExpertModule() {
                    Name = "B",
                    Branch = Branch
                },
                new ExpertModule() {
                    Name = "C",
                    Branch = Branch
                },
                new ExpertModule() {
                    Name = "D",
                    Branch = Branch
                }
            };
        }

        public bool TryGetDependencyManifest(string moduleName, out XDocument manifest) {
            if (moduleName == "A") {
                manifest = XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>        
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            if (moduleName == "B") {
                manifest = XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='A' />
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            if (moduleName == "C") {
                manifest = XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='A' />
        <ReferencedModule Name='B' />
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            if (moduleName == "D") {
                manifest = XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='A' />
        <ReferencedModule Name='B' />
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            manifest = null;
            return false;
        }

        public bool TryGetDependencyManifestPath(string moduleName, out string manifestPath) {
            throw new NotImplementedException();
        }

        public bool IsAvailable(string moduleName) {
            return true;
        }
    }

    internal class CircularReferenceProvider : IModuleProvider {
        public XDocument ProductManifest {
            get {
                return XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<ProductManifest Name='Expert' ExpertVersion='802'>
    <Modules>
        <Module Name='A' AssemblyVersion='1.8.0.0' />
        <Module Name='B' AssemblyVersion='1.8.0.0' />
    </Modules>
</ProductManifest>");
            }
        }

        public string ProductManifestPath {
            get;
            private set;
        }

        public string Branch {
            get {
                return @"Dev\Foo";
            }
        }

        public IEnumerable<ExpertModule> GetAll() {
            return new ExpertModule[] {
                new ExpertModule() {
                    Name = "A",
                    Branch = Branch
                },
                new ExpertModule() {
                    Name = "B",
                    Branch = Branch
                },
            };
        }

        public bool TryGetDependencyManifest(string moduleName, out XDocument manifest) {
            if (moduleName == "A") {
                manifest = XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>        
       <ReferencedModule Name='B' />
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            if (moduleName == "B") {
                manifest = XDocument.Parse(@"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='A' />
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            manifest = null;
            return false;
        }

        public bool TryGetDependencyManifestPath(string moduleName, out string manifestPath) {
            throw new NotImplementedException();
        }

        public bool IsAvailable(string moduleName) {
            return true;
        }
    }
}