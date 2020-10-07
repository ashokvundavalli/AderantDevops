namespace Aderant.Build {
    public interface IFileSystemLinkOps {

        /// <summary>
        /// Determines whether the specified link path is symlink.
        /// </summary>
        /// <param name="linkPath">The link path.</param>
        /// <returns><c>true</c> if the specified link path is symlink; otherwise, <c>false</c>.</returns>
        bool IsSymlink(string linkPath);

        /// <summary>
        /// Makes a file link.
        /// </summary>
        /// <param name="linkPath">The link path.</param>
        /// <param name="actualFilePath">The actual file path.</param>
        /// <param name="overwrite">If true overwrites an existing reparse point or empty directory</param>
        void CreateFileHardLink(string linkPath, string actualFilePath, bool overwrite = false);


        /// <summary>
        /// Makes a file link.
        /// </summary>
        /// <param name="linkPath">The link path.</param>
        /// <param name="actualFilePath">The actual file path.</param>
        /// <param name="overwrite">If true overwrites an existing reparse point or empty directory</param>
        void CreateFileSymlink(string linkPath, string actualFilePath, bool overwrite = false);

        /// <summary>
        /// Creates a junction point from the specified directory to the specified target directory.
        /// </summary>
        /// <param name="linkPath">The link path.</param>
        /// <param name="actualFolderPath">The actual folder path.</param>
        /// <param name="overwrite">If true overwrites an existing reparse point or empty directory</param>
        void CreateDirectoryLink(string linkPath, string actualFolderPath, bool overwrite = false);
    }
}
