using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Aderant.Build.DependencyResolver.Models {
    internal class PaketDependenciesStructure {
        internal readonly string[] Content;

        internal PaketDependenciesStructure(string[] lines) {
            if (lines == null) {
                throw new ArgumentNullException(nameof(lines));
            }
            if (lines.Length == 0) {
                throw new ArgumentException("Value cannot be an empty collection.", nameof(lines));
            }

            List<DependencyGroup> dependencyGroups = new List<DependencyGroup>();

            int index = 0;

            while (index < lines.Length) {
                DependencyGroup group = GenerateGroups(lines, index, out index);
                group.AddSource(Constants.PackageServerUrl);

                if (group.Dependencies.Any(x => x.IndexOf("nuget Aderant.Database.Backup", StringComparison.OrdinalIgnoreCase) != -1)) {
                    group.AddSource(Constants.DatabasePackageUri);
                } else {
                    group.RemoveSource(Constants.DatabasePackageUri);
                }

                SortSources(group.Sources);

                dependencyGroups.Add(group);
            }

            Content = GenerateContent(dependencyGroups);
        }

        private void SortSources(List<string> groupSources) {
            groupSources.Sort((s, s1) => s.IndexOf("nuget.org", StringComparison.OrdinalIgnoreCase));
        }

        internal static DependencyGroup GenerateGroups(string[] lines, int index, out int updateIndex) {
            DependencyGroup group;
            if (lines[index].StartsWith("group", StringComparison.OrdinalIgnoreCase)) {
                Regex regex = new Regex("group ", RegexOptions.IgnoreCase);
                group = new DependencyGroup(regex.Replace(lines[index], string.Empty));
                index++;
            } else {
                group = new DependencyGroup();
            }

            for (int i = index; i < lines.Length; i++) {
                if (lines[i].StartsWith("group", StringComparison.OrdinalIgnoreCase)) {
                    updateIndex = i;
                    return group;
                }

                if (lines[i].StartsWith("source", StringComparison.OrdinalIgnoreCase)) {
                    group.Sources.Add(lines[i]);
                    continue;
                }

                if (lines[i].StartsWith("nuget", StringComparison.OrdinalIgnoreCase)) {
                    group.Dependencies.Add(lines[i]);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(lines[i])) {
                    group.Properties.Add(lines[i]);
                }
            }

            updateIndex = lines.Length;

            return group;
        }

        internal static string[] GenerateContent(List<DependencyGroup> dependencyGroups) {
            List<string> lines = new List<string>();

            foreach (DependencyGroup group in dependencyGroups) {
                if (!group.Name.Equals("Main", StringComparison.OrdinalIgnoreCase)) {
                    lines.Add(string.Empty);
                    lines.Add(string.Concat("group ", group.Name));
                }

                foreach (string property in group.Properties) {
                    lines.Add(property);
                }

                foreach (string source in group.Sources) {
                    lines.Add(source);
                }

                foreach (string dependency in group.Dependencies) {
                    lines.Add(dependency);
                }
            }

            return lines.ToArray();
        }
    }

    internal class DependencyGroup {
        internal readonly string Name;
        internal readonly List<string> Properties = new List<string>();
        internal readonly List<string> Sources = new List<string>();
        internal readonly List<string> Dependencies = new List<string>();

        internal DependencyGroup() {
            Name = "Main";
        }

        internal DependencyGroup(string name) {
            Name = name;
        }

        internal void AddSource(string source) {
            if (!source.StartsWith("source ", StringComparison.OrdinalIgnoreCase)) {
                source = string.Concat("source ", source);
            }

            if (Sources.All(x => x.IndexOf(source, StringComparison.OrdinalIgnoreCase) == -1)) {
                Sources.Add(source);
            }
        }

        internal void RemoveSource(string source) {
            if (!source.StartsWith("source ", StringComparison.OrdinalIgnoreCase)) {
                source = string.Concat("source ", source);
            }

            Sources.RemoveAll(x => x.IndexOf(source, StringComparison.OrdinalIgnoreCase) != -1);
        }
    }
}
