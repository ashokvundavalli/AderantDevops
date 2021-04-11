using System;
using System.Collections.Generic;

namespace Aderant.Build.DependencyResolver {

    internal interface IWellKnownSources {
        IReadOnlyList<PackageSource> GetSources();
    }

    internal class PackageSource {
        public PackageSource(string name, string url) {
            Name = name;
            Url = url;
        }

        /// <summary>
        /// The friendly name of the source.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The package source URL.
        /// </summary>
        public string Url { get; }
    }

    internal class WellKnownPackageSources : IWellKnownSources {

        /// <summary>
        /// Gets the default implementation of the NuGet sources known the system.
        /// </summary>
        internal static IWellKnownSources Default { get; } = new WellKnownPackageSources();

        public IReadOnlyList<PackageSource> GetSources() {
            if (AzureHostedSources.hasOverride) {
                return Array.Empty<PackageSource>();
            }

            return AzureHostedSources.Sources;
        }

        internal class AzureHostedSources : IWellKnownSources {
            internal static bool hasOverride;

            static AzureHostedSources() {
                string url = Environment.GetEnvironmentVariable("EXPERT_PACKAGES_URL"); ;
                if (string.IsNullOrEmpty(url)) {
                    url = "https://expertpackages.azurewebsites.net/v3/index.json";
                } else {
                    hasOverride = true;
                }

                Sources = new List<PackageSource> {
                    new PackageSource("PackagesOnAzure", url)
                };
            }

            internal static IReadOnlyList<PackageSource> Sources { get; }

            public IReadOnlyList<PackageSource> GetSources() {
                return Sources;
            }
        }
    }

}