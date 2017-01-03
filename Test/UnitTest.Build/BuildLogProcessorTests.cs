using System.IO;
using System.Linq;
using Aderant.Build.Tasks.WarningProcess;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {

    [TestClass]
    public class BuildLogProcessorTests {

        [TestMethod]
        public void Can_delta_build_log() {
            var processor = new Aderant.Build.Tasks.WarningProcess.BuildLogProcessor();

            var result = processor.GetWarnings(
                new StringReader(BuildLogProcessor.Resources.buildlog_1),
                new StringReader(BuildLogProcessor.Resources.buildlog_2));

            Assert.AreEqual(1, result.GetDifference().Count());
        }

        [TestMethod]
        public void Line_is_split_on_colon() {
            var actual = WarningReportBuilder.CreateLine(@"Src\Aderant.Database.Build\StoredProcedureDelegateCompiler.cs(13, 23): Warning CS1591: Missing XML comment for publicly visible type or member 'StoredProcedureDelegateCompiler.foo'");

            var expected = @"Src\Aderant.Database.Build\StoredProcedureDelegateCompiler.cs(13, 23)  
Warning CS1591: Missing XML comment for publicly visible type or member 'StoredProcedureDelegateCompiler.foo'";

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void Line_is_not_changed_if_not_split() {
            var actual = WarningReportBuilder.CreateLine(@"Src\Aderant.Database.Build\StoredProcedureDelegateCompiler.cs(13, 23) Warning CS1591 Missing XML comment for publicly visible type or member 'StoredProcedureDelegateCompiler.foo'");

            var expected = @"Src\Aderant.Database.Build\StoredProcedureDelegateCompiler.cs(13, 23) Warning CS1591 Missing XML comment for publicly visible type or member 'StoredProcedureDelegateCompiler.foo'";

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void Rooted_path_is_not_split() {
            string text = @"C:\Program Files (x86)\MSBuild\14.0\bin\Microsoft.Common.CurrentVersion.targets(1820,5): Warning MSB3245: Could not resolve this reference. Could not locate the assembly ""Microsoft.VisualStudio.Services.Client"". Check to make sure the assembly exists on disk. If this reference is required by your code, you may get compilation errors.";
            var actual = WarningReportBuilder.CreateLine(text);

            string expected = @"C:\Program Files (x86)\MSBuild\14.0\bin\Microsoft.Common.CurrentVersion.targets(1820,5)  
Warning MSB3245: Could not resolve this reference. Could not locate the assembly ""Microsoft.VisualStudio.Services.Client"". Check to make sure the assembly exists on disk. If this reference is required by your code, you may get compilation errors.";

            Assert.AreEqual(expected, actual);
            Assert.IsNotNull(actual);
        }

        [TestMethod]
        public void CreateLine_is_null_safe() {
            var actual = WarningReportBuilder.CreateLine(null);
            Assert.IsNotNull(actual);
        }

        [TestMethod]
        public void Can_detect_transient_warnings() {

            var json = @"{""count"":1,""value"":[""2016-09-30T02:31:56.5884032Z ##[warning]C:\\Program Files (x86)\\MSBuild\\14.0\\bin\\Microsoft.Common.CurrentVersion.targets(3963,5): Warning MSB3026: Could not copy \""C:\\Program Files (x86)\\MSBuild\\14.0\\bin\\ru\\Microsoft.Build.Engine.resources.dll\"" to \""..\\..\\Bin\\Test\\ru\\Microsoft.Build.Engine.resources.dll\"". Beginning retry 1 in 1000ms. The process cannot access the file '..\\..\\Bin\\Test\\ru\\Microsoft.Build.Engine.resources.dll' because it is being used by another process.""]}";

            var parser = new BuildLogParser();
            var entries = parser.GetWarningEntries(new StringReader(json)).ToList();

            Assert.AreEqual(1, entries.Count);
            Assert.IsTrue(entries.First().IsCopyWarning);
        }

        [TestMethod]
        public void Can_parse_plain_text_log_file() {
            var parser = new BuildLogParser();
            var entries = parser.GetWarningEntries(new StringReader(BuildLogProcessor.Resources.plain_text_build_log)).ToList();

            Assert.AreEqual(43, entries.Count);
        }
    }
}
