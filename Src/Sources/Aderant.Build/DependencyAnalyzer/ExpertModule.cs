using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyResolver;

namespace Aderant.Build.DependencyAnalyzer {
    /// <summary>
    /// Represents a module in the expert code base
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class ExpertModule : IEquatable<ExpertModule>, IComparable<ExpertModule> {
        private string name;
        private IList<XAttribute> customAttributes;
        private ModuleType? type;
        private bool? replicateToDependencies = null;
        private bool? replaceVersionConstraints = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpertModule"/> class.
        /// </summary>
        public ExpertModule() {
            if (!string.IsNullOrEmpty(Branch)) {
                RepositoryType = RepositoryType.Folder;
            }
        }

        /// <summary>
        /// Creates a Expert Module from the specified element.
        /// </summary>
        /// <param name="element">The element.</param>
        public static ExpertModule Create(XElement element) {
            XAttribute name = element.Attribute("Name");

            if (string.IsNullOrEmpty(name?.Value)) {
                throw new ArgumentNullException(nameof(element), "No name element specified");
            }

            ModuleType moduleType = GetModuleType(name.Value);

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
        internal ExpertModule(XElement element)
            : this() {
            ExpertModuleMapper.MapFrom(element, this, out customAttributes);
        }

        internal ExpertModule(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
            }
            Name = name;
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
                if (type == null) {
                    type = GetModuleType(Name);
                }
                return type.Value;
            }
        }

        internal List<IDependencyRequirement> DependencyRequirements { get; set; }

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

            return String.Equals(name, other.name, StringComparison.OrdinalIgnoreCase)
                   && VersionRequirement == other.VersionRequirement
                   && string.Equals(DependencyGroup, other.DependencyGroup, StringComparison.OrdinalIgnoreCase);
        }

        private static readonly Dictionary<string, ModuleType> typeMap = new Dictionary<string, ModuleType>(StringComparer.OrdinalIgnoreCase) {
            { "Libraries", ModuleType.Library },
            { "Services", ModuleType.Service },
            { "Applications", ModuleType.Application },
            { "Workflow", ModuleType.Sample },
            { "Internal", ModuleType.InternalTool },
            { "Web", ModuleType.Web },
            { "Mobile", ModuleType.Web },
            { "Tests", ModuleType.Test },
        };

        public bool ExcludeFromPackaging { get; set; }

        public static ModuleType GetModuleType(string name) {
            string firstPart = name.Split('.')[0];

            ModuleType type;

            if (typeMap.TryGetValue(firstPart, out type)) {
                return type;
            }

            if (Enum.TryParse(firstPart, true, out type)) {
                return type;
            }

            // Help builds to /bin just like a third party module
            if (name.EndsWith(".HELP", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Help;
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
                    return (ICollection<XAttribute>)Enumerable.Empty<XAttribute>();
                }
                return new ReadOnlyCollection<XAttribute>(customAttributes);
            }
        }

        internal RepositoryType RepositoryType { get; set; }
        public bool Extract { get; set; }
        public string Target { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to replicate this instance to the dependencies folder (otherwise it just stays in package)
        /// </summary>
        public bool ReplicateToDependencies {
            get {
                return replicateToDependencies.GetValueOrDefault(true);
            }
            set {
                replicateToDependencies = value;
            }
        }

        public bool HasReplicateToDependenciesValue {
            get { return replicateToDependencies != null; }
        }

        public bool ReplaceVersionConstraint {
            get {
                return replaceVersionConstraints ?? false;
            }
            set {
                replaceVersionConstraints = value;
            }
        }

        internal VersionRequirement VersionRequirement {
            get {
                if (customAttributes != null) {
                    var version = customAttributes.FirstOrDefault(s => string.Equals(s.Name.LocalName, "Version"));
                    if (version != null) {
                        return new VersionRequirement {ConstraintExpression = ConstraintExpression.Parse(version.Value)};
                    }
                }

                return null;
            }
        }

        public string FullPath { get; set; }

        public string DependencyGroup { get; set; } = Constants.MainDependencyGroup;

        internal bool IsInDefaultDependencyGroup {
            get { return string.IsNullOrWhiteSpace(DependencyGroup) || string.Equals(DependencyGroup, Constants.MainDependencyGroup); }
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
            return Equals((ExpertModule)obj);
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

        public int CompareTo(ExpertModule other) {
            if (ReferenceEquals(this, other)) {
                return 0;
            }
            if (ReferenceEquals(null, other)) {
                return 1;
            }
            return string.Compare(name, other.name, StringComparison.OrdinalIgnoreCase);
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
        NuGet,
        RemoteFile,
    }
}