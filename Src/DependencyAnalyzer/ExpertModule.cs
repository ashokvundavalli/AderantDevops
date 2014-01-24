using System;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

namespace DependencyAnalyzer {
    /// <summary>
    /// Represents a Module under the expert source tree
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class ExpertModule : IEquatable<ExpertModule> {
        private string name;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpertModule"/> class.
        /// </summary>
        public ExpertModule() {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpertModule"/> class from a Product Manifest element.
        /// </summary>
        /// <param name="moduleElement">The product manifest module element.</param>
        public ExpertModule(XElement moduleElement) {
            Name = moduleElement.Attribute("Name") == null ? null : moduleElement.Attribute("Name").Value;
            AssemblyVersion = moduleElement.Attribute("AssemblyVersion") == null ? null : moduleElement.Attribute("AssemblyVersion").Value;
            FileVersion = moduleElement.Attribute("FileVersion") == null ? null : moduleElement.Attribute("FileVersion").Value;
            Branch = moduleElement.Attribute("Path") == null ? null : moduleElement.Attribute("Path").Value;
        }

        /// <summary>
        /// Gets or sets the name of the module.
        /// </summary>
        /// <value>The name.</value>
        public string Name {
            get {
                return name;
            }
            set {
                name = Path.GetFileName(value);
            }
        }

        /// <summary>
        /// Gets the type of the module.
        /// </summary>
        /// <value>The type of the module.</value>
        public ModuleType ModuleType {
            get {
                return GetModuleType();
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
            return string.Equals(Name, other.Name, StringComparison.InvariantCultureIgnoreCase);
        }

        protected virtual ModuleType GetModuleType() {
            if (Name.StartsWith("LIBRARIES", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Library;
            }
            if (Name.StartsWith("SERVICES", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Service;
            }
            if (Name.StartsWith("APPLICATIONS", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Application;
            }
            if (Name.StartsWith("WORKFLOW", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Sample;
            }
            if (Name.StartsWith("SDK", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.SDK;
            }
            if (Name.StartsWith("THIRDPARTY", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.ThirdParty;
            }
            if (Name.StartsWith("BUILD", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Build;
            }
            if (Name.StartsWith("INTERNAL", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.InternalTool;
            }
            if (Name.StartsWith("WEB", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Web;
            }
            if (Name.Equals("DATABASE", StringComparison.OrdinalIgnoreCase)) {
                return ModuleType.Database;
            }

            return ModuleType.Unknown;
        }

        /// <summary>
        /// Gets or sets the assembly version of this module
        /// </summary>
        /// <value>
        /// The assembly version.
        /// </value>
        public string AssemblyVersion {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the assembly version of this module
        /// </summary>
        /// <value>
        /// The assembly version.
        /// </value>
        public string FileVersion {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the branch for which this module originates.
        /// </summary>
        /// <value>
        /// The branch.
        /// </value>
        public string Branch {
            get;
            set;
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
            return Name.ToUpperInvariant().GetHashCode();
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
    }
}