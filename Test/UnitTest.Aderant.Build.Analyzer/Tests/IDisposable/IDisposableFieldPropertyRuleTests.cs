using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.IDisposable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Aderant.Build.Analyzer.Tests.IDisposable {
    [TestClass]
    public class IDisposableFieldPropertyRuleTests : IDisposableRuleBaseTests {
        #region Properties

        protected override RuleBase Rule => new IDisposableFieldPropertyRule();

        #endregion Properties

        #region Tests: Field

        [TestMethod]
        public void IDisposableFieldPropertyRule_ConstructorParameter_Public() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : IDisposable {
        private readonly IDisposable item;

        public TestClass(IDisposable arg) {
            item = arg;
        }

        public void Dispose() {
            // Empty.
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
                // Error: IDisposable item;
                GetDiagnostic(6, 38, "item"));
        }

        [TestMethod]
        public void IDisposableFieldPropertyRule_ConstructorParameter_Protected() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : IDisposable {
        private readonly IDisposable item;

        protected TestClass(IDisposable arg) {
            item = arg;
        }

        public void Dispose() {
            // Empty.
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
                // Error: IDisposable item;
                GetDiagnostic(6, 38, "item"));
        }

        [TestMethod]
        public void IDisposableFieldPropertyRule_ConstructorParameter_Private() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : IDisposable {
        private readonly IDisposable item;

        private TestClass(IDisposable arg) {
            item = arg;
        }

        public void Dispose() {
            // Empty.
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
                // Error: IDisposable item;
                GetDiagnostic(6, 38, "item"));
        }

        [TestMethod]
        public void IDisposableFieldPropertyRule_ConstructorParameter_Internal() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : IDisposable {
        private readonly IDisposable item;

        internal TestClass(IDisposable arg) {
            item = arg;
        }

        public void Dispose() {
            // Empty.
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
                // Error: IDisposable item;
                GetDiagnostic(6, 38, "item"));
        }

        [TestMethod]
        public void IDisposableFieldPropertyRule_NotDisposed() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        private DisposeMe item;

        public void Dispose() {
            // Empty.
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
                // Error: item = new DisposeMe();
                GetDiagnostic(4, 27, "item"));
        }

        [TestMethod]
        public void IDisposableFieldPropertyRule_DoubleAssignment() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        private DisposeMe item;

        private void Method() {
            item = new DisposeMe();
            item = new DisposeMe();
        }

        public void Dispose() {
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
                // Error: item = new DisposeMe();
                GetDiagnostic(7, 13, "item"));
        }

        [TestMethod]
        public void IDisposableFieldPropertyRule_UnrelatedUsing_MultiMethodAssingmentAndDisposal() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        private DisposeMe item0, item1 = new DisposeMe();

        private void Method() {
            item0 = new DisposeMe();

            using (item1) {
                // Empty.
            }

            item0.Dispose();

            item0 = new DisposeMe();
        }

        public void Dispose() {
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

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableFieldPropertyRule_NoFields() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        private void Method() {
            DisposeMe item = new DisposeMe();
            item.Dispose();

            using (item = new DisposeMe()) {
                // Empty.
            }
        }

        public void Dispose() {
            // Empty.
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
        public void IDisposableFieldPropertyRule_ObservableCollection() {
            const string code = @"
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Aderant.Framework.Extensions;

namespace Test {
    public class TestClass : System.IDisposable {
        private readonly ObservableCollection<DisposeMe> items = new ObservableCollection<DisposeMe>();

        private void Method() {
            items.Add(new DisposeMe());
            items.RemoveAt(0);
        }

        public void Dispose() {
            items.DisposeItems();
        }
    }

    public class DisposeMe : System.IDisposable {
        public void Dispose() {
            // Empty.
        }
    }
}

namespace Aderant.Framework.Extensions {
    public static class IDisposableExtensions {
        public static void DisposeItems(this IEnumerable<System.IDisposable> enumerable) {

        }
    }
}

namespace System.Collections.ObjectModel {
    public class ObservableCollection<T> : IList<T>, IEnumerable<T> {
        public IEnumerator<T> GetEnumerator() {
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(T item) {
        }

        public void Clear() {
        }

        public bool Contains(T item) {
            return false;
        }

        public void CopyTo(T[] array, int arrayIndex) {
        }

        public bool Remove(T item) {
            return false;
        }

        public int Count { get; }
        public bool IsReadOnly { get; }

        public int IndexOf(T item) {
            return 0;
        }

        public void Insert(int index, T item) {
        }

        public void RemoveAt(int index) {
        }

        public T this[int index] {
            get { return default(T); }
            set { }
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests: Field

        #region Tests: Property

        [TestMethod]
        public void IDisposableFieldPropertyRule_Property_NotDisposed() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        public DisposeMe Item { get; set; } = new DisposeMe();

        public void Dispose() {
            // Empty.
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
                // Error: public DisposeMe Item { get; set; } = new DisposeMe();
                GetDiagnostic(4, 26, "Item"));
        }

        [TestMethod]
        public void IDisposableFieldPropertyRule_Property_MultiAssignment_Disposed() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        public DisposeMe Item { get; set; }

        private void Method() {
            Item = new DisposeMe();

            Item.Dispose();

            Item = new DisposeMe();
        }

        public void Dispose() {
            Item.Dispose();
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
        public void IDisposableFieldPropertyRule_Property_MultiAssignment_NotDisposed() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        public DisposeMe Item { get; set; }

        private void Method() {
            Item = new DisposeMe();

            Item.Dispose();

            Item = new DisposeMe();
        }

        public void Dispose() {
            // Empty.
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
                // Error: Item = new DisposeMe();
                GetDiagnostic(4, 26, "Item"));
        }

        [TestMethod]
        public void IDisposableFieldPropertyRule_Property_BackingField() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        private DisposeMe backingField;

        public DisposeMe Item {
            get { return backingField; }
            set { backingField = value; }
        }

        public void Dispose() {
            backingField.Dispose();
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
        public void IDisposableFieldPropertyRule_Property_BackingField_LambdaSyntax() {
            const string code = @"
namespace Test {
    public class TestClass : System.IDisposable {
        private DisposeMe backingField;

        public DisposeMe Item => backingField;

        public void Dispose() {
            backingField.Dispose();
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
        public void IDisposableLocalVariableRule_Field_GetSet_Disposed() {
            const string code = @"
using System;

namespace Test {
    public class TestClass : IDisposable {
        private bool backingField;
        private IDisposable item;

        public bool TestProp {
            get {
                item = new DisposeMe();
                item.Dispose();
                return backingField;
            }
            set {
                item = new DisposeMe();
                item?.Dispose();
                backingField = value;
            }
        }

        public void Dispose() {
            item?.Dispose();
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
    public class TestClass : IDisposable {
        private bool backingField;
        private IDisposable item;

        public bool TestProp {
            get {
                item = new DisposeMe();
                item = new DisposeMe();
                return backingField;
            }
            set {
                item = new DisposeMe();
                item = new DisposeMe();
                backingField = value;
            }
        }

        public void Dispose() {
            item?.Dispose();
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
                // Error: item = new DisposeMe();
                GetDiagnostic(11, 17, "item"),
                // Error: item = new DisposeMe();
                GetDiagnostic(16, 17, "item"));
        }

        #endregion Tests: Property
    }
}
