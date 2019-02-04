using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.PipelineService;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.EndToEnd {
    internal class TestBuildServiceHost : IDisposable {
        private readonly string deploymentItemsDirectory;
        private readonly TestContext testContext;

        private BuildOperationContext context;

        private string endpoint;
        private Dictionary<string, string> properties;
        private BuildPipelineServiceClient client;
        private BuildPipelineServiceHost service;

        public TestBuildServiceHost(TestContext testContext, string deploymentItemsDirectory) {
            this.testContext = testContext;
            this.deploymentItemsDirectory = deploymentItemsDirectory;
        }

        public IDictionary<string, string> Properties {
            [DebuggerStepThrough]
            get {
                Initialize();
                return properties;
            }
        }

        public void Dispose() {
            try {
                client?.Dispose();
            } catch {

            }

            try {
                service?.Dispose();
            } catch {

            }
        }

        private void Initialize() {
            if (properties == null) {
                endpoint = Path.GetRandomFileName();

                properties = new Dictionary<string, string> {
                    { "BuildScriptsDirectory", testContext.DeploymentDirectory + "\\" },
                    { "CompileBuildSystem", bool.FalseString },
                    { "ProductManifestPath", Path.Combine(deploymentItemsDirectory, "ExpertManifest.xml") },
                    { "SolutionRoot", Path.Combine(deploymentItemsDirectory) },
                    { "ArtifactStagingDirectory", $@"{Path.Combine(testContext.DeploymentDirectory, Path.GetRandomFileName())}\" },
                    { "PackageArtifacts", bool.TrueString },
                    { "XamlBuildDropLocation", "A" },
                    { "CopyToDropEnabled", bool.TrueString },
                    { "GetProduct", bool.FalseString },
                    { "PackageProduct", bool.TrueString},
                    { "RunTests", bool.FalseString },
                    { "NoDependencyFetch", bool.TrueString},
                    { "AllowNullScmBranch", bool.TrueString},
                    { WellKnownProperties.ContextEndpoint, endpoint },
                };

                context = CreateContext(properties);

                StartService();
                service.CurrentContext = context;

                client = new BuildPipelineServiceClient(service.ServerUri.AbsoluteUri);

                properties["PrimaryDropLocation"] = context.DropLocationInfo.PrimaryDropLocation;
                properties["BuildCacheLocation"] = context.DropLocationInfo.BuildCacheLocation;
            }
        }

        private void StartService() {
            if (service != null) {
                service.Dispose();
            }

            service = new BuildPipelineServiceHost();
            service.StartService(endpoint);
        }

        public BuildOperationContext GetContext() {
            return service.CurrentContext;
        }

        private BuildOperationContext CreateContext(Dictionary<string, string> props) {
            var ctx = new BuildOperationContext();
            ctx.DropLocationInfo.PrimaryDropLocation = Path.Combine(testContext.DeploymentDirectory, testContext.TestName, "_drop");
            ctx.DropLocationInfo.BuildCacheLocation = Path.Combine(testContext.DeploymentDirectory, testContext.TestName, "_cache");

            ctx.BuildMetadata = new BuildMetadata { BuildSourcesDirectory = deploymentItemsDirectory };

            ctx.SourceTreeMetadata = GetSourceTreeMetadata();
            ctx.BuildScriptsDirectory = props["BuildScriptsDirectory"];
            ctx.BuildSystemDirectory = Path.Combine(testContext.DeploymentDirectory, @"..\..\");

            return ctx;
        }

        public void PrepareForAnotherRun() {
            Initialize();

            context = CreateContext(properties);
            context.BuildMetadata.BuildId += 1;

            var buildStateMetadata = new ArtifactService(NullLogger.Default)
                .GetBuildStateMetadata(
                    context.SourceTreeMetadata.GetBuckets().Select(s => s.Id).ToArray(),
                    context.DropLocationInfo.BuildCacheLocation);

            Assert.AreNotEqual(0, buildStateMetadata.BuildStateFiles.Count);

            context.BuildStateMetadata = buildStateMetadata;

            service.CurrentContext = context;
        }

        private SourceTreeMetadata GetSourceTreeMetadata() {
            var versionControl = new GitVersionControlService();
            return versionControl.GetMetadata(deploymentItemsDirectory, "", "");
        }
    }
}