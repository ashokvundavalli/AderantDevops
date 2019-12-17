namespace Aderant.Build.VersionControl.Model {
    public interface ISourceChange {
        string Path { get; }
        string FullPath { get; }
        FileStatus Status { get; }
    }
}
