using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.IDisposable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Aderant.Build.Analyzer.Tests.IDisposable {
    [TestClass]
    public class IDisposableFactoryRegistrationRuleTests : IDisposableRuleBaseTests<IDisposableFactoryRegistrationRule> {
        #region Fields

        public static bool VerifiedMockedClasses;

        #endregion Fields

        #region Properties
        protected override string PreCode => FactoryRegistrationClass;

        #endregion Properties

        #region Test Administration

        [TestInitialize]
        public void TestInit() {
            if (!VerifiedMockedClasses) {
                VerifyCSharpDiagnostic(FactoryRegistrationClass);
                VerifiedMockedClasses = true;
            }
        }

        #endregion Test Administration

        #region FactoryRegistration Attribute

        //Skeleton class of the attribute with same class name, namespace and constructor signatures.
        private const string FactoryRegistrationClass = @"namespace Aderant.Framework.Factories {
    using System;
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true, Inherited=false)]
    public class FactoryRegistrationAttribute : Attribute {

        public Type InterfaceType { get; set; }

        public string Scope { get; set; }

        public FactoryRegistrationAttribute(Type interfaceType, string scope, Type builderType)
            : this(interfaceType, scope, builderType, null) {
        }

        public FactoryRegistrationAttribute(Type interfaceType, string scope, Type builderType, Type securerType) {
        }

        public FactoryRegistrationAttribute(Type interfaceType, string scope, Type builderType, Type securerType, Type implementationType) {
        }

        public FactoryRegistrationAttribute(string interfaceName, string scope, Type builderType) : this(interfaceName, scope, builderType, null) {
        }

        public FactoryRegistrationAttribute(string interfaceName, string scope, Type builderType, Type securerType) {
        }

        public FactoryRegistrationAttribute(Type interfaceType, string scope)
            : this(interfaceType, scope, null) {
        }

        public FactoryRegistrationAttribute(Type interfaceType)
            : this(interfaceType, null, null) {
        }

        public FactoryRegistrationAttribute(string interfaceName) : this(interfaceName, null, null){
        }

        public FactoryRegistrationAttribute() {
        }
    }
}";

        #endregion FactoryRegistration Attribute

        #region Tests: Valid Code

        [TestMethod]
        public void IDisposableFactoryRegistrationRule_ClassDoesNotImplementIDisposable_StandardAttribute() {
            const string code = @"namespace Test {
    using Aderant.Framework.Factories;
    using System;
    [FactoryRegistration(typeof(INonDisposable))]
    public class TestClass {}

    public interface INonDisposable {}
}
";
            VerifyCSharpDiagnostic(InsertCode(code));
        }

        [TestMethod]
        public void IDisposableFactoryRegistrationRule_ClassDoesNotImplementIDisposable_PropertyAssignmentAttribute() {
            const string code = @"namespace Test {
    using Aderant.Framework.Factories;
    using System;
    [FactoryRegistration(InterfaceType = typeof(INonDisposable))]
    public class TestClass {}

    public interface INonDisposable {}
}
";
            VerifyCSharpDiagnostic(InsertCode(code));
        }

        [TestMethod]
        public void IDisposableFactoryRegistrationRule_BothImplementIDisposable_StandardAttribute() {
            const string code = @"namespace Test {
    using Aderant.Framework.Factories;
    using System;
    [FactoryRegistration(typeof(IImplementDisposable))]
    public class TestClass: IImplementDisposable {
        public void Dispose() {
        }
    }

    public interface IImplementDisposable : IDisposable{}
}
";
            VerifyCSharpDiagnostic(InsertCode(code));
        }

        [TestMethod]
        public void IDisposableFactoryRegistrationRule_BothImplementIDisposable_WhiteListDoesNotInterfere() {

            const string code = @"namespace Aderant.Query {
    using Aderant.Framework.Factories;
    using System;
    [FactoryRegistration(typeof(IQueryServiceProxy))]
    public class TestClass: IQueryServiceProxy {
        public void Dispose() {
        }
    }

    public interface IQueryServiceProxy : IDisposable{}
}
";
            VerifyCSharpDiagnostic(InsertCode(code));
        }

        [TestMethod]
        public void IDisposableFactoryRegistrationRule_BothImplementIDisposable_PropertyAssignmentAttribute() {
            const string code = @"namespace Test {
    using Aderant.Framework.Factories;
    using System;
    [FactoryRegistration(InterfaceType = typeof(IImplementDisposable))]
    public class TestClass: IImplementDisposable {
        public void Dispose() {
        }
    }

    public interface IImplementDisposable : IDisposable{}
}
";
            VerifyCSharpDiagnostic(InsertCode(code));
        }

        [TestMethod]
        public void IDisposableFactoryRegistrationRule_BothImplementIDisposable_PropertyAssignmentAttribute_AsSecondaryAssignment() {
            const string code = @"namespace Test {
    using Aderant.Framework.Factories;
    using System;
    [FactoryRegistration(Scope = ""TestScope"", InterfaceType = typeof(IImplementDisposable))]
    public class TestClass: IImplementDisposable {
        public void Dispose() {
        }
    }

    public interface IImplementDisposable : IDisposable{}
}
";
            VerifyCSharpDiagnostic(InsertCode(code));
        }

        [TestMethod]
        public void IDisposableFactoryRegistrationRule_ClassImplementsIDisposable_NotFactoryRegistered() {
            const string code = @"namespace Test {
    using System;
    public class TestClass: IImplementDisposable {
        public void Dispose() {
        }
    }

    public interface IImplementDisposable : IDisposable{}
}
";
            VerifyCSharpDiagnostic(InsertCode(code));
        }

        #endregion Tests: Valid Code

        #region Tests: Invalid Code

        [TestMethod]
        public void IDisposableFactoryRegistrationRule_OnlyClassImplementsIDisposable_StandardAttribute() {
            const string code = @"
namespace Test {
    using Aderant.Framework.Factories;
    using System;
    [FactoryRegistration(typeof(INonDisposable))]
    public class TestClass: INonDisposable, IDisposable {
        public void Dispose() {
        }
    }

    public interface INonDisposable {}
}
";
            VerifyCSharpDiagnostic(InsertCode(code), GetDiagnostic(44, 6, "INonDisposable", "TestClass"));
        }

        [TestMethod]
        public void IDisposableFactoryRegistrationRule_OnlyClassImplementsIDisposable_StandardAttribute_MultipleParameters() {
            const string code = @"
namespace Test {
    using Aderant.Framework.Factories;
    using System;
    [FactoryRegistration(typeof(INonDisposable), ""TestScope"")]
    public class TestClass: INonDisposable, IDisposable {
        public void Dispose() {
        }
    }

    public interface INonDisposable {}
}
";
            VerifyCSharpDiagnostic(InsertCode(code), GetDiagnostic(44, 6, "INonDisposable", "TestClass"));
        }

        [TestMethod]
        public void IDisposableFactoryRegistrationRule_OnlyClassImplementsIDisposable_PropertyAssignmentAttribute() {
            const string code = @"
namespace Test {
    using Aderant.Framework.Factories;
    using System;
    [FactoryRegistration(InterfaceType = typeof(INonDisposable))]
    public class TestClass: INonDisposable, IDisposable {
        public void Dispose() {
        }
    }

    public interface INonDisposable {}
}
";
            VerifyCSharpDiagnostic(InsertCode(code), GetDiagnostic(44, 6, "INonDisposable", "TestClass"));
        }

        [TestMethod]
        public void IDisposableFactoryRegistrationRule_OnlyClassImplementsIDisposable_PropertyAssignmentAttribute_AsSecondAssignment() {
            const string code = @"
namespace Test {
    using Aderant.Framework.Factories;
    using System;
    [FactoryRegistration(Scope = ""hello"", InterfaceType = typeof(INonDisposable))]
    public class TestClass: INonDisposable, IDisposable {
        public void Dispose() {
        }
    }

    public interface INonDisposable {}
}
";
            VerifyCSharpDiagnostic(InsertCode(code), GetDiagnostic(44, 6, "INonDisposable", "TestClass"));
        }

        [TestMethod]
        public void IDisposableFactoryRegistrationRule_OnlyClassImplementsIDisposable_StandardAttribute_IndirectInheritance() {
            const string code = @"
namespace Test {
    using Aderant.Framework.Factories;
    using System;
    [FactoryRegistration(Scope = ""hello"", InterfaceType = typeof(INonDisposable))]
    public class TestClass: INonDisposable, IImplementDisposable {
        public void Dispose() {
        }
    }

    public interface INonDisposable {}
    public interface IImplementDisposable : IDisposable{}
}
";
            VerifyCSharpDiagnostic(InsertCode(code), GetDiagnostic(44, 6, "INonDisposable", "TestClass"));
        }

        #endregion Tests: Invalid Code
    }
}
