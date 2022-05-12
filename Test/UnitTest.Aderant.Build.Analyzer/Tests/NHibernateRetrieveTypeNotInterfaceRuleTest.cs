using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class NHibernateRetrieveTypeNotInterfaceRuleTest : AderantCodeFixVerifier<NHibernateRetrieveTypeNotInterfaceRule> {

        #region Properties

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

        #endregion Properties

        #region Tests

        [TestMethod]
        public async Task RepoInterfaceGetIntoInterfaceWithInterface() {
            const string test = "IRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Get<ITime>(new Identifier()); ";
            var expected = GetDiagnostic(MyCodeStartsAtLine + 1, 32);
            await VerifyCSharpDiagnostic(InsertCode(test), expected);
        }

        [TestMethod]
        public async Task RepoInterfaceGetIntoInterfaceWithConcrete() {
            const string test = "IRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Get<Time>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task RepoInterfaceGetIntoConcreteWithConcrete() {
            const string test = "IRepository provider = new ObjectRepository(); \n" + "Time timeEntry = provider.Get<Time>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task RepoConcreteGetIntoInterfaceWithInterface() {
            const string test = "ObjectRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Get<ITime>(new Identifier()); ";
            var expected = GetDiagnostic(MyCodeStartsAtLine + 1, 32);
            await VerifyCSharpDiagnostic(InsertCode(test), expected);
        }

        [TestMethod]
        public async Task RepoConcreteGetIntoInterfaceWithConcrete() {
            const string test = "ObjectRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Get<Time>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task RepoConcreteGetIntoConcreteWithConcrete() {
            const string test = "ObjectRepository provider = new ObjectRepository(); \n" + "Time timeEntry = provider.Get<Time>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task RepoInterfaceLinqIntoInterfaceWithInterface() {
            const string test = "IRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Linq<ITime>().FirstOrDefault(); ";
            var expected = GetDiagnostic(MyCodeStartsAtLine + 1, 33);
            await VerifyCSharpDiagnostic(InsertCode(test), expected);
        }

        [TestMethod]
        public async Task RepoInterfaceLinqIntoInterfaceWithConcrete() {
            const string test = "IRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Linq<Time>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task RepoInterfaceLinqIntoConcreteWithConcrete() {
            const string test = "IRepository provider = new ObjectRepository(); \n" + "Time timeEntry = provider.Linq<Time>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task RepoConcreteLinqIntoInterfaceWithInterface() {
            const string test = "ObjectRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Linq<ITime>().FirstOrDefault(); ";
            var expected = GetDiagnostic(MyCodeStartsAtLine + 1, 33);
            await VerifyCSharpDiagnostic(InsertCode(test), expected);
        }

        [TestMethod]
        public async Task RepoConcreteLinqIntoInterfaceWithConcrete() {
            const string test = "ObjectRepository provider = new ObjectRepository(); \n" + "ITime timeEntry = provider.Linq<Time>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task RepoConcreteLinqIntoConcreteWithConcrete() {
            const string test = "ObjectRepository provider = new ObjectRepository(); \n" + "Time timeEntry = provider.Linq<Time>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task SessionInterfaceGetIntoInterfaceWithInterface() {
            const string test = "IFrameworkSession provider = new Session(); \n" + "ITime timeEntry = provider.Get<ITime>(new Identifier()); ";
            var expected = GetDiagnostic(MyCodeStartsAtLine + 1, 32);
            await VerifyCSharpDiagnostic(InsertCode(test), expected);
        }

        [TestMethod]
        public async Task SessionInterfaceGetIntoInterfaceWithConcrete() {
            const string test = "IFrameworkSession provider = new Session(); \n" + "ITime timeEntry = provider.Get<Time>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task SessionInterfaceGetIntoConcreteWithConcrete() {
            const string test = "IFrameworkSession provider = new Session(); \n" + "Time timeEntry = provider.Get<Time>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task SessionConcreteGetIntoInterfaceWithInterface() {
            const string test = "Session provider = new Session(); \n" + "ITime timeEntry = provider.Get<ITime>(new Identifier()); ";
            var expected = GetDiagnostic(MyCodeStartsAtLine + 1, 32);
            await VerifyCSharpDiagnostic(InsertCode(test), expected);
        }

        [TestMethod]
        public async Task SessionConcreteGetIntoInterfaceWithConcrete() {
            const string test = "Session provider = new Session(); \n" + "ITime timeEntry = provider.Get<Time>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task SessionConcreteGetIntoConcreteWithConcrete() {
            const string test = "Session provider = new Session(); \n" + "Time timeEntry = provider.Get<Time>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task SessionInterfaceLinqIntoInterfaceWithInterface() {
            const string test = "IFrameworkSession provider = new Session(); \n" + "ITime timeEntry = provider.Linq<ITime>().FirstOrDefault(); ";
            var expected = GetDiagnostic(MyCodeStartsAtLine + 1, 33);
            await VerifyCSharpDiagnostic(InsertCode(test), expected);
        }

        [TestMethod]
        public async Task SessionInterfaceLinqIntoInterfaceWithConcrete() {
            const string test = "IFrameworkSession provider = new Session(); \n" + "ITime timeEntry = provider.Linq<Time>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task SessionInterfaceLinqIntoConcreteWithConcrete() {
            const string test = "IFrameworkSession provider = new Session(); \n" + "Time timeEntry = provider.Linq<Time>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task SessionConcreteLinqIntoInterfaceWithInterface() {
            const string test = "Session provider = new Session(); \n" + "ITime timeEntry = provider.Linq<ITime>().FirstOrDefault(); ";
            var expected = GetDiagnostic(MyCodeStartsAtLine + 1, 33);
            await VerifyCSharpDiagnostic(InsertCode(test), expected);
        }

        [TestMethod]
        public async Task SessionConcreteLinqIntoInterfaceWithConcrete() {
            const string test = "Session provider = new Session(); \n" + "ITime timeEntry = provider.Linq<Time>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task SessionConcreteLinqIntoConcreteWithConcrete() {
            const string test = "Session provider = new Session(); \n" + "Time timeEntry = provider.Linq<Time>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task DecoyInterfaceGetIntoInterfaceWithInterface() {
            const string test = "IDecoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Get<ITime>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task DecoyInterfaceGetIntoInterfaceWithConcrete() {
            const string test = "IDecoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Get<Time>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task DecoyInterfaceGetIntoConcreteWithConcrete() {
            const string test = "IDecoy provider = new Decoy(); \n" + "Time timeEntry = provider.Get<Time>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task DecoyConcreteGetIntoInterfaceWithInterface() {
            const string test = "Decoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Get<ITime>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task DecoyConcreteGetIntoInterfaceWithConcrete() {
            const string test = "Decoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Get<Time>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task DecoyConcreteGetIntoConcreteWithConcrete() {
            const string test = "Decoy provider = new Decoy(); \n" + "Time timeEntry = provider.Get<Time>(new Identifier()); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task DecoyInterfaceLinqIntoInterfaceWithInterface() {
            const string test = "IDecoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Linq<ITime>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task DecoyInterfaceLinqIntoInterfaceWithConcrete() {
            const string test = "IDecoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Linq<Time>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task DecoyInterfaceLinqIntoConcreteWithConcrete() {
            const string test = "IDecoy provider = new Decoy(); \n" + "Time timeEntry = provider.Linq<Time>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task DecoyConcreteLinqIntoInterfaceWithInterface() {
            const string test = "Decoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Linq<ITime>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task DecoyConcreteLinqIntoInterfaceWithConcrete() {
            const string test = "Decoy provider = new Decoy(); \n" + "ITime timeEntry = provider.Linq<Time>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task DecoyConcreteLinqIntoConcreteWithConcrete() {
            const string test = "Decoy provider = new Decoy(); \n" + "Time timeEntry = provider.Linq<Time>().FirstOrDefault(); ";
            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        #endregion Tests
    }
}
