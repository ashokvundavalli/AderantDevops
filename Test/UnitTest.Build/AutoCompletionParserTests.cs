using System.Collections.Generic;
using System.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build {

    [TestClass]
    public class AutoCompletionParserTests {

        [TestMethod]
        public void CursorAtCommandNameWithNoTrailingSpace_AndDefaultParam_ReturnsFalse() {
            AutoCompletionParser parser = new AutoCompletionParser("My-Command", "My-Command", new object[0]);
            Assert.IsFalse(parser.IsAutoCompletionForParameter("My-Command", "MyArg", true));
        }

        [TestMethod]
        public void CursorAtCommandNameWithTrailingSpace_AndDefaultParam_ReturnsTrue() {
            AutoCompletionParser parser = new AutoCompletionParser("My-Command ", "", new object[0]);
            Assert.IsTrue(parser.IsAutoCompletionForParameter("My-Command", "MyArg", true));
        }

        [TestMethod]
        public void OneCharacterAfterCommand_AndDefaultParam_ReturnsTrue() {
            AutoCompletionParser parser = new AutoCompletionParser("My-Command m", "m", new object[0]);
            Assert.IsTrue(parser.IsAutoCompletionForParameter("My-Command", "MyArg", true));
        }

        [TestMethod]
        public void OneSignificantCharacterAfterCommand_AndNotDefaultParam_ReturnsFalse() {
            AutoCompletionParser parser = new AutoCompletionParser("My-Command m", "m", new object[0]);
            Assert.IsFalse(parser.IsAutoCompletionForParameter("My-Command", "MyArg", false));
        }

        [TestMethod]
        public void OneSignificantCharacterAfterCommandWithDuplicateSpacesInbetween_AndDefaultParam_ReturnsTrue() {
            AutoCompletionParser parser = new AutoCompletionParser("My-Command    m", "m", new object[0]);
            Assert.IsTrue(parser.IsAutoCompletionForParameter("My-Command", "MyArg", true));
        }

        [TestMethod]
        public void CursorAtHyphen_ReturnsFalse() {
            AutoCompletionParser parser = new AutoCompletionParser("My-Command -", "-", new object[0]);
            Assert.IsFalse(parser.IsAutoCompletionForParameter("My-Command", "MyArg", false));
        }

        [TestMethod]
        public void CursorAtParameterName_ReturnsFalse() {
            AutoCompletionParser parser = new AutoCompletionParser("My-Command -MyArg", "-MyArg", new object[0]);
            Assert.IsFalse(parser.IsAutoCompletionForParameter("My-Command", "MyArg", false));
        }

        [TestMethod]
        public void CursorAtSpaceAfterParameterName_ReturnsTrue() {
            AutoCompletionParser parser = new AutoCompletionParser("My-Command -MyArg ", "", new object[0]);
            Assert.IsTrue(parser.IsAutoCompletionForParameter("My-Command", "MyArg", false));
        }

        [TestMethod]
        public void CursorAtSpaceAfterCommaInCommaSeparatedListParameterValue_ReturnsTrue() {
            AutoCompletionParser parser = new AutoCompletionParser("My-Command -MyArg abc, def", "", new object[0]);
            Assert.IsTrue(parser.IsAutoCompletionForParameter("My-Command", "MyArg", false));
        }

        [TestMethod]
        public void CursorAtSpaceAfterParameterNameInMultiPartStatement_ReturnsTrue() {
            AutoCompletionParser parser = new AutoCompletionParser("My-FirstCommand;My-Command -MyArg ", "", new object[0]);
            Assert.IsTrue(parser.IsAutoCompletionForParameter("My-Command", "MyArg", false));
        }

        #region module name completion tests
        [TestMethod]
        public void AutoCompletesSearchStringSeparatedByCase() {
            Mock<IDependencyBuilder> analyzerMock = new Mock<IDependencyBuilder>();

            ExpertModule module1 = new ExpertModule("Module.TwoWords");
            ExpertModule module2 = new ExpertModule("Module.ShouldntMatch");

            List<ExpertModule> moduleList = new List<ExpertModule>();
            moduleList.Add(module1);
            moduleList.Add(module2);
            analyzerMock.Setup(a => a.GetAllModules()).Returns(moduleList);

            AutoCompletionParser parser = new AutoCompletionParser("cm MTW", "MTW", new object[0]);
            string[] matches = parser.GetModuleMatches(analyzerMock.Object);
            Assert.IsTrue(matches.Count() == 1);
            Assert.IsTrue(matches.First() == "Module.TwoWords");
        }

        [TestMethod]
        public void AutoCompletesSearchStringSeparatedByPeriods() {
            Mock<IDependencyBuilder> analyzerMock = new Mock<IDependencyBuilder>();

            ExpertModule module1 = new ExpertModule("Module.TwoWords");
            ExpertModule module2 = new ExpertModule("Module.ShouldntMatch");

            List<ExpertModule> moduleList = new List<ExpertModule>();
            moduleList.Add(module1);
            moduleList.Add(module2);
            analyzerMock.Setup(a => a.GetAllModules()).Returns(moduleList);

            AutoCompletionParser parser = new AutoCompletionParser("cm m.t.w", "m.t.w", new object[0]);
            string[] matches = parser.GetModuleMatches(analyzerMock.Object);
            Assert.IsTrue(matches.Count() == 1);
            Assert.IsTrue(matches.First() == "Module.TwoWords");
        }

        [TestMethod]
        public void AggressivelySeparatesLowerCaseWhenNoMatchFound() {
            Mock<IDependencyBuilder> analyzerMock = new Mock<IDependencyBuilder>();

            ExpertModule module1 = new ExpertModule("Module.TwoWords");
            ExpertModule module2 = new ExpertModule("Module.ShouldntMatch");

            List<ExpertModule> moduleList = new List<ExpertModule>();
            moduleList.Add(module1);
            moduleList.Add(module2);
            analyzerMock.Setup(a => a.GetAllModules()).Returns(moduleList);

            AutoCompletionParser parser = new AutoCompletionParser("cm mtw", "mtw", new object[0]);
            string[] matches = parser.GetModuleMatches(analyzerMock.Object);
            Assert.IsTrue(matches.Count() == 1);
            Assert.IsTrue(matches.First() == "Module.TwoWords");
        }

        [TestMethod]
        public void AggressivelySeparates_AFTER_TryingToMatchBeginningOfString_WhenSearchStringIsAllLowerCase() {
            Mock<IDependencyBuilder> analyzerMock = new Mock<IDependencyBuilder>();

            ExpertModule module1 = new ExpertModule("Module.TwoWords");
            ExpertModule module3 = new ExpertModule("mtw");
            ExpertModule module2 = new ExpertModule("Module.ShouldntMatch");

            List<ExpertModule> moduleList = new List<ExpertModule>();
            moduleList.Add(module1);
            moduleList.Add(module2);
            moduleList.Add(module3);
            analyzerMock.Setup(a => a.GetAllModules()).Returns(moduleList);

            AutoCompletionParser parser = new AutoCompletionParser("cm mtw", "mtw", new object[0]);
            string[] matches = parser.GetModuleMatches(analyzerMock.Object);
            Assert.IsTrue(matches.Count() == 1);
            Assert.IsTrue(matches.First() == "mtw");
        }

        #endregion
    }
}