using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Aderant.Build {

    internal abstract class ArgumentProcessor {
        /// <summary>
        /// Processes the given workflow argument. Performs additional argument creation or manipulation for specific build use cases.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <param name="arguments">The MSBuild argument collection.</param>
        internal abstract void ProcessArgument(string name, object value, IList<string> arguments);

        /// <summary>
        /// Processes the given workflow argument. Performs additional argument creation or manipulation for specific build use cases.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <param name="arguments">The MSBuild argument collection.</param>
        internal static void ProcessArguments(string name, object value, IList<string> arguments) {
            new ManifestPathProcessor().ProcessArgument(name, value, arguments);
        }
    }

    internal sealed class ManifestPathProcessor : ArgumentProcessor {
        internal override void ProcessArgument(string name, object value, IList<string> arguments) {
            if (name == "EnvironmentManifestPath") {
                string path = value as string;
                if (!string.IsNullOrEmpty(path)) {
                    string directoryName = Path.GetDirectoryName(path);
                    if (directoryName != null && Directory.Exists(directoryName)) {
                        arguments.Add(string.Format("/p:{0}=\"{1}\"", "RemoteExpertSourceDirectory", directoryName.TrimEnd((Path.DirectorySeparatorChar))));
                    }

                    if (File.Exists(path)) {
                        string manifestText = File.ReadAllText(path);
                        XDocument manifest = XDocument.Parse(manifestText);

                        XElement element = manifest.Element("environment");
                        if (element != null) {
                            XAttribute attribute = element.Attribute("sourcePath");
                            string sourcePath = attribute.Value;

                            if (!string.IsNullOrEmpty(sourcePath)) {
                                // Go up from ExpertSource to the "Binaries" directory
                                sourcePath = Path.GetDirectoryName(sourcePath);
                                if (sourcePath != null) {
                                    arguments.Add(string.Format("/p:{0}=\"{1}\"", "LocalExpertBinariesPathOnRemote", sourcePath.TrimEnd((Path.DirectorySeparatorChar))));
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}