using System.Linq;
using Aderant.Build.DependencyResolver;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Paket;

namespace UnitTest.Build.DependencyResolver.Model {
    [TestClass]
    public class RemoteFileTests {
        [TestMethod]
        public void Mapper_removes_http_prefix() {
            var unresolvedSource = new ModuleResolver.UnresolvedSource(
                "", "", "",
                ModuleResolver.Origin.NewHttpLink("http://google.com/foo.zip"),
                ModuleResolver.VersionRestriction.NoVersionRestriction,
                FSharpOption<string>.None,
                FSharpOption<string>.None,
                FSharpOption<string>.None,
                FSharpOption<string>.None);

            var map = PaketPackageManager.RemoteFileMapper.Map(
                new FSharpList<ModuleResolver.UnresolvedSource>(unresolvedSource,
                    FSharpList<ModuleResolver.UnresolvedSource>.Empty), "Banana").ToList();

            Assert.AreEqual(1, map.Count);
            Assert.AreEqual("http://google.com/foo.zip", map[0].Uri);
        }
    }
}