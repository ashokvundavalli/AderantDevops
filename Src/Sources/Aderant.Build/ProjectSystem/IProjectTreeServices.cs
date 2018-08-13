using System.ComponentModel.Composition;

namespace Aderant.Build.ProjectSystem {
    internal interface IProjectServices : IProjectCommonServices {
    }

    internal interface IProjectCommonServices {

        IFileSystem2 FileSystem { get; }
    }

    [Export(typeof(IProjectServices))]
    internal class ProjectServices : IProjectServices {

        [Import]
        public IFileSystem2 FileSystem { get; internal set; }
    }
}
