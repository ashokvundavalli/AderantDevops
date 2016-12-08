using System;
using System.Collections.Generic;
using System.IO;

namespace Aderant.Build {
    public interface IFileSystem2 { 
        string Root { get; }

        void DeleteDirectory(string path, bool recursive);

        IEnumerable<string> GetFiles(string path, string filter, bool recursive);

        IEnumerable<string> GetDirectories(string path, bool recursive = false);

        string GetFullPath(string path);

        void DeleteFile(string path);

        bool FileExists(string path);

        bool DirectoryExists(string path);

        void AddFile(string path, Stream stream);

        void AddFile(string path, Action<Stream> writeToStream);

        void MakeFileWritable(string path);

        void MoveFile(string source, string destination);

        Stream CreateFile(string path);

        Stream OpenFile(string path);

        Stream OpenFileForWrite(string path);

        DateTimeOffset GetLastModified(string path);

        DateTimeOffset GetCreated(string path);

        DateTimeOffset GetLastAccessed(string path);

        void CopyDirectory(string source, string destination);

        void MoveDirectory(string source, string destination);
    }
}