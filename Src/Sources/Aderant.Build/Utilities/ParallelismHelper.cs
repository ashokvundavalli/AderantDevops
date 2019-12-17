using System;

namespace Aderant.Build.Utilities {
    internal static class ParallelismHelper {
        private static int maxDegreeOfParallelism;

        public static int MaxDegreeOfParallelism {
            get {
                if (maxDegreeOfParallelism == 0) {
                    maxDegreeOfParallelism = Environment.ProcessorCount < 6 ? Environment.ProcessorCount : 6;
                }

                return maxDegreeOfParallelism;
            }
        }
    }
}
