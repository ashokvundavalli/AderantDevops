using System;
using System.IO;

namespace Aderant.Build.Packaging {
    internal class PhysicalPackageFile : IPackageFile {
        private readonly string file;

        /// <summary>
        /// Initializes a new instance of the <see cref="PhysicalPackageFile"/> class.
        /// </summary>
        /// <param name="file">The file.</param>
        public PhysicalPackageFile(string file) {
            this.file = file;
        }

        public FileVersionDescriptor Version { get; set; }

        public string FullPath {
            get { return file; }
        }

        public Stream GetStream() {
            if (OpenFile == null) {
                return File.OpenRead(FullPath);
            }
            return OpenFile(FullPath);
        }

        internal Func<string, Stream> OpenFile { get; set; }
    }
}