using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyAnalyzer {
    /// <summary>
    /// Represents a Module under the expert source tree
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class ExpertModule : IEquatable<ExpertModule> {
        internal IFileSystem2 FileSystem { get; private set; }

        private string name;
        private List<XAttribute> customAttributes;
        private ModuleType type;
        private bool hasModuleType = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpertModule"/> class.
        /// </summary>
        public ExpertModule() {
        }

        /// <summary>
        /// Creates a Expert Module from the specified element.
        /// </summary>
        /// <param name="element">The element.</param>
        public static ExpertModule Create(XElement element) {
            var name = element.Attribute("Name");

            if (string.IsNullOrEmpty(name?.Value)) {
                throw new ArgumentNullException(nameof(element), "No name element specified");
            }

            var moduleType = GetModuleType(name.Value);
            if (moduleType == ModuleType.ThirdParty || moduleType == ModuleType.Help) {
                return new ThirdPartyModule(element);
            }

            if (moduleType == ModuleType.Web) {
                return new WebModule(element);
            }
            return new ExpertModule(element);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpertModule"/> class from a Product Manifest element.
        /// </summary>
        /// <param name="element">The product manifest module element.</param>
        internal ExpertModule(XElement element) : this() {
            customAttributes = element.Attributes().ToList();

            SetPropertyValue(value => name = value, element, "Name");
            SetPropertyValue(value => AssemblyVersion = value, element, "AssemblyVersion");
            SetPropertyValue(value => FileVersion = value, element, "FileVersion");
            SetPropertyValue(value => Branch = value, element, "Path");
            SetPropertyValue(SetGetAction, element, "GetAction");

            if (ModuleType == ModuleType.ThirdParty) {
                RepositoryType = RepositoryType.NuGet;
            }

            if (GetAction == GetAction.NuGet) {
                RepositoryType = RepositoryType.NuGet;
            }
        }

        private void SetPropertyValue(Action<string> setAction, XElement element, string attributeName) {
            XAttribute attribute = element.Attribute(attributeName);
            if (attribute != null) {
                setAction(attribute.Value);

                // Remove this attribute from the collection as it isn't custom
                customAttributes.Remove(attribute);
            }
        }

        private void SetGetAction(string value) {
            if (!string.IsNullOrEmpty(value)) {
                GetAction = (GetAction) Enum.Parse(typeof (GetAction), value.Replace("_", "-"), true);
            }
        }

        /// <summary>
        /// Gets or sets the name of the module.
        /// </summary>
        /// <value>The name.</value>
        public string Name {
            get { return name; }
            set { name = Path.GetFileName(value); }
        }

        /// <summary>
        /// Gets the type of the module.
        /// </summary>
        /// <value>The type of the module.</value>
        public ModuleType ModuleType {
            get {
                if (!hasModuleType) {
                    type = GetModuleType(Name);
                    hasModuleType = true;
                }
                return type;
            }
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(ExpertModule other) {
            if (ModuleType != other.ModuleType) {
                return false;
            }

            if (Char.IsUpper(Name[0]) && char.IsUpper(other.name[0])) {
                if (Name[0] != other.name[0]) {
                    return false;
                }
            }

            return String.Equals(name, other.name, StringComparison.OrdinalIgnoreCase);
        }

        public static ModuleType GetModuleType(string name) {
            if (name.StartsWith("LIBRARIES", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Library;
            }
            if (name.StartsWith("SERVICES", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Service;
            }
            if (name.StartsWith("APPLICATIONS", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Application;
            }
            if (name.StartsWith("WORKFLOW", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Sample;
            }
            if (name.StartsWith("SDK", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.SDK;
            }

            if (name.StartsWith("THIRDPARTY", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.ThirdParty;
            }

            // Help builds to /bin just like a third party module
            if (name.EndsWith(".HELP", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Help;
            }

            if (name.StartsWith("BUILD", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Build;
            }
            if (name.StartsWith("INTERNAL", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.InternalTool;
            }
            if (name.StartsWith("WEB", StringComparison.OrdinalIgnoreCase) || name.StartsWith("MOBILE", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Web;
            }
            if (name.StartsWith("INSTALLS", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Installs;
            }
            if (name.StartsWith("TESTS", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Test;
            }
            if (name.StartsWith("PERFORMANCE", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Performance;
            }
            if (name.Equals("DATABASE", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Database;
            }

            return ModuleType.Unknown;
        }

        public static bool IsNonProductModule(ModuleType type) {
            return (type == ModuleType.Build || type == ModuleType.Performance || type == ModuleType.Test);
        }

        /// <summary>
        /// Gets or sets the assembly version of this module
        /// </summary>
        /// <value>
        /// The assembly version.
        /// </value>
        public string AssemblyVersion { get; set; }

        /// <summary>
        /// Gets or sets the assembly version of this module
        /// </summary>
        /// <value>
        /// The assembly version.
        /// </value>
        public string FileVersion { get; set; }

        /// <summary>
        /// Gets or sets the branch for which this module originates.
        /// </summary>
        /// <value>
        /// The branch.
        /// </value>
        public string Branch { get; internal set; }

        public GetAction GetAction { get; set; }

        /// <summary>
        /// Gets the custom attributes associated with this instance.
        /// </summary>
        /// <value>
        /// The custom attributes.
        /// </value>
        public ICollection<XAttribute> CustomAttributes {
            get {
                if (customAttributes == null) {
                    return (ICollection<XAttribute>) Enumerable.Empty<XAttribute>();
                }
                return new ReadOnlyCollection<XAttribute>(customAttributes);
            }
        }

        internal RepositoryType RepositoryType { get; set; }
        internal VersionRequirement VersionRequirement { get; set; }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with this instance.</param>
        /// <returns>
        /// 	<c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj) {
            if (!(obj is ExpertModule)) {
                return false;
            }
            return Equals((ExpertModule) obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode() {
            if (name != null) {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(name);
            }
         
            return string.Empty.GetHashCode();
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString() {
            return Name;
        }

        public string GetPathToBinaries(string dropLocationDirectory) {
            if (!string.IsNullOrEmpty(Branch)) {
                if (dropLocationDirectory.StartsWith("\\")) {
                    dropLocationDirectory = AdjustDropPathToBranch(dropLocationDirectory, this);
                }
            }

            return GetBinariesPath(dropLocationDirectory);
        }

        protected static string AdjustDropPathToBranch(string dropLocationDirectory, ExpertModule module) {
            return PathHelper.ChangeBranch(dropLocationDirectory, module.Branch);
        }

        protected virtual string GetBinariesPath(string dropLocation) {
            if (AssemblyVersion == null) {
                throw new ArgumentNullException(nameof(AssemblyVersion), string.Format(CultureInfo.InvariantCulture, "The module {0} from source {1} does not have an assembly version specified.", Name, RepositoryType));
            }

            dropLocation = Path.Combine(dropLocation, Name, AssemblyVersion);

            IFileSystem2 fs = new PhysicalFileSystem(dropLocation);

            if (!fs.DirectoryExists(dropLocation)) {
                throw new BuildNotFoundException("No drop location for " + Name);
            }

            var entries = fs.GetDirectories(dropLocation).ToArray();
            string[] orderedBuilds = OrderBuildsByBuildNumber(entries);

            foreach (string build in orderedBuilds) {
                var files = fs.GetFiles(build, null, false);

                foreach (string file in files) {
                    if (file.IndexOf("build.failed", StringComparison.OrdinalIgnoreCase) >= 0) {
                        break;
                    }

                    if (file.IndexOf("build.succeeded", StringComparison.OrdinalIgnoreCase) >= 0) {
                        string binariesPath;
                        if (HasBinariesFolder(fs.GetFullPath(build), fs, out binariesPath)) {
                            return binariesPath;
                        }
                    }
                }

                string buildLog = files.FirstOrDefault(f => f.EndsWith("BuildLog.txt", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(buildLog)) {
                    if (CheckLog(fs.GetFullPath(buildLog))) {
                        string binariesPath;
                        if (HasBinariesFolder(fs.GetFullPath(build), fs, out binariesPath)) {
                            return binariesPath;
                        }
                    }
                }
            }

            throw new BuildNotFoundException("No latest build found for " + Name);
        }

        private static bool HasBinariesFolder(string build, IFileSystem2 fileSystem, out string binariesFolder) {
            string binaries = Path.Combine(build, "Bin", "Module");

            if (fileSystem.DirectoryExists(binaries)) {
                // Guard against empty drop folders, if we run into one it will cause lots of runtime problems
                // due to missing binaries.
                if (fileSystem.GetFiles(binaries, "*", false).Any()) {
                    binariesFolder = binaries;
                    return true;
                }
            }
            binariesFolder = null;
            return false;
        }

        internal static string[] OrderBuildsByBuildNumber(string[] entries) {
            // Converts the dotted version into an int64 to get the highest build number
            // This differs from the PowerShell implementation that padded each part of the version string and used an alphanumeric sort

            List<KeyValuePair<Version, string>> numbers = new List<KeyValuePair<Version, string>>(entries.Length);
            foreach (var entry in entries) {
                string directoryName = Path.GetFileName(entry);
                Version version;
                if (System.Version.TryParse(directoryName, out version)) {
                    numbers.Add(new KeyValuePair<Version, string>(version, entry));
                }
            }

            return numbers.OrderByDescending(d => d.Key).Select(s => s.Value).ToArray();
        }

        internal static bool CheckLog(string logfile) {
            // UCS-2 Little Endian files sometimes get created which makes it difficult
            // to produce an efficient solution for reading a text file backwards
            IEnumerable<string> lineReader = File.ReadAllLines(logfile).Reverse().Take(10);

            int i = 0;
            foreach (string s in lineReader) {
                if (i > 10) {
                    break;
                }

                if (s.IndexOf("0 Error(s)", StringComparison.OrdinalIgnoreCase) >= 0) {
                    return true;
                }

                i++;
            }

            return false;
        }

        public virtual void Deploy(string moduleDependenciesDirectory) {
        }
    }


    /// <summary>
    /// Controls the behaviour of the get action
    /// </summary>
    public enum GetAction {
        None,

        Branch,

        local,
        local_external_module,
        current_branch_external_module,
        other_branch_external_module,
        other_branch,
        current_branch,
        specific_path,
        specific_path_external_module,

        SpecificDropLocation,
        NuGet
    }
}

