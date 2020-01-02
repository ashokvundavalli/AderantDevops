using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class NHibernateRetrieveTypeNotInterfaceRuleTest : AderantCodeFixVerifier {
        private RuleBase rule;
        private string postCode;

        protected override RuleBase Rule => new NHibernateRetrieveTypeNotInterfaceRule();

        protected override string PreCode => @"using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using System.Data;
    using System.Data.SqlClient;
    using Aderant.Framework; 
    using Aderant.Framework.Persistence;
    using Aderant.Framework.Persistence.DomainRepsitory;
    using Aderant.Time.Models;

namespace Aderant.Framework {
    public struct Identifier {
        public string StringValue { get; set; }
        public int IntValue { get; set; }
    }
}

namespace Aderant.Framework.Persistence {
    public interface IQuery {
        IOrderedQueryable<T> Linq<T>();
        T Get<T>(Identifier id);
    }

    public interface IFrameworkSession : IQuery {
        string Name { get; }
    }

    public class Session : IFrameworkSession {
        public IOrderedQueryable<T> Linq<T>() {
            return null;
        }
        public T Get<T>(Identifier id) {
            return default(T);
        }
        public string Name => string.Empty;
    }

    public interface IRepository : IQuery {
        bool ExcludeKeys { get; set; }
    }

    public interface IDecoy {
        IOrderedQueryable<T> Linq<T>();
        T Get<T>(Identifier id);
    }

    public class Decoy : IDecoy {
        public IOrderedQueryable<T> Linq<T>() {
            return null;
        }
        public T Get<T>(Identifier id) {
            return default(T);
        }
    }
}

namespace Aderant.Framework.Persistence.DomainRepsitory {
    public class ObjectRepository : Aderant.Framework.Persistence.IRepository {
        public IOrderedQueryable<T> Linq<T>() {
            return null;
        }
        public T Get<T>(Identifier id) {
            return default(T);
        }
        public bool ExcludeKeys { get; set; }
    }
}

namespace Aderant.Time.Models {
    public interface ITime {
        Identifier Id { get; set; }
        void ResetId();
    }

    public class Time : ITime {
        public virtual Identifier Id { get; set; }

        public virtual void ResetId() {
            Id = new Identifier();
        }
    }
}

namespace ConsoleApplication1 {
        class PROGRAM {
            static void Main(string[] args) {
                ";
        [TestMethod]
        public void RepoInterfaceGetIntoInterfaceWithInterface() {
            const string test = "IRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Get<ITime>(new Identifier()); ";
            var expected = GetDiagnostic(MyCodeStartsAtLine + 1, 32);
            VerifyCSharpDiagnostic(InsertCode(test), expected);
        }
        [TestMethod]
        public void RepoInterfaceGetIntoInterfaceWithConcrete() {
            const string test = "IRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Get<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void RepoInterfaceGetIntoConcreteWithConcrete() {
            const string test = "IRepository provider = new ObjectRepository(); \n" + "Time timeEntry = provider.Get<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void RepoConcreteGetIntoInterfaceWithInterface() {
            const string test = "ObjectRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Get<ITime>(new Identifier()); ";
            var expected = GetDiagnostic(MyCodeStartsAtLine + 1, 32);
            VerifyCSharpDiagnostic(InsertCode(test), expected);
        }
        [TestMethod]
        public void RepoConcreteGetIntoInterfaceWithConcrete() {
            const string test = "ObjectRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Get<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void RepoConcreteGetIntoConcreteWithConcrete() {
            const string test = "ObjectRepository provider = new ObjectRepository(); \n" + "Time timeEntry = provider.Get<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void RepoInterfaceLinqIntoInterfaceWithInterface() {
            const string test = "IRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Linq<ITime>(new Identifier()); ";
            DiagnosticResult expected = GetDiagnostic(MyCodeStartsAtLine + 1, 33);
            VerifyCSharpDiagnostic(InsertCode(test), expected);
        }
        [TestMethod]
        public void RepoInterfaceLinqIntoInterfaceWithConcrete() {
            const string test = "IRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Linq<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void RepoInterfaceLinqIntoConcreteWithConcrete() {
            const string test = "IRepository provider = new ObjectRepository(); \n" + "Time timeEntry = provider.Linq<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void RepoConcreteLinqIntoInterfaceWithInterface() {
            const string test = "ObjectRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Linq<ITime>(new Identifier()); ";
            DiagnosticResult expected = GetDiagnostic(MyCodeStartsAtLine + 1, 33);
            VerifyCSharpDiagnostic(InsertCode(test), expected);
        }
        [TestMethod]
        public void RepoConcreteLinqIntoInterfaceWithConcrete() {
            const string test = "ObjectRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Linq<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void RepoConcreteLinqIntoConcreteWithConcrete() {
            const string test = "ObjectRepository provider = new ObjectRepository(); \n" + "Time timeEntry = provider.Linq<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void SessionInterfaceGetIntoInterfaceWithInterface() {
            const string test = "IFrameworkSession provider = new Session(); \n" + "ITime timeEntry = provider.Get<ITime>(new Identifier()); ";
            DiagnosticResult expected = GetDiagnostic(MyCodeStartsAtLine + 1, 32);
            VerifyCSharpDiagnostic(InsertCode(test), expected);
        }
        [TestMethod]
        public void SessionInterfaceGetIntoInterfaceWithConcrete() {
            const string test = "IFrameworkSession provider = new Session(); \n" + "ITime timeEntry = provider.Get<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void SessionInterfaceGetIntoConcreteWithConcrete() {
            const string test = "IFrameworkSession provider = new Session(); \n" + "Time timeEntry = provider.Get<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void SessionConcreteGetIntoInterfaceWithInterface() {
            const string test = "Session provider = new Session(); \n" + "ITime timeEntry = provider.Get<ITime>(new Identifier()); ";
            DiagnosticResult expected = GetDiagnostic(MyCodeStartsAtLine + 1, 32);
            VerifyCSharpDiagnostic(InsertCode(test), expected);
        }
        [TestMethod]
        public void SessionConcreteGetIntoInterfaceWithConcrete() {
            const string test = "Session provider = new Session(); \n" + "ITime timeEntry = provider.Get<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void SessionConcreteGetIntoConcreteWithConcrete() {
            const string test = "Session provider = new Session(); \n" + "Time timeEntry = provider.Get<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void SessionInterfaceLinqIntoInterfaceWithInterface() {
            const string test = "IFrameworkSession provider = new Session(); \n" + "ITime timeEntry = provider.Linq<ITime>(new Identifier()); ";
            DiagnosticResult expected = GetDiagnostic(MyCodeStartsAtLine + 1, 33);
            VerifyCSharpDiagnostic(InsertCode(test), expected);
        }
        [TestMethod]
        public void SessionInterfaceLinqIntoInterfaceWithConcrete() {
            const string test = "IFrameworkSession provider = new Session(); \n" + "ITime timeEntry = provider.Linq<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void SessionInterfaceLinqIntoConcreteWithConcrete() {
            const string test = "IFrameworkSession provider = new Session(); \n" + "Time timeEntry = provider.Linq<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void SessionConcreteLinqIntoInterfaceWithInterface() {
            const string test = "Session provider = new Session(); \n" + "ITime timeEntry = provider.Linq<ITime>(new Identifier()); ";
            DiagnosticResult expected = GetDiagnostic(MyCodeStartsAtLine + 1, 33);
            VerifyCSharpDiagnostic(InsertCode(test), expected);
        }
        [TestMethod]
        public void SessionConcreteLinqIntoInterfaceWithConcrete() {
            const string test = "Session provider = new Session(); \n" + "ITime timeEntry = provider.Linq<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void SessionConcreteLinqIntoConcreteWithConcrete() {
            const string test = "Session provider = new Session(); \n" + "Time timeEntry = provider.Linq<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void DecoyInterfaceGetIntoInterfaceWithInterface() {
            const string test = "IDecoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Get<ITime>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void DecoyInterfaceGetIntoInterfaceWithConcrete() {
            const string test = "IDecoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Get<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void DecoyInterfaceGetIntoConcreteWithConcrete() {
            const string test = "IDecoy provider = new Decoy(); \n" + "Time timeEntry = provider.Get<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void DecoyConcreteGetIntoInterfaceWithInterface() {
            const string test = "Decoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Get<ITime>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void DecoyConcreteGetIntoInterfaceWithConcrete() {
            const string test = "Decoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Get<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void DecoyConcreteGetIntoConcreteWithConcrete() {
            const string test = "Decoy provider = new Decoy(); \n" + "Time timeEntry = provider.Get<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void DecoyInterfaceLinqIntoInterfaceWithInterface() {
            const string test = "IDecoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Linq<ITime>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void DecoyInterfaceLinqIntoInterfaceWithConcrete() {
            const string test = "IDecoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Linq<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void DecoyInterfaceLinqIntoConcreteWithConcrete() {
            const string test = "IDecoy provider = new Decoy(); \n" + "Time timeEntry = provider.Linq<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void DecoyConcreteLinqIntoInterfaceWithInterface() {
            const string test = "Decoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Linq<ITime>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void DecoyConcreteLinqIntoInterfaceWithConcrete() {
            const string test = "Decoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Linq<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
        [TestMethod]
        public void DecoyConcreteLinqIntoConcreteWithConcrete() {
            const string test = "Decoy provider = new Decoy(); \n" + "Time timeEntry = provider.Linq<Time>(new Identifier()); ";
            VerifyCSharpDiagnostic(InsertCode(test));
        }
    }
}
