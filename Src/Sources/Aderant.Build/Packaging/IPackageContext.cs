namespace Aderant.Build {
    internal interface IPackageContext {
        bool IncludeDevelopmentDependencies { get; }
        bool AllowExternalPackages { get; }
    }
}