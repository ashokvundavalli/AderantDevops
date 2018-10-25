using System;
using System.Threading;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem;

namespace UnitTest.Build.DependencyAnalyzer {
    internal class FakeVisualStudioProject : IDependable {

        private static int i;
        private int id = Interlocked.Increment(ref i);

        public string Id {
            get { return id.ToString(); }
        }
    }

    internal class TestConfiguredProject : ConfiguredProject {
        private readonly Guid guid = Guid.NewGuid();
        internal string outputAssembly;

        public TestConfiguredProject(IProjectTree tree)
            : base(tree) {
        }

        public TestConfiguredProject(IProjectTree projectTree, Guid guid)
            : base(projectTree) {
            this.guid = guid;
        }

        public override string OutputAssembly {
            get { return outputAssembly; }
        }

        public override Guid ProjectGuid {
            get { return guid; }
        }
    }
}
