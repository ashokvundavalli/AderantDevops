namespace Aderant.Build.DependencyResolver.Model {
    /// <summary>
    /// Represents a file
    /// </summary>
    internal class RemoteFile : IDependencyRequirement {
        public string Uri { get; }

        public RemoteFile(string itemName, string uri, string groupName) {
            Uri = uri;
            Group = groupName;
        }

        public string Name {
            get { return Uri; }
        }

        public string Group { get; }
        public VersionRequirement VersionRequirement { get; set; }
        public bool ReplaceVersionConstraint { get; set; }
        public bool ReplicateToDependencies { get; set; }
    }
}