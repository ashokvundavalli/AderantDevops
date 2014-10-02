using System;
using System.Diagnostics;
using DependencyAnalyzer;

namespace Aderant.Build.DependencyAnalyzer {
    /// <summary>
    /// Represents a module dependency where Consumer depends on a Provider
    /// </summary>
    [DebuggerDisplay("{Consumer} depends on {Provider} in branch {Branch}")]
    public class ModuleDependency : IEquatable<ModuleDependency> {

        /// <summary>
        /// Gets or sets the module which consumes the <see cref="Provider"/>.
        /// </summary>
        /// <value>The source module.</value>
        public ExpertModule Consumer { get; set; }

        /// <summary>
        /// Gets or sets the module upon which the <see cref="Consumer"/> module is dependent.
        /// </summary>
        /// <value>The target module.</value>
        public ExpertModule Provider { get; set; }

        /// <summary>
        /// Gets or sets the build version.
        /// </summary>
        /// <value>The version.</value>
        public string AssemblyVersion {
            get {
                if (Provider != null) {
                    return Provider.AssemblyVersion;
                }
                return null;
            }
        }

        /// <summary>
        /// Gets or sets the file version.
        /// </summary>
        /// <value>The file version.</value>
        public string FileVersion {
            get {
                if (Provider != null) {
                    return Provider.FileVersion;
                }
                return null;
            }
        }

        /// <summary>
        /// Gets or sets the branch which contains the <see cref="Provider"/>. Default (same branch as source) is Null.
        /// </summary>
        /// <value>The branch.</value>
        public string Branch {
            get {
                if (Provider != null) {
                    return Provider.Branch;
                }
                return null;
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
            if(!(obj is ModuleDependency)) {
                return false;
            }
            return this.Equals((ModuleDependency)obj);
        }

        public override int GetHashCode() {
            return (Branch == null ? 0 : Branch.GetHashCode()) ^
                (AssemblyVersion == null ? 0 : AssemblyVersion.GetHashCode()) ^
                    (FileVersion == null ? 0 : FileVersion.GetHashCode()) ^
                        (Consumer == null ? 0 : Consumer.GetHashCode()) ^
                            (Provider == null ? 0 : Provider.GetHashCode());

        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(ModuleDependency other) {
            return string.Equals(this.Branch, other.Branch, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(this.AssemblyVersion, other.AssemblyVersion, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(this.FileVersion, other.FileVersion, StringComparison.OrdinalIgnoreCase) &&
                this.Consumer.Equals(other.Consumer) &&
                this.Provider.Equals(other.Provider);
        }
    }
}