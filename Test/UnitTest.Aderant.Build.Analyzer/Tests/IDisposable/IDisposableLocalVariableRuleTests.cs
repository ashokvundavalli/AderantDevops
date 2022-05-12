using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.IDisposable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Aderant.Build.Analyzer.Tests.IDisposable {
    [TestClass]
    public class IDisposableLocalVariableRuleTests : IDisposableRuleBaseTests<IDisposableLocalVariableRule> {

        #region Tests

        [TestMethod]
        public void IDisposableLocalVariableRule_FlowControl_If_Diagnostic() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            if (true) {
                DisposeMe item0;
                item0 = new DisposeMe();
            } else if (!false) {
                DisposeMe item1 = new DisposeMe();
            } else {
                DisposeMe item2;
                item2 = new DisposeMe();
            }
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: item0 = new DisposeMe();
                GetDiagnostic(7, 17, "item0"),
                // Error: item1 = new DisposeMe();
                GetDiagnostic(9, 27, "item1"),
                // Error: item2 = new DisposeMe();
                GetDiagnostic(12, 17, "item2"));
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_FlowControl_If_NoDiagnostic() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item;

            if (true) {
                item = new DisposeMe();
            } else if (!false) {
                item = new DisposeMe();
            } else {
                item = new DisposeMe();
            }

            item.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_FlowControl_If_NoDiagnostic_Assigned() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item = new DisposeMe();

            if (true) {
                item = new DisposeMe();
            }

            item.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_FlowControl_Switch() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item;

            int i = 0;

            switch (i) {
                case 0: {
                    item = new DisposeMe();
                    break;
                }
                case 1:
                    item = new DisposeMe();
                    break;
                default:
                    item = new DisposeMe();
                    break;
            }

            item.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_CollectionAdd() {
            const string code = @"
using System;
using System.Collections.Generic;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : IDisposable {
        private readonly List<DisposeMe> items = new List<DisposeMe>(1);

        private void Method() {
            DisposeMe item;

            item = new DisposeMe();

            items?.Add(item);
        }

        public void Dispose() {
            items?.DisposeItems();
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}

namespace Aderant.Framework.Extensions {
    public static class IDisposableExtensions {
        public static void DisposeItems(this IEnumerable<IDisposable> enumerable) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_ConditionalOperator() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            var item = new DisposeMe();
            item?.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_ConditionalOperator_Close() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            var item = new DisposeMe();
            item?.Close();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Close() {
            Dispose();
        }

        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_DisposableObject_NonDisposableProperty() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            var item0 = new ChildClass();

            var item1 = item0.TestInt;

            item0.Dispose();
        }

        public class ChildClass : System.IDisposable {
            public DisposeMe Item;

            public int TestInt = 10;

            public void Dispose() {
                Item.Dispose();
            }
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_ObjectCreationExpression() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            var item = new ChildClass {
                Item = new DisposeMe()
            };

            item.Dispose();
        }

        public class ChildClass : System.IDisposable {
            public DisposeMe Item;

            public void Dispose() {
                Item.Dispose();
            }
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_MethodParameter_ObjectCreation() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item = GetDisposeMe(new object());

            item.Dispose();
        }

        private static DisposeMe GetDisposeMe(object item) {
            return new DisposeMe();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_MethodParameter_MethodInvocation() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item = new DisposeMe(SomeMethod());

            item.Dispose();
        }

        private static object SomeMethod() {
            return new object();
        }
    }

    public class DisposeMe : System.IDisposable {
        public DisposeMe(object item) {
            // Empty.
        }

        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_Using() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            using (var item = new DisposeMe()) {
                // Empty.
            }
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_Using_Delayed() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            var item = new DisposeMe();

            using (item) {
                // Empty.
            }
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_Using_DetatchedDeclaration() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item;

            using (item = new DisposeMe()) {
                // Empty.
            }
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_MultipleDeclaration() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item0 = new DisposeMe(), item1;

            using (item0 = new DisposeMe()) {
                // Empty.
            }

            item1 = new DisposeMe();

            item0 = new DisposeMe();
            item0.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: DisposeMe item0 = new DisposeMe()
                GetDiagnostic(5, 23, "item0"),
                // Error: item1 = new DisposeMe();
                GetDiagnostic(11, 13, "item1"));
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_Reassignment() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            var item = new DisposeMe();
            item = new DisposeMe();
            item.Dispose();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: DisposeMe item = new DisposeMe()
                GetDiagnostic(5, 17, "item"));
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_Reassignment_ParameterProperty() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method(DisposeMe param) {
            var item0 = new DisposeMe();
            item0.Dispose();
            item0 = (param?.Prop ?? null) as System.IDisposable;
            item0 = (param?.Prop ?? GetDisposable()) as System.IDisposable;
            item0?.Dispose();

            var item1 = null;
            item1 = (System.IDisposable)(param?.Prop != null ? param.Prop : null);
            item1 = (System.IDisposable)(param?.Prop != null ? param.Prop : GetDisposable());
            item1?.Dispose();
        }

        private DisposeMe GetDisposable() {
            return new DisposeMe();
        }
    }

    public class DisposeMe : System.IDisposable {
        public System.IDisposable Prop { get; }

        public void Dispose() {
            Prop.Dispose();
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_ParameterProperty() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method(DisposeMe param) {
            var item0 = (param?.Prop ?? null) as System.IDisposable;
            var item1 = (System.IDisposable)(param?.Prop != null ? param.Prop : null);
        }
    }

    public class DisposeMe : System.IDisposable {
        public System.IDisposable Prop { get; }

        public void Dispose() {
            Prop.Dispose();
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_Returned() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void Method() {
            DisposeMe item = new DisposeMe();

            return item;
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_GetterObjectCreation_Disposed() {
            const string code = @"
using System;

namespace Test {
    public class TestClass{

        private string TestProperty {
            get {
                var x = new FooDisposable();
                x.Dispose();
                return ""testString"";
            }
        }
    }

    public class FooDisposable : IDisposable {
        public void Dispose() {
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_SetterObjectCreation_Disposed() {
            const string code = @"
using System;

namespace Test {
    public class TestClass{

        private string testBackingProperty;
        private string TestProperty {
            set {
                var x = new FooDisposable();
                testBackingProperty = value;
                x.Dispose();
            }
        }
    }

    public class FooDisposable : IDisposable {
        public void Dispose() {
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_GetterObjectCreation_NotDisposed() {
            const string code = @"
using System;

namespace Test {
    public class TestClass{
        private string TestProperty {
            get {
                var item = new FooDisposable();
                return ""testString"";
            }
        }
    }

    public class FooDisposable : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code, GetDiagnostic(8, 21, "item"));
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_SetterObjectCreation_NotDisposed() {
            const string code = @"
using System;

namespace Test {
    public class TestClass{
        private string testBackingField;

        private string TestProperty {
            set {
                var item = new FooDisposable();
                testBackingField = value;
            }
        }
    }

    public class FooDisposable : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";
            VerifyCSharpDiagnostic(code, GetDiagnostic(10, 21, "item"));
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_NestedConditional_WhenFalse_NoParen() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public IDisposable TestMethod() {
            var item = new DisposeMe();

            return item != null ? null : true ? item : null;
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";
            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_NestedConditional_WhenFalse_Paren() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public IDisposable TestMethod() {
            var item = new DisposeMe();

            return item != null ? null : (true ? item : null);
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";
            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_NestedConditional_WhenTrue_NoParen() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public IDisposable TestMethod() {
            var item = new DisposeMe();

            return item != null ? true ? null : item : null;
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";
            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_NestedConditional_WhenTrue_Paren() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        public IDisposable TestMethod() {
            var item = new DisposeMe();

            return item != null ? (true ? null : item) : null;
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";
            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_Field_GetSet_Disposed() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private bool backingField;

        public bool TestProp {
            get {
                var item = new DisposeMe();
                item.Dispose();
                return backingField;
            }
            set {
                var item = new DisposeMe();
                item?.Dispose();
                backingField = value;
            }
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableLocalVariableRule_Field_GetSet_NotDisposed() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private bool backingField;

        public bool TestProp {
            get {
                var item = new DisposeMe();
                return backingField;
            }
            set {
                var item = new DisposeMe();
                backingField = value;
            }
        }
    }

    public class DisposeMe : IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: var item = new DisposeMe();
                GetDiagnostic(10, 21, "item"),
                // Error: var item = new DisposeMe();
                GetDiagnostic(14, 21, "item"));
        }

        #endregion Tests
    }
}
