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
        /// Supports overrides via environment variables.
        /// </summary>
        internal static IWellKnownSources Default { get; } = new WellKnownPackageSources();

        public IReadOnlyList<PackageSource> GetSources() {
            // Escape hatch to fall back to the legacy sources.
            string variable = Environment.GetEnvironmentVariable("DISABLE_AZURE_NUGET");

            if (string.IsNullOrEmpty(variable)) {
                return AzureHostedSources.Sources;
            }

            return NonAzureHostedSources.Sources;
        }

        /// <summary>
        /// Encapsulates the internally hosted package source information.
        /// </summary>
        internal class NonAzureHostedSources : IWellKnownSources {
            static NonAzureHostedSources() {
                Sources = new List<PackageSource> {
                    new PackageSource("DatabasePackages", Constants.DatabasePackageUri),
                    new PackageSource("Packages", Constants.PackageServerUrl),
                };
            }

            internal static IReadOnlyList<PackageSource> Sources { get; }

            public IReadOnlyList<PackageSource> GetSources() {
                return Sources;
            }
        }

        internal class AzureHostedSources : IWellKnownSources {
            static AzureHostedSources() {
                Sources = new List<PackageSource> {
                    new PackageSource("ExpertPackagesOnAzure", Constants.PackageServerUrlV3)
                };
            }

            internal static IReadOnlyList<PackageSource> Sources { get; }

            public IReadOnlyList<PackageSource> GetSources() {
                return Sources;
            }
        }
    }

}