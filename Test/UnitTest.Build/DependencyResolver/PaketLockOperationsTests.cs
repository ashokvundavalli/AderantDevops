using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.DependencyResolver.Model;
using Aderant.Build.Logging;
using Microsoft.FSharp.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Paket;
using Constants = Aderant.Build.Constants;

namespace UnitTest.Build.DependencyResolver {
    [TestClass]
    public class PaketLockOperationsTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestHashing() {
            var groups = new FSharpMap<Domain.GroupName, LockFileGroup>(
                new List<Tuple<Domain.GroupName, LockFileGroup>>(1) {
                    Tuple.Create(Domain.GroupName("Test"),
                        new LockFileGroup(Domain.GroupName("Test"), InstallOptions.Default,
                            new FSharpMap<Domain.PackageName, PackageResolver.ResolvedPackage>(
                                new List<Tuple<Domain.PackageName, PackageResolver.ResolvedPackage>>()), null))
                });

            string hash = PaketLockOperations.HashLockFile(new LockFile(Constants.PaketLock, groups));

            Assert.IsNotNull(hash);
            Assert.AreEqual(40, hash.Length);
        }

        [TestMethod]
        public void GetPackageInfoOperatesAsExpected() {
            PaketLockOperations paketLockOperations = new PaketLockOperations("Test", Build.Resources.PaketLock.Split(new string[] {Environment.NewLine}, StringSplitOptions.None));
            List<PackageGroup> packageInfo = paketLockOperations.GetPackageInfo();
            
            Assert.AreEqual(2, packageInfo.Count);

            const string buildAnalyzer = "Aderant.Build.Analyzer";

            Assert.IsTrue(packageInfo.Any(x => string.Equals("Main", x.Name) && x.PackageInfo.Any(y => string.Equals(buildAnalyzer, y.Name) && string.Equals("2.1.1", y.Version))));
            Assert.IsTrue(packageInfo.Any(x => string.Equals("Development", x.Name) && x.PackageInfo.Any(y => string.Equals(buildAnalyzer, y.Name) && string.Equals("3.1.1", y.Version))));
        }
    }
}
