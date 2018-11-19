using System;
using System.Diagnostics;
using Aderant.Build.Annotations;

namespace Aderant.Build {
    internal static class ErrorUtilities {

        /// <summary>
        /// Blows up if the reference is null.
        /// </summary>
        /// <param name="obj">The reference to check.</param>
        /// <param name="name">The parameter which cannot be null.</param>
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

        /// <summary>
        /// Blows up if the reference if the condition is false.
        /// </summary>
        internal static void VerifyThrowArgument(bool condition, string errorMessageTemplate, object arg0) {
            VerifyThrowArgument(condition, null, errorMessageTemplate, arg0);
        }

        internal static void VerifyThrowArgument(bool condition, Exception innerException, string resourceName, object arg0) {
            if (!condition) {
                ThrowArgument(innerException, resourceName, arg0);
            }
        }

        private static void ThrowArgument(Exception innerException, string resourceName, params object[] args) {
            throw new ArgumentException(string.Format(resourceName, args), innerException);
        }
    }
}
