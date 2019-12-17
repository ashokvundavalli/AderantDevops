using System;
using System.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class FilterDirectoryTests {
        [TestMethod]
        public void FilterDirectories() {
            string rootDirectory = @"C:\Git\ExpertSuite\";

            TaskItem root = new TaskItem(@"C:\Git\ExpertSuite");

            ITaskItem[] directories = new ITaskItem[] {
                root,
                new TaskItem(@"C:\Git\ExpertSuite\Init"),
                new TaskItem(@"C:\TFS\ExpertSuite\SomethingElse")
            };

            if (directories.GroupBy(x => x.ItemSpec.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase)).Count() > 1) {
                ITaskItem[] filteredDirectories = directories.Where(x => !x.ItemSpec.Equals(rootDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)).ToArray();
                Assert.IsFalse(filteredDirectories.Contains(root));
                return;
            }

            Assert.Fail();
        }
    }
}
