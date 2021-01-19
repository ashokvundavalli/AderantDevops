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
        /// Blows up if the condition is false.
        /// </summary>
        internal static void VerifyThrowArgument(bool condition, string errorMessageTemplate, params object[] args) {
            VerifyThrowArgument(condition, null, errorMessageTemplate, args);
        }

        internal static void VerifyThrowArgument(bool condition, Exception innerException, string resourceName, params object[] args) {
            if (!condition) {
                ThrowArgument(innerException, resourceName, args);
            }
        }

        private static void ThrowArgument(Exception innerException, string resourceName, params object[] args) {
            throw new ArgumentException(string.Format(resourceName, args), innerException);
        }
    }
}
