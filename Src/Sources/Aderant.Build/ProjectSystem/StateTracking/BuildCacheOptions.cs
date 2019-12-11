namespace Aderant.Build.ProjectSystem.StateTracking {
    internal enum  BuildCacheOptions {
        /// <summary>
        /// If a project within a cone is dirty, then the entire cone is dirty and the cache is disabled.
        /// </summary>
        DisableCacheWhenProjectChanged,

        /// <summary>
        /// If a project within a cone is dirty, then the cache is still used and outputs from individual projects
        /// are merged with the cached build.
        /// </summary>
        DoNotDisableCacheWhenProjectChanged
    }
}