namespace Aderant.Build.SeedPackageValidation {
    internal abstract class Error {
        protected readonly string fileName;

        protected Error(string fileName) {
            this.fileName = fileName;
        }
    }
}