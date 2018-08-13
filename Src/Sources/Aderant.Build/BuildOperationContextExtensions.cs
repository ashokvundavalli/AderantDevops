using Aderant.Build.Ipc;

namespace Aderant.Build {
    internal static class BuildOperationContextExtensions {

        internal static string Publish(this BuildOperationContext context, string name) {
            return MemoryMappedFileReaderWriter.WriteData(name, context);
        }

        internal static BuildOperationContext GetBuildOperationContext(this string context) {
            return (BuildOperationContext)MemoryMappedFileReaderWriter.Read(context);
        }
    }
}
