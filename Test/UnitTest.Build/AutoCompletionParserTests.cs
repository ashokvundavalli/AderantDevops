using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    }
}