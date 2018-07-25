using System.Threading;
using Aderant.Build;
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

    internal class ConfiguredProject1 : ConfiguredProject {
        internal string outputAssembly;

        public ConfiguredProject1(IProjectTree tree, IFileSystem fileSystem)
            : base(tree, fileSystem) {
        }

        public override string OutputAssembly {
            get { return outputAssembly; }
        }
    }
}
