using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class DependencyBuilderTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void GetModulesReturnsDistinctModules() {
            var provider = new FakeProvider();
            DependencyBuilder builder = new DependencyBuilder(provider);
            IEnumerable<ExpertModule> modules = builder.GetAllModules();
            Assert.IsNotNull(modules);
            Assert.AreNotEqual(0, modules.Count());

            TestContext.WriteLine(string.Join(Environment.NewLine, modules.OrderBy(x => x.Name).Select(XName => XName.Name).ToArray()));
        }

        [TestMethod]
        [Ignore]
        public void GetModuleDependenciesReturnsCorrectDependencies() {
            var provider = new FakeProvider();
            DependencyBuilder builder = new DependencyBuilder(provider);
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
            var provider = new FakeProvider();
            DependencyBuilder builder = new DependencyBuilder(provider);
            XDocument doc = builder.BuildMGraphDocument();
            Assert.IsNotNull(doc);
            TestContext.WriteLine(doc.ToString(SaveOptions.None));
        }

        [TestMethod]
        [Ignore]
        public void BuildDGMLDocumentReturnsCorrectDocument() {
            var provider = new FakeProvider();
            DependencyBuilder builder = new DependencyBuilder(provider);
            XDocument doc = builder.BuildDgmlDocument(true, false);
            Assert.IsNotNull(doc);
            TestContext.WriteLine(doc.ToString(SaveOptions.None));
        }

        [TestMethod]
        [Ignore]
        public void BuildDependencyTree() {
            var provider = new FakeProvider();
            DependencyBuilder builder = new DependencyBuilder(provider);
            IEnumerable<Aderant.Build.Build> tree = builder.GetTree(false);


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
        [Ignore]
        public void When_C_Depends_On_B_Depends_On_A() {
            var provider = new FakeProvider();

            DependencyBuilder builder = new DependencyBuilder(provider);
            var builds = builder.GetTree(true).ToList();

            Assert.AreEqual(3, builds.Count);
            CollectionAssert.AllItemsAreUnique(builds);
        }

        [TestMethod]
        [Ignore]
        public void When_Bottom_Of_Stack_Is_Equivalent_Can_Be_Built_In_Parallel() {
            var provider = new ParallelFakeProvider();

            DependencyBuilder builder = new DependencyBuilder(provider);
            var builds = builder.GetTree(true).ToList();

            Assert.AreEqual(3, builds.Count);
            CollectionAssert.AllItemsAreUnique(builds);

            var build = builds.Last();
            Assert.AreEqual(2, build.Modules.Count());
            Assert.AreEqual("C", build.Modules.First().Name);
            Assert.AreEqual("D", build.Modules.Last().Name);
        }

        [TestMethod]
        [ExpectedException(typeof(CircularDependencyException))]
        [Ignore]
        public void WhenDependencyChainIsCircularAnExceptionIsThrown() {
            var provider = new CircularReferenceProvider();

            DependencyBuilder builder = new DependencyBuilder(provider);
            builder.GetTree(true);
        }
    }

    internal class FakeProvider : TestModuleProviderBase {

        public override string Branch {
            get { return @"Dev\Foo"; }
        }

        public override IEnumerable<ExpertModule> GetAll() {
            return new ExpertModule[] {
                new ExpertModule("A") {
                    Branch = Branch
                },
                new ExpertModule("B") {
                    Branch = Branch
                },
                new ExpertModule("C") {
                    Branch = Branch
                }
            };
        }

        public override bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {
            if (moduleName == "A") {
                manifest = DependencyManifest.Parse("A", @"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            if (moduleName == "B") {
                manifest = DependencyManifest.Parse("B", @"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='A' />
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            if (moduleName == "C") {
                manifest = DependencyManifest.Parse("C", @"<?xml version='1.0' encoding='utf-8'?>
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

        public override bool IsAvailable(string moduleName) {
            return true;
        }
    }

    internal class ParallelFakeProvider : TestModuleProviderBase {

        public override string Branch {
            get { return @"Dev\Foo"; }
        }

        public override IEnumerable<ExpertModule> GetAll() {
            return new ExpertModule[] {
                new ExpertModule("A") {
                    Branch = Branch
                },
                new ExpertModule("B") {
                    Branch = Branch
                },
                new ExpertModule("C") {
                    Branch = Branch
                },
                new ExpertModule("D") {
                    Branch = Branch
                }
            };
        }

        public override bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {
            if (moduleName == "A") {
                manifest = DependencyManifest.Parse("A", @"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            if (moduleName == "B") {
                manifest = DependencyManifest.Parse("B", @"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='A' />
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            if (moduleName == "C") {
                manifest = DependencyManifest.Parse("C", @"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
        <ReferencedModule Name='A' />
        <ReferencedModule Name='B' />
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            if (moduleName == "D") {
                manifest = DependencyManifest.Parse("D", @"<?xml version='1.0' encoding='utf-8'?>
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

        public override bool IsAvailable(string moduleName) {
            return true;
        }

    }

    internal class CircularReferenceProvider : TestModuleProviderBase {

        public override IEnumerable<ExpertModule> GetAll() {
            return new ExpertModule[] {
                new ExpertModule("A") {
                    Branch = Branch
                },
                new ExpertModule("B") {
                    Branch = Branch
                },
            };
        }

        public override bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {
            if (moduleName == "A") {
                manifest = DependencyManifest.Parse("A", @"<?xml version='1.0' encoding='utf-8'?>
<DependencyManifest>
    <ReferencedModules>
       <ReferencedModule Name='B' />
    </ReferencedModules>
</DependencyManifest>");
                return true;
            }

            if (moduleName == "B") {
                manifest = DependencyManifest.Parse("B", @"<?xml version='1.0' encoding='utf-8'?>
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

        public override bool IsAvailable(string moduleName) {
            return true;
        }
    }
}
