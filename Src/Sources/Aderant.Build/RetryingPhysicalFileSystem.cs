using System.IO;
using System.Threading;

namespace Aderant.Build {
    internal class RetryingPhysicalFileSystem : PhysicalFileSystem {
        private const int NumberOfRetries = 3;
        private const int DelayOnRetry = 1000;

        public RetryingPhysicalFileSystem()
            : base() {
        }

        public override void CopyDirectory(string source, string destination) {
            for (int i = 1; i <= NumberOfRetries; ++i) {
                try {
                    base.CopyDirectory(source, destination);
                    break; // When done we can break loop
                } catch (IOException) {
                    if (i == NumberOfRetries) {
                        throw;
                    }

                    Thread.Sleep(DelayOnRetry);
                }
            }
        }
    }
}
