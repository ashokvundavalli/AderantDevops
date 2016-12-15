using System.Diagnostics;

namespace ProjectReferenceTool {

    /// <summary>
    /// Represents an assembly in the packages folder.
    /// </summary>
    [DebuggerDisplay("{FileName} - {FolderDepth}")]
    public class PackageAssemblyInfo {

        /// <summary>
        /// Gets or sets the name of the file.
        /// </summary>
        public string FileName {
            get; set;
        }

        /// <summary>
        /// Gets or sets the name of the relative file.
        /// </summary>
        public string RelativeFileName {
            get; set;
        }

        /// <summary>
        /// Gets or sets the folder depth.
        /// </summary>
        public int FolderDepth {
            get; set;
        }

    }
}