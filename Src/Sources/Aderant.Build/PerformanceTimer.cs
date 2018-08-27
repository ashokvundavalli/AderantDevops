using System;
using System.Diagnostics;

namespace Aderant.Build {
    internal struct PerformanceTimer : IDisposable {
        private readonly Stopwatch stopwatch;
        private readonly Action<long> callback;

        public PerformanceTimer(Action<long> callback)
            : this() {
            this.callback = callback;

            stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Starts a timing a section of code.
        /// Invokes your call back when done with the elapsed milliseconds
        /// </summary>
        public static PerformanceTimer Start(Action<long> callback) {
            return new PerformanceTimer(callback);
        }

        public void Dispose() {
            stopwatch.Stop();

            if (Duration > 0) {
                if (callback != null) {
                    callback(Duration);
                }
            }
        }

        public long Duration {
            get { return this.stopwatch.ElapsedMilliseconds; }
        }
    }
}
