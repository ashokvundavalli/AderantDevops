using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks.Dataflow;
using Aderant.Build.Packaging;
using static Aderant.Build.PhysicalFileSystem;

namespace Aderant.Build {
    public interface IFileSystem2 : IFileSystem {
        string Root { get; }
    }

    public interface IFileSystem : IFileSystemLinkOps {
        void DeleteDirectory(string path, bool recursive);

        /// <summary>
        /// Returns the full path of files present in a directory that match the <see cref="inclusiveFilter"/>.
        /// </summary>
        IEnumerable<string> GetFiles(string path, string inclusiveFilter = null, bool recursive = true);

        IEnumerable<string> GetDirectories(string path, bool recursive = false);

        string GetFullPath(string path);

        string GetParent(string path);

        void DeleteFile(string path);

        bool FileExists(string path);

        bool DirectoryExists(string path);

        string CreateDirectory(string path);

        string AddFile(string path, Stream stream);

        string AddFile(string path, Action<Stream> writeToStream);

        void MakeFileWritable(string path);

        DateTime GetLastAccessTimeUtc(string path);

        void MoveFile(string source, string destination);

        void CopyFile(string source, string destination);

        void CopyFile(string source, string destination, bool overwrite);

        Stream CreateFile(string path);

        Stream OpenFile(string path);

        Stream OpenFileForWrite(string path);

        DateTimeOffset GetLastModified(string path);

        DateTimeOffset GetCreated(string path);

        DateTimeOffset GetLastAccessed(string path);

        void CopyDirectory(string source, string destination);

        void MoveDirectory(string source, string destination);

        /// <summary>
        /// Performs a bulk file copy operation on the specified path specifications.
        /// </summary>
        /// <param name="pathSpecs">The copy specification</param>
        /// <param name="overwrite">If the destination exists the file will be overwritten.</param>
        /// <param name="useSymlinks">The copy process can be optimized using symlinks.</param>
        /// <param name="useHardlinks">The copy process can be optimized using hardlink.</param>
        ActionBlock<PathSpec> BulkCopy(IEnumerable<PathSpec> pathSpecs, bool overwrite, bool useSymlinks = false, bool useHardlinks = false);

        void ExtractZipToDirectory(string sourceArchiveFileName, string destination, bool overwrite = false);
    }
}
