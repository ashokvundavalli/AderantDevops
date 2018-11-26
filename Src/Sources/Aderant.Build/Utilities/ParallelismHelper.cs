using System;

namespace Aderant.Build.Utilities {
    internal class ParallelismHelper {
        public static int MaxDegreeOfParallelism() {
            return Environment.ProcessorCount < 6 ? Environment.ProcessorCount : 6;
        }
    }
}