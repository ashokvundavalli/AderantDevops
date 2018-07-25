using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Microsoft.TeamFoundation.Build.Common;
using Moq;

namespace UnitTest.Build {
    [TestClass]
    public class ResolverTests {
        [TestMethod]
        public void Resolver_adds_build_analyzer() {
            var resolverImpl = Moq.Mock.Of<IDependencyResolver>();

            Mock.Get(resolverImpl).Setup(s => s.Resolve(
                It.IsAny<ResolverRequest>(), 
                It.Is<IEnumerable<IDependencyRequirement>>(r => Validate2(r)),
                It.IsAny<CancellationToken>())).Verifiable();

            var fs = Mock.Of<IFileSystem2>();

            var resolver = new Resolver(new NullLogger(), new IDependencyResolver[] { resolverImpl });

            var request = new ResolverRequest(new NullLogger(), fs, ExpertModule.Create(XElement.Parse("<Module Name=\'MyModule\' AssemblyVersion=\'5.3.1.0\' GetAction=\'NuGet\' />")));

            resolver.ResolveDependencies(request);

            Mock.Get(resolverImpl).Verify();
        }

        private bool Validate2(IEnumerable<IDependencyRequirement> dependencyRequirements) {
            return dependencyRequirements.ToList()[0].Name == "Aderant.Build.Analyzer";
        }
    }
}