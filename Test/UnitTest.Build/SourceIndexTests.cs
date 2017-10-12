using System;
using Aderant.Build;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MSBuild.Community.Tasks.SourceServer;

namespace UnitTest.Build {
    [TestClass]
    public class SourceIndexTests {
        [TestMethod]
        public void Generated_code_files_are_removed() {
            FileSystem fs = new FileSystem {
                Directory = new TestDirectoryOperations {
                    GetDirectoryContentsDelegate = (s, s1) => new[] { @"C:\temp\Controllers\Foo.tt" }
                }
            };

            SourceFile sourceFile = new SourceFile(@"C:\temp\Controllers\Foo.cs");
            bool removeSourceFile = SourceIndex.RemoveSourceFile(fs, sourceFile);

            Assert.IsTrue(removeSourceFile);
        }
    }

    internal class TestDirectoryOperations : DirectoryOperations {
        // The test implementation for GetFileSystemEntries.
        public Func<string, string, string[]> GetDirectoryContentsDelegate { get; set; }

        public override string[] GetFileSystemEntries(string path, string searchPattern) {
            if (GetDirectoryContentsDelegate != null) {
                return GetDirectoryContentsDelegate(path, searchPattern);
            }

            return base.GetFileSystemEntries(path, searchPattern);
        }
    }  
}