using System;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Packaging {
    internal class RootItemHandler {
        private readonly IFileSystem fs;

        public RootItemHandler(IFileSystem fs) {
            this.fs = fs;
        }

        public ExpertModule Module { get; set; }

        public void MoveContent(ProductAssemblyContext context, string contentDirectory) {
            if (string.IsNullOrEmpty(Module.Target)) {
                throw new InvalidOperationException("Cannot perform operation as the target property is not set on: " + Module.Name);
            }

            var index = Module.Target.IndexOf(';');
            if (index == -1) {
                string relativeDirectory = context.ResolvePackageRelativeDirectory(Module);
                fs.MoveDirectory(contentDirectory, relativeDirectory);
            } else {
                var destinations = context.ResolvePackageRelativeDestinationDirectories(Module);

                foreach (var destination in destinations) {
                    fs.CopyDirectory(contentDirectory, destination);
                }

                fs.DeleteDirectory(contentDirectory, true);
            }
            
        }
    }
}
