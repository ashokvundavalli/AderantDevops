namespace Aderant.Build.DependencyAnalyzer {
    internal static class ListHelper {
        /// <summary>
        /// A reasonable starting pint for lists that will hold directory entries.
        /// Source trees typically contain many folders so we wan to have a good default capacity.
        /// </summary>
        internal const int DefaultDirectoryListCapacity = 64;
    }
}
