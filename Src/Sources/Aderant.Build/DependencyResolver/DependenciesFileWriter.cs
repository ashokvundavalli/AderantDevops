using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Paket;

namespace Aderant.Build.DependencyResolver {
    internal class DependenciesFileWriter {
        public string Write(List<DependenciesGroup> groups, Dictionary<string, IReadOnlyCollection<string>> frameworkRestrictions) {
            StringBuilder sb = new StringBuilder();

            foreach (var group in groups) {
                if (!string.Equals(group.Name.Name, Constants.MainDependencyGroup, StringComparison.OrdinalIgnoreCase)) {
                    sb.Append("group ");
                    sb.Append(group.Name);
                    sb.AppendLine();
                }

                foreach (var source in group.Sources) {
                    sb.Append("source ");
                    sb.Append(source.Url);
                    sb.AppendLine();
                }

                if (frameworkRestrictions != null) {
                    IReadOnlyCollection<string> restrictions;
                    if (frameworkRestrictions.TryGetValue(group.Name.Name, out restrictions)) {
                        WriteFrameworkRestrictionText(sb, restrictions);
                    } else {
                        // Preserve the existing framework restriction (if any)
                        WriteFrameworkRestriction(sb, group);
                    }
                } else {
                    // Preserve the existing framework restriction (if any)
                    WriteFrameworkRestriction(sb, group);
                }

                if (group.Options.Strict) {
                    sb.AppendLine("references: strict");
                } else {
                    sb.AppendLine();
                }

                foreach (var packageRequirement in group.Packages) {
                    if (packageRequirement.Sources.HeadOrDefault.IsNuGetV2 || packageRequirement.Sources.HeadOrDefault.IsNuGetV3) {
                        var preReleaseString = string.Empty;
                        var status = packageRequirement.VersionRequirement.PreReleases;

                        if (status.IsConcrete) {
                            var versionRequirement = packageRequirement.VersionRequirement.ToString();
                            var preReleaseStatuses = ((PreReleaseStatus.Concrete) status).Item;

                            if (preReleaseStatuses.All(x => versionRequirement.IndexOf(x, StringComparison.OrdinalIgnoreCase) == -1)) {
                                preReleaseString = string.Join(" ", ((PreReleaseStatus.Concrete) status).Item);
                            }
                        }

                        sb.Append("nuget ");
                        sb.Append(packageRequirement.Name);

                        var requirement = packageRequirement.VersionRequirement.ToString();
                        if (!string.IsNullOrEmpty(requirement)) {
                            sb.Append(" ");
                            sb.Append(requirement);
                        }

                        if (!string.IsNullOrEmpty(preReleaseString)) {
                            sb.Append(" ");
                            sb.Append(preReleaseString);
                        }
                        sb.AppendLine();
                    } else {
                        throw new ArgumentException($"Unsupported dependency type for package: {packageRequirement.Name.Name}.");
                    }
                }

                foreach (var remoteFile in group.RemoteFiles) {
                    sb.AppendLine(remoteFile.ToString());
                }

                if (groups.Count > 1) {
                    // Put a blank line between groups if needed to keep the file readable
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static void WriteFrameworkRestrictionText(StringBuilder sb, IEnumerable<string> framework) {
            sb.Append("framework: ");
            sb.Append(string.Join(",", framework));
            sb.AppendLine();
        }

        private static void WriteFrameworkRestriction(StringBuilder sb, DependenciesGroup group) {
            var frameworkRestrictions = group.Options.Settings.FrameworkRestrictions;

            // If the restrictions are the default do not write them out to keep the file simple
            if (Requirements.InstallSettings.Default.FrameworkRestrictions.Equals(frameworkRestrictions)) {
                return;
            }

            var restrictions = frameworkRestrictions;
            if (restrictions.IsExplicitRestriction) {
                var restriction = restrictions.GetExplicitRestriction();
                WriteFrameworkRestrictionText(sb, restriction.RepresentedFrameworks.Select(s => s.ToString()));
            }
        }
    }
}
