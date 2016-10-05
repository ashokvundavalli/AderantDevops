namespace Aderant.Build.DependencyAnalyzer {
    internal static class ExpertModuleExtensions {
        /// <summary>
        /// Determines whether the module name is one of the specified types.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="types">The types.</param>
        public static bool IsOneOf(this string name, params ModuleType[] types) {
            foreach (var type in types) {
                if (ExpertModule.GetModuleType(name) == type) {
                    return true;
                }
            }

            return false;
        }
    }
}