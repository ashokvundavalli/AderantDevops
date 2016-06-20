using System.IO;

namespace Aderant.Build.Packaging {
    internal interface IPackageFile {
        string FullPath { get; }
        Stream GetStream();
    }
}