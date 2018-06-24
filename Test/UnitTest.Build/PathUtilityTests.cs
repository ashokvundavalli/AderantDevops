using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class PathUtilityTests {

        [TestMethod]
        public void MakeRelativeTests() {
            Assert.AreEqual(@"foo.cpp", PathUtility.MakeRelative(@"c:\abc\def", @"c:\abc\def\foo.cpp"));
            Assert.AreEqual(@"def\foo.cpp", PathUtility.MakeRelative(@"c:\abc\", @"c:\abc\def\foo.cpp"));
            Assert.AreEqual(@"..\foo.cpp", PathUtility.MakeRelative(@"c:\abc\def\xyz", @"c:\abc\def\foo.cpp"));
            Assert.AreEqual(@"..\ttt\foo.cpp", PathUtility.MakeRelative(@"c:\abc\def\xyz\", @"c:\abc\def\ttt\foo.cpp"));
            Assert.AreEqual(@"e:\abc\def\foo.cpp", PathUtility.MakeRelative(@"c:\abc\def", @"e:\abc\def\foo.cpp"));
            Assert.AreEqual(@"foo.cpp", PathUtility.MakeRelative(@"\\aaa\abc\def", @"\\aaa\abc\def\foo.cpp"));
            Assert.AreEqual(@"foo.cpp", PathUtility.MakeRelative(@"c:\abc\def", @"foo.cpp"));
            Assert.AreEqual(@"foo.cpp", PathUtility.MakeRelative(@"c:\abc\def", @"..\def\foo.cpp"));
            Assert.AreEqual(@"\\host\path\file", PathUtility.MakeRelative(@"c:\abc\def", @"\\host\path\file"));
            Assert.AreEqual(@"\\host\d$\file", PathUtility.MakeRelative(@"c:\abc\def", @"\\host\d$\file"));
            Assert.AreEqual(@"..\fff\ggg.hh", PathUtility.MakeRelative(@"c:\foo\bar\..\abc\cde", @"c:\foo\bar\..\abc\fff\ggg.hh"));
        }
    }
}
