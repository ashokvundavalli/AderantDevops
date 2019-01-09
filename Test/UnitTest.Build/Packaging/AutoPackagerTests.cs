using System.Linq;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Packaging {
    [TestClass]
    public class AutoPackagerTests {

        [TestMethod]
        public void Can_add_file_to_package() {
            var builder = new AutoPackager(NullLogger.Default);

            var filesToPackage = new PathSpec[] {
                new PathSpec("Foo.dll", "Foo.dll"),
            };

            var snapshot = new ProjectOutputSnapshot {
                IsTestProject = true,
                Directory = "",
                OutputPath = "",
                FilesWritten = new[] { "Foo.dll" },
            };

            var files = builder.BuildArtifact(filesToPackage, new[] { snapshot });

            Assert.AreEqual(1, files.Count);
        }

        [TestMethod]
        public void Trailing_slash_on_output_path_is_normalized() {
            var builder = new AutoPackager(NullLogger.Default);

            var filesToPackage = new PathSpec[] {
                new PathSpec("Foo.dll", "Foo.dll"),
            };

            var snapshot = new ProjectOutputSnapshot {
                IsTestProject = true,
                Directory = "",
                OutputPath = @"..\..\Bin\Module\\",
                FilesWritten = new[] { @"..\..\Bin\Module\Foo.dll" },
            };

            var files = builder.BuildArtifact(filesToPackage, new[] { snapshot });

            Assert.AreEqual(1, files.Count);
        }

        [TestMethod]
        public void Can_add_file_in_sub_directory_to_package() {
            var builder = new AutoPackager(NullLogger.Default);

            var filesToPackage = new PathSpec[] {
                new PathSpec(@"Test\Foo.dll", @"Test\Foo.dll"),
            };

            var snapshot = new ProjectOutputSnapshot {
                IsTestProject = true,
                Directory = "",
                OutputPath = "",
                FilesWritten = new[] { @"Test\Foo.dll" },
            };

            var files = builder.BuildArtifact(filesToPackage, new[] { snapshot });

            Assert.AreEqual(1, files.Count);
        }

        [TestMethod]
        public void File_content_of_auto_package_is_unique() {
            var builder = new AutoPackager(NullLogger.Default);

            var snapshot = new ProjectOutputSnapshot {
                IsTestProject = true,
                Directory = "",
                OutputPath = "",
                FilesWritten = new[] { "Foo1", "Foo2", "Foo3" },
            };

            var definitions = builder.CreatePackages(
                new[] { snapshot },
                new[] {
                    ArtifactPackageDefinition.Create(
                        "A",
                        b => { b.AddFile("Foo1", @"Bar\Foo1"); }),
                },
                new[] {
                    ArtifactPackageDefinition.Create(
                        "Test.B",
                        b => {
                            b.AddFile("Foo1", @"Bar\Foo1");
                            b.AddFile("Foo2", @"Bar\Foo2");
                            b.AddFile("Foo3", @"Bar\Foo3");
                        })
                }).ToList();

            Assert.AreEqual(1, definitions.Count);
            Assert.AreEqual(2, definitions[0].GetFiles().Count);
        }
    }
}
