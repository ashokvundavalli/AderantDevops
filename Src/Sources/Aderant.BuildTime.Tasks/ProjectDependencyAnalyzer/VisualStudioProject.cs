using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.BuildTime.Tasks.Sequencer;

namespace Aderant.BuildTime.Tasks.ProjectDependencyAnalyzer {
    /// <summary>
    /// Object representing a Visual Studio project
    /// </summary>
    [DebuggerDisplay("{AssemblyName} in {SolutionDirectoryName} depends on {Dependencies.Count} objects")]
    internal class VisualStudioProject : IEquatable<VisualStudioProject>, IDependencyRef {
        private readonly XDocument project;

        protected VisualStudioProject() {
        }

        /// <summary>
        /// Get or set the list of assembly names on which this projects depends
        /// </summary>
        /// <value>
        /// The list of project references
        /// </value>
        public ICollection<IDependencyRef> Dependencies { get; set; }

        /// <summary>
        /// Get or set the name of the assembly generated by the project
        /// </summary>
        [XmlAttribute("Label")]
        public string AssemblyName { get; set; }

        /// <summary>
        /// Get or set the root namespace of the project
        /// </summary>
        [XmlAttribute("RootNamespace")]
        public string RootNamespace { get; set; }

        /// <summary>
        /// Get or set the project path on disk
        /// </summary>
        [XmlAttribute("Path")]
        public string Path { get; set; }

        /// <summary>
        /// Get or set the unique project identifier
        /// </summary>
        public Guid ProjectGuid { get; set; }

        /// <summary>
        /// Get the project id as a string.
        /// </summary>
        [XmlAttribute("ProjectGuid")]
        public string Id {
            get { return ProjectGuid.ToString(); }
        }

        /// <summary>
        /// Get or set a value indicating if the project is a web project
        /// </summary>
        [XmlAttribute("IsWebProject")]
        public bool IsWebProject { get; set; }

        /// <summary>
        /// Get or set a value indicating if the project is a test project
        /// </summary>
        [XmlAttribute("IsTestProject")]
        public bool IsTestProject { get; set; }

        /// <summary>
        /// Gets or sets the project's artefacts output type
        /// </summary>
        [XmlAttribute("OutputType")]
        public string OutputType { get; set; }

        public string SolutionRoot { get; set; }

        public string SolutionDirectoryName {
            get { return System.IO.Path.GetFileName(SolutionRoot); }
        }

        public bool IncludeInBuild { get; set; }

        public string Name {
            get { return AssemblyName; }
        }

        public string SolutionFile { get; set; }

        public BuildConfiguration BuildConfiguration { get; set; }

        private static XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";
        private IEnumerable<string> projectItems;

        public bool ContainsFile(string file) {
            if (projectItems == null) {
                projectItems = GetProjectItems(project);
            }

            string relativePath = PathUtility.MakeRelative(System.IO.Path.GetDirectoryName(Path), file);

            if (projectItems.Contains(relativePath, StringComparer.OrdinalIgnoreCase)) {
                return true;
            }

            return false;
        }

        private static IEnumerable<string> GetProjectItems(XDocument document) {
            return document.Descendants(msbuild + "Content").Concat(document.Descendants(msbuild + "None")).Select(s => s.Attribute("Include")?.Value);
        }

        /// <summary>
        /// Initialize a new instance of the <see cref="VisualStudioProject"/> class.
        /// </summary>
        /// <param name="project"></param>a
        /// <param name="assemblyName">Name of the assembly generated from the project.</param>
        /// <param name="rootNamespace">The project's root namespace.</param>
        /// <param name="path">The physical location of the project on the disk.</param>
        public VisualStudioProject(XDocument project, Guid projectGuid, string assemblyName, string rootNamespace, string path) {
            this.project = project;
            AssemblyName = assemblyName;
            ProjectGuid = projectGuid;
            RootNamespace = rootNamespace;
            Path = path;
            Dependencies = new HashSet<IDependencyRef>();
        }

        public bool Equals(IDependencyRef other) {
            if (this.ProjectGuid == Guid.Parse("A262C3C9-5870-437D-A7B3-9BD348E715D6")) {
                
            }
            return Equals(other as VisualStudioProject);
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != typeof(VisualStudioProject))
                return false;
            return Equals((VisualStudioProject)obj);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        public bool Equals(VisualStudioProject other) {
            if (ReferenceEquals(null, other)) {
                return false;
            }
            if (ReferenceEquals(this, other)) {
                return true;
            }

            return other.AssemblyName != null && other.AssemblyName.Equals(AssemblyName) && other.ProjectGuid == ProjectGuid;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode() {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(AssemblyName) ^ ProjectGuid.GetHashCode();
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(VisualStudioProject left, VisualStudioProject right) {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(VisualStudioProject left, VisualStudioProject right) {
            return !Equals(left, right);
        }
    }
}