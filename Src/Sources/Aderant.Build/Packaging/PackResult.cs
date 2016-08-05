namespace Aderant.Build.Packaging {
    public sealed class PackResult {
        private readonly PackSpecification spec;

        internal PackResult(PackSpecification spec) {
            this.spec = spec;
        }

        public string OutputPath {
            get { return spec.OutputPath; }
        }
    }
}