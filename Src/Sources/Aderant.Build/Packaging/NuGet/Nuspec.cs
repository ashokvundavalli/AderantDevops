namespace Aderant.Build.Packaging.NuGet {
    internal class Nuspec {
        private string text;

        public Nuspec(string text) {
            this.text = text;
            new NuspecSerializer(text, this).Deserialize();
        }

        public Nuspec()
            : this(Resources.TemplateNuspec) {
        }

        /// <summary>
        /// Gets or sets the package identifier.
        /// </summary>
        public StringNuspecValue Id { get; set; }

        /// <summary>
        /// Gets or sets the SemVer package version.
        /// </summary>
        public StringNuspecValue Version { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public StringNuspecValue Description { get; set; }

        public string Save() {
            return NuspecSerializer.Serialize(this, text);
        }
    }
}