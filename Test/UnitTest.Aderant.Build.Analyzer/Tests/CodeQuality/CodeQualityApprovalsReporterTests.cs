using System.Threading.Tasks;
using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualityApprovalsReporterTests : AderantCodeFixVerifier<CodeQualityApprovalsReporterRule> {

        #region Properties

        protected override string PreCode => "using System;";

        protected override string PostCode => @"
namespace ApprovalTests.Reporters {
    [AttributeUsage(AttributeTargets.All)]
    public class UseReporterAttribute : Attribute {
        public UseReporterAttribute(Type reporter) { }
        public UseReporterAttribute(params Type[] reporter) { }
    }

    public class QuietReporter { }

    public class FileLauncherReporter { }

    public class DiffReporter { }

    public class TfsVnextReporter { }

    public class MsTestReporter { }
}

namespace Random.Attributes {
    [AttributeUsage(AttributeTargets.All)]
    public class RandomAttribute : Attribute { }
}
";

        #endregion Properties

        #region Tests

        [TestMethod]
        public async Task CodeQualityApprovalsReporter_AttributelessClassThrowsNoErrors() {
            const string code = @"
namespace Test.Namespace {
    public class AttributelessClass {
        public void AttributelessMethod() { }
    }
}
";
            await VerifyCSharpDiagnostic(InsertCode(code));
        }

        [TestMethod]
        public async Task CodeQualityApprovalsReporter_OnlyErrorsForUseReporterAttribute() {
            const string code = @"using Random.Attributes;
namespace Test.Namespace {
    [Random]
    public class DummyTestClass {
        [Random]
        public void DummyTestMethod() { }
    }
}
";
            await VerifyCSharpDiagnostic(InsertCode(code));
        }

        [TestMethod]
        public async Task CodeQualityApprovalsReporter_WhitelistedReportersAreIgnored_SingleArgumentAttributeConstructor() {
            const string code = @"using ApprovalTests.Reporters;

namespace Test.Namespace {
    [UseReporter(typeof(MsTestReporter))]
    public class DummyTestClass { 
        [UseReporter(typeof(TfsVnextReporter))]
        public void DummyMethod() { }

        [UseReporter(typeof(QuietReporter))]
        public void DummyMethod2() { }
    }
}
";
            await VerifyCSharpDiagnostic(InsertCode(code));
        }

        [TestMethod]
        public async Task CodeQualityApprovalsReporter_WhitelistedReportersAreIgnored_ParamAttributeConstructor() {
            const string code = @"using ApprovalTests.Reporters;

namespace Test.Namespace {
    [UseReporter(typeof(MsTestReporter), typeof(TfsVnextReporter), typeof(QuietReporter))]
    public class DummyTestClass { }
}
";
            await VerifyCSharpDiagnostic(InsertCode(code));
        }

        [TestMethod]
        public async Task CodeQualityApprovalsReporter_WhitelistedReportersAreIgnored_InlineAttributeDeclaration() {
            const string code = @"using ApprovalTests.Reporters;
using Random.Attributes;

namespace Test.Namespace {
    [Random, UseReporter(typeof(MsTestReporter), typeof(TfsVnextReporter), typeof(QuietReporter))]
    public class DummyTestClass { }
}
";
            await VerifyCSharpDiagnostic(InsertCode(code));
        }

        [TestMethod]
        public async Task CodeQualityApprovalsReporter_InvalidAttributeUsage_InlineAttributeDeclaration() {
            const string code = @"using ApprovalTests.Reporters;
using Random.Attributes;

namespace Test.Namespace {
    [Random, UseReporter(typeof(MsTestReporter), typeof(DiffReporter), typeof(QuietReporter))]
    public class DummyTestClass {
        [UseReporter(typeof(FileLauncherReporter)), Random]
        public void DummyMethod() { }
    }
}
";
            await VerifyCSharpDiagnostic(InsertCode(code),
                GetDiagnostic().WithSpan(5, 14, 5, 94),
                GetDiagnostic().WithSpan(7, 10, 7, 51));
        }

        [TestMethod]
        public async Task CodeQualityApprovalsReporter_InvalidAttributeUsageOnClass_SingleArgumentAttributeConstructor() {
            const string code = @"using ApprovalTests.Reporters;
using Random.Attributes;

namespace Test.Namespace {
    [Random]
    [UseReporter(typeof(DiffReporter))]
    public class DummyTestClass { }
}
";
            await VerifyCSharpDiagnostic(InsertCode(code), GetDiagnostic(6, 6));
        }

        [TestMethod]
        public async Task CodeQualityApprovalsReporter_InvalidAttributeUsageOnClass_ParamAttributeConstructor() {
            const string code = @"using ApprovalTests.Reporters;
using Random.Attributes;

namespace Test.Namespace {
    [Random]
    [UseReporter(typeof(DiffReporter), typeof(FileLauncherReporter))]
    public class DummyTestClass { }
}
";
            await VerifyCSharpDiagnostic(InsertCode(code), GetDiagnostic(6, 6));
        }

        [TestMethod]
        public async Task CodeQualityApprovalsReporter_InvalidAttributeUsageOnMethod_SingleArgumentAttributeConstructor() {
            const string code = @"using ApprovalTests.Reporters;

namespace Test.Namespace {
    public class DummyTestClass { 
        [UseReporter(typeof(DiffReporter))]
        public void DummyMethod() { }
    }
}
";
            await VerifyCSharpDiagnostic(InsertCode(code), GetDiagnostic(5, 10));
        }

        [TestMethod]
        public async Task CodeQualityApprovalsReporter_InvalidAttributeUsageOnMethod_ParamAttributeConstructor() {
            const string code = @"using ApprovalTests.Reporters;

namespace Test.Namespace {
    public class DummyTestClass { 
        [UseReporter(typeof(DiffReporter), typeof(FileLauncherReporter))]
        public void DummyMethod() { }
    }
}
";
            await VerifyCSharpDiagnostic(InsertCode(code), GetDiagnostic(5, 10));
        }

        [TestMethod]
        public async Task CodeQualityApprovalsReporter_MixedValidityAttributeArguments() {
            const string code = @"using ApprovalTests.Reporters;

namespace Test.Namespace {
    [UseReporter(typeof(DiffReporter), typeof(TfsVnextReporter))]
    public class DummyTestClass { 
        public void DummyMethod() { }
    }
}
";
            await VerifyCSharpDiagnostic(InsertCode(code), GetDiagnostic().WithSpan(4, 6, 4, 65));
        }

        #endregion Tests
    }
}
