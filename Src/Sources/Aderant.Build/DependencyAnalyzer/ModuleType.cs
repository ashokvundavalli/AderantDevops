namespace Aderant.Build.DependencyAnalyzer {

    /// <summary>
    /// The type of a module
    /// </summary>
    public enum ModuleType {
        /// <summary>
        /// 
        /// </summary>
        Library,
        /// <summary>
        /// 
        /// </summary>
        Service,
        /// <summary>
        /// 
        /// </summary>
        Application,
        /// <summary>
        /// 
        /// </summary>
        SDK,
        /// <summary>
        /// 
        /// </summary>
        Sample,
        /// <summary>
        /// 
        /// </summary>
        ThirdParty,
        /// <summary>
        /// 
        /// </summary>
        Build,
        /// <summary>
        /// 
        /// </summary>
        Unknown,
        Database,
        InternalTool,
        /// <summary>
        /// The web module type (MVC website)
        /// </summary>
        Web,
        Installs,
        /// <summary>
        /// The tests module type
        /// </summary>
        Test,
        Performance
    }
}