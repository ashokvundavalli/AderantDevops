using System.ComponentModel.Composition;
using Aderant.Build.VersionControl;

namespace Aderant.Build.ProjectSystem {
    internal interface IProjectServices : IProjectCommonServices {
    }

    internal interface IProjectCommonServices {

        IFileSystem2 FileSystem { get; }

        IVersionControlService VersionControl { get; }
    }

    [Export(typeof(IProjectServices))]
    internal class ProjectServices : IProjectServices {

        public ProjectServices() {
        }

        [Import]
        public IFileSystem2 FileSystem { get; private set; }

        [Import]
        public IVersionControlService VersionControl { get; private set; }
    }
}
