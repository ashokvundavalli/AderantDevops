using System.IO;

namespace Aderant.Build.Versioning {
    public interface IVersionAnalyzer {
        /// <summary>
        /// Determines whether this instance can analyze the specified file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        bool CanAnalyze(FileInfo file);
    }
}