using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Xaml.Permissions;
using System.Xml.Linq;
using Aderant.Build.Providers;
using MiscUtil.IO;

namespace Aderant.Build.DependencyAnalyzer {
    /// <summary>
    /// Represents a Module under the expert source tree
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class ExpertModule : IEquatable<ExpertModule> {
        internal FileSystem FileSystem { get; private set; }

        private string name;
        private List<XAttribute> customAttributes;

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

            if (name == null || string.IsNullOrEmpty(name.Value)) {
                throw new ArgumentNullException("element", "No name element specified");
            }

            if (GetModuleType(name.Value) == ModuleType.ThirdParty) {
                return new ThirdPartyModule(element);
            }

            if (GetModuleType(name.Value) == ModuleType.Web) {
                return new WebModule(element);
            }
            return new ExpertModule(element);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpertModule"/> class from a Product Manifest element.
        /// </summary>
        /// <param name="element">The product manifest module element.</param>
        public ExpertModule(XElement element) : this(FileSystem.Default) {
            customAttributes = element.Attributes().ToList();

            SetPropertyValue(value => name = value, element, "Name");
            SetPropertyValue(value => AssemblyVersion = value, element, "AssemblyVersion");
            SetPropertyValue(value => FileVersion = value, element, "FileVersion");
            SetPropertyValue(value => Branch = value, element, "Path");
            SetPropertyValue(SetGetAction, element, "GetAction");
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

        private ExpertModule(FileSystem fileSystem) {
            this.FileSystem = fileSystem;
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
            get { return GetModuleType(Name); }
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(ExpertModule other) {
            return String.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
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
                return ModuleType.ThirdParty;
            }

            if (name.StartsWith("BUILD", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Build;
            }
            if (name.StartsWith("INTERNAL", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.InternalTool;
            }
            if (name.StartsWith("WEB", StringComparison.OrdinalIgnoreCase)) {
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
                return name.ToUpperInvariant().GetHashCode();
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
            dropLocation = Path.Combine(dropLocation, Name, AssemblyVersion);

            DirectoryOperations directoryOperations = FileSystem.Directory;

            string[] entries = directoryOperations.GetFileSystemEntries(dropLocation);

            var orderedBuilds = entries.OrderByDescending(d => d);
            foreach (string build in orderedBuilds) {
                string[] files = directoryOperations.GetFileSystemEntries(build);

                foreach (string file in files) {
                    if (file.EndsWith("BuildLog.txt", StringComparison.OrdinalIgnoreCase)) {
                        if (CheckLog(file)) {

                            string binaries = Path.Combine(build, "Bin", "Module");

                            if (directoryOperations.Exists(binaries)) {
                                return binaries;
                            }
                        }
                    }
                }
            }

            throw new BuildNotFoundException("No latest build found for " + Name);
        }

        internal static bool CheckLog(string logfile) {
            ReverseLineReader lineReader = new ReverseLineReader(logfile);

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

        SpecificDropLocation
    }
}

