namespace Aderant.Build.DependencyResolver.Model {
    /// <summary>
    /// Represents a file
    /// </summary>
    internal class RemoteFile : IDependencyRequirement {
        private readonly string itemName;
        public string Uri { get; }

        public RemoteFile(string itemName, string uri, string groupName) {
            this.itemName = itemName;
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
