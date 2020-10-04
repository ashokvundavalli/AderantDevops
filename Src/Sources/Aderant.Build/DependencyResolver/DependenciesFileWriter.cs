using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.FSharp.Collections;
using Paket;

namespace Aderant.Build.DependencyResolver.Models {
    internal class DependenciesFileWriter {
        public string Write(List<DependenciesGroup> groups, Dictionary<string, IReadOnlyCollection<string>> frameworkRestrictions) {
            StringBuilder sb = new StringBuilder();

            foreach (var group in groups) {
                if (!string.Equals(group.Name.Name, Constants.MainDependencyGroup, StringComparison.OrdinalIgnoreCase)) {
                    sb.AppendLine("group " + group.Name);
                }

                foreach (var source in group.Sources) {
                    sb.AppendLine("source " + source.Url);
                }

                if (frameworkRestrictions != null) {
                    IReadOnlyCollection<string> restrictions;
                    if (frameworkRestrictions.TryGetValue(group.Name.Name, out restrictions)) {
                        sb.AppendLine("framework: " + string.Join(",", restrictions));
                    }
                }

                if (group.Options.Strict) {
                    sb.AppendLine("references: strict");
                }

                sb.AppendLine();

                foreach (var packageRequirement in group.Packages) {
                    if (packageRequirement.Sources.HeadOrDefault.IsNuGetV2 || packageRequirement.Sources.HeadOrDefault.IsNuGetV3) {
                        string preReleaseString = string.Empty;
                        PreReleaseStatus status = packageRequirement.VersionRequirement.PreReleases;
                        if (status.IsConcrete) {
                            FSharpList<string> preReleaseStatuses = ((PreReleaseStatus.Concrete) status).Item;
                            string versionRequirement = packageRequirement.VersionRequirement.ToString();

                            if (preReleaseStatuses.All(x => versionRequirement.IndexOf(x, StringComparison.OrdinalIgnoreCase) == -1)) {
                                preReleaseString = string.Join(" ", ((PreReleaseStatus.Concrete) status).Item);
                            }
                        }

                        sb.AppendFormat(
                            "nuget {0} {1} {2}",
                            packageRequirement.Name,
                            packageRequirement.VersionRequirement,
                            preReleaseString);
                        sb.AppendLine();
                    } else {
                        throw new ArgumentException($"Unsupported dependency type for package: {packageRequirement.Name.Name}.");
                    }
                }

                foreach (var remoteFile in group.RemoteFiles) {
                    sb.AppendLine(remoteFile.ToString());
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
