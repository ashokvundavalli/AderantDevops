using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aderant.Build.DependencyResolver;

namespace Aderant.Build.DependencyAnalyzer {

    /// <summary>
    /// Represents a module in the expert code base
    /// </summary>
    [DebuggerDisplay("Module: {Name} ({DebuggerDisplayNames})")]
    public class ExpertModule : IEquatable<ExpertModule>, IComparable<ExpertModule>, IDependencyRef {
        
        private string name;
        private readonly IList<XAttribute> customAttributes;
        private ModuleType? type;
        public string SolutionRoot;
        public readonly string[] Names;
        public DependencyManifest Manifest;
        private HashSet<IDependencyRef> dependsOn;

        internal string DebuggerDisplayNames {
            get {
                if (Names != null) {
                    return string.Join(", ", Names);
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpertModule"/> class.
        /// </summary>
        internal ExpertModule() {
        }

        public ExpertModule(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
            }

            Name = name;
        }

        public ExpertModule(string solutionRoot, string[] names, DependencyManifest manifest) {
            this.SolutionRoot = solutionRoot;
            this.Names = names;
            this.Manifest = manifest;
            if (names != null) {
                this.Name = !string.IsNullOrWhiteSpace(names[0]) ? names[0] : string.Empty;
            }
        }

        internal ICollection<IDependencyRef> DependsOn {
            get {
                if (dependsOn == null) {
                    if (Manifest != null) {
                        dependsOn = new HashSet<IDependencyRef>(Manifest.ReferencedModules.ToList<IDependencyRef>());
                    } else {
                        dependsOn = new HashSet<IDependencyRef>();
                    }
                }
                return dependsOn;
            }
            set { dependsOn = new HashSet<IDependencyRef>(value); }
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

        /// <summary>
        /// Gets or sets the name of the module.
        /// </summary>
        /// <value>The name.</value>
        public string Name {
            get { return name; }
            set { name = Path.GetFileName(value); }
        }

        public void Accept(GraphVisitorBase visitor, StreamWriter outputFile) {
            visitor.Visit(this, outputFile);
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

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(ExpertModule other) {
            return other != null && String.Equals(name, other.name, StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, ModuleType> typeMap = new Dictionary<string, ModuleType>(StringComparer.OrdinalIgnoreCase) {
            { "Libraries", ModuleType.Library },
            { "Services", ModuleType.Service },
            { "Applications", ModuleType.Application },
            { "Workflow", ModuleType.Sample },
            { "Internal", ModuleType.InternalTool },
            { "Web", ModuleType.Web },
            { "Mobile", ModuleType.Web },
            { "Tests", ModuleType.Test },
        };



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

        public bool Extract { get; set; }
        public string Target { get; set; }
        public PackageType PackageRootRelativeDirectory { get; set; }

        internal VersionRequirement VersionRequirement {
            get {
                var version = customAttributes.FirstOrDefault(s => string.Equals(s.Name.LocalName, "Version"));
                if (version != null) {
                    return new VersionRequirement { ConstraintExpression = version.Value };
                }
                return null;
            }
        }

        public RepositoryType RepositoryType { get; set; }

        string IDependencyRef.Name => throw new NotImplementedException();

        ICollection<IDependencyRef> IDependencyRef.DependsOn => throw new NotImplementedException();

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

        public int CompareTo(ExpertModule other) {
            if (other == null) {
                throw new ArgumentNullException(nameof(other));
            }

            return string.Compare(name, other.name, StringComparison.OrdinalIgnoreCase);
        }

        private string PadNumbers(string input) {
            return Regex.Replace(input, "[0-9]+", match => match.Value.PadLeft(10, '0'));
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

        public static ExpertModule Create(string solutionRoot, string[] names, DependencyManifest manifest) {
            return new ExpertModule(solutionRoot, names, manifest);
        }

        internal bool Equals(IDependencyRef other) {
            ExpertModule module = other as ExpertModule;
            return Equals(module);
        }

        public bool Match(string depName) {
            foreach (var name in Names) {
                if (string.Equals(name, depName, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        void IDependencyRef.Accept(GraphVisitorBase visitor, StreamWriter outputFile) {
            throw new NotImplementedException();
        }

        bool IEquatable<IDependencyRef>.Equals(IDependencyRef other) {
            throw new NotImplementedException();
        }
    }

    public abstract class GraphVisitorBase {
        public virtual void Visit(ExpertModule expertModule, StreamWriter outputFile) {
            
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