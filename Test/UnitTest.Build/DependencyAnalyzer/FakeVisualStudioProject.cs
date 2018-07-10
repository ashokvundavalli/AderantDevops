using System.Threading;
using Aderant.Build.Model;

namespace UnitTest.Build.DependencyAnalyzer {
    internal class FakeVisualStudioProject : IDependable {

        private static int i;
        private int id = Interlocked.Increment(ref i);

        public string Id {
            get { return id.ToString(); }
        }
    }
}
