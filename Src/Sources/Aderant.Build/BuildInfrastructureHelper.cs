using System.IO;

namespace Aderant.Build {
    internal static class BuildInfrastructureHelper {

        internal static string PathToBuildScriptsFromModules = Path.Combine(BuildConstants.BuildInfrastructureDirectory, "Src", "Build");
    }
}