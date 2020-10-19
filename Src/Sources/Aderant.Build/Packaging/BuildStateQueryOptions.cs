namespace Aderant.Build.Packaging {
    internal class BuildStateQueryOptions {
        public string BuildFlavor { get; set; }
        public bool SkipNugetPackageHashCheck { get; set; }
    }
}
