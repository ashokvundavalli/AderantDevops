using System;
using System.Diagnostics;
using Aderant.Build.Annotations;

namespace Aderant.Build {
    internal static class ErrorUtilities {

        [AssertionMethod]
        [DebuggerStepThrough]
        public static void IsNotNull<T>(
            [AssertionCondition(AssertionConditionType.IS_NOT_NULL)]
            T obj,
            string name) where T : class {
            if (obj == null) {
                throw new ArgumentNullException(name);
            }
        }
    }
}
