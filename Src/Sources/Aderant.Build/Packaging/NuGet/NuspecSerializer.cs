using System;
using System.Linq;
using System.Xml.Linq;

namespace Aderant.Build.Packaging.NuGet {
    internal class NuspecSerializer {
        private readonly string text;
        private readonly Nuspec nuspec;

        public NuspecSerializer(string text, Nuspec nuspec) {
            this.text = text;
            this.nuspec = nuspec;
        }

        public static string GetVersion(string text) {
            XDocument document = XDocument.Parse(text);

            var version = GetElementValue("version", document);

            return version.Value;
        }

        public static string GetPackageName(string text) {
            XDocument document = XDocument.Parse(text);

            var id = GetElementValue("id", document);

            return id.Value;
        }

        public static string GetTags(string text) {
            XDocument document = XDocument.Parse(text);

            var tags = GetElementValue("tags", document);

            return tags.Value;
        }

        public static string GetRepositoryName(string text) {
            return GetTag(text, "repo:");
        }

        public static string GetBranchName(string text) {
            return GetTag(text, "branch:");
        }

        public static string GetCommitHash(string text) {
            return GetTag(text, "sha:");
        }

        public static string GetBuildId(string text) {
            return GetTag(text, "build:");
        }

        public static string GetBuildNumber(string text) {
            return GetTag(text, "buildNumber:");
        }

        private static string GetTag(string text, string prefix) {
            try {
                XDocument document = XDocument.Parse(text);

                var tags = GetElementValue("tags", document);

                var tagList = tags.Value.Split(' ');
                var tag = tagList.FirstOrDefault(t => t.StartsWith(prefix));
                if (tag == null) {
                    return string.Empty;
                }
                return tag.Split(':')[1];
            } catch {
                return string.Empty;
            }
        }


        private static XElement GetElementValue(string elementName, XDocument document) {
            return document.Descendants().FirstOrDefault(d => String.Equals(d.Name.LocalName, elementName, StringComparison.OrdinalIgnoreCase));
        }

        public void Deserialize() {
            XDocument document = XDocument.Parse(text);

            nuspec.Id = new StringNuspecValue { Value = GetElementValue("id", document).Value };
            nuspec.Version = new StringNuspecValue { Value = GetElementValue("version", document).Value };
            nuspec.Description = new StringNuspecValue { Value = GetElementValue("description", document).Value };
            nuspec.Files = GetElementValue("files", document);
        }

        public static string Serialize(Nuspec nuspec, string text) {
            XDocument document = XDocument.Parse(text);

            GetElementValue("id", document).Value = nuspec.Id.Value;
            GetElementValue("version", document).Value = nuspec.Version.Value;
            GetElementValue("description", document).Value = nuspec.Description.Value;

            if (document.Element("files") != null) {
                document.Element("Files").ReplaceWith(nuspec.Files);
            }

            return document.ToString();
        }
    }
}