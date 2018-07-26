using System;
using System.Collections.Generic;
using System.IO;

namespace Aderant.Build {

    public interface IFileSystem2 : IFileSystem {
        string Root { get; }
    }

    public interface IFileSystem {
    
        void DeleteDirectory(string path, bool recursive);

        /// <summary>
        /// Returns the full path of files present in a directory that match the <see cref="inclusiveFilter"/>.
        /// </summary>
        IEnumerable<string> GetFiles(string path, string inclusiveFilter, bool recursive);

        IEnumerable<string> GetDirectories(string path, bool recursive = false);

        string GetFullPath(string path);

        string GetParent(string path);

        void DeleteFile(string path);

        bool FileExists(string path);

        bool DirectoryExists(string path);

        string AddFile(string path, Stream stream);

        string AddFile(string path, Action<Stream> writeToStream);

        void MakeFileWritable(string path);

        void MoveFile(string source, string destination);

        void CopyFile(string source, string destination);
        
        Stream CreateFile(string path);

        Stream OpenFile(string path);

        Stream OpenFileForWrite(string path);

        DateTimeOffset GetLastModified(string path);

        DateTimeOffset GetCreated(string path);

        DateTimeOffset GetLastAccessed(string path);

        void CopyDirectory(string source, string destination);

        void MoveDirectory(string source, string destination);

        /// <summary>
        /// Searches upward for a directory containing the specified file, beginning in the specified directory.
        /// </summary>
        string GetDirectoryNameOfFileAbove(string startingDirectory, string fileName, string[] ceilingDirectories = null);

        /// <summary>
        /// Searches upward for a directory containing the specified files included by the filter, beginning in the specified directory.
        /// </summary>
        IEnumerable<string> GetDirectoryNameOfFilesAbove(string startingDirectory, string filter, IReadOnlyCollection<string> ceilingDirectories = null);
    }
}
