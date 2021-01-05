using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Aderant.Build;
using Aderant.Build.Logging;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.VersionControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.EndToEnd {
    internal class TestBuildServiceHost : IDisposable {
        private readonly string deploymentItemsDirectory;
        private readonly bool disableInProcNode;
        private readonly TestContext testContext;

        private BuildOperationContext context;

        private string endpoint;
        private Dictionary<string, string> properties;
        public BuildPipelineServiceClient Client { get; private set; }
        private BuildPipelineServiceHost service;

        public TestBuildServiceHost(bool disableInProcNode, TestContext testContext, string deploymentItemsDirectory) {
            this.disableInProcNode = disableInProcNode;
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
                Client?.Dispose();
            } catch {

            }

            try {
                service?.Dispose();
            } catch {

            }
        }

        public void Initialize() {
            if (properties == null) {
                endpoint = Path.GetRandomFileName();
                testContext.WriteLine("Creating test host: " + endpoint);

                properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    { "BuildScriptsDirectory", testContext.DeploymentDirectory + "\\" },
                    { "CompileBuildSystem", bool.FalseString },
                    { "ProductManifestPath", Path.Combine(deploymentItemsDirectory, "ExpertManifest.xml") },
                    { "SolutionRoot", deploymentItemsDirectory },
                    { "ArtifactStagingDirectory", $@"{Path.Combine(testContext.DeploymentDirectory, Path.GetRandomFileName())}\" },
                    { "PackageArtifacts", bool.TrueString },
                    { "CopyToDropEnabled", bool.TrueString },
                    { "GetProduct", bool.FalseString },
                    { "PackageProduct", bool.TrueString},
                    { "RunTests", bool.FalseString },
                    { "GetDependencies", bool.FalseString},
                    { "AllowNullScmBranch", bool.TrueString},
                    { "GenerateFactory", bool.FalseString},
                    { "ExcludeArtifactStagingDirectory", bool.FalseString},
                    { WellKnownProperties.ContextEndpoint, endpoint },
                };

                context = CreateContext(properties);

                StartService();
                service.CurrentContext = context;

                Client = new BuildPipelineServiceClient(service.ServerUri.AbsoluteUri);
                Client.Ping();

                properties["PrimaryDropLocation"] = context.DropLocationInfo.PrimaryDropLocation;
                properties["BuildCacheLocation"] = context.DropLocationInfo.BuildCacheLocation;
            }
        }

        private void StartService() {
            if (service != null) {
                service.Dispose();
            }

            service = new BuildPipelineServiceHost();
            service.SetServiceAddressEnvironmentVariable = disableInProcNode;
            service.StartService(endpoint);
        }

        public BuildOperationContext GetContext() {
            return service.CurrentContext;
        }

        private BuildOperationContext CreateContext(Dictionary<string, string> props) {
            var ctx = new BuildOperationContext();
            ctx.BuildRoot = props["SolutionRoot"];
            ctx.BuildScriptsDirectory = props["BuildScriptsDirectory"];
            ctx.BuildSystemDirectory = Path.Combine(testContext.DeploymentDirectory, @"..\..\");

            ctx.DropLocationInfo.PrimaryDropLocation = Path.Combine(testContext.DeploymentDirectory, testContext.TestName, "_drop");
            ctx.DropLocationInfo.BuildCacheLocation = Path.Combine(testContext.DeploymentDirectory, testContext.TestName, "_cache");

            ctx.BuildMetadata = new BuildMetadata { BuildSourcesDirectory = deploymentItemsDirectory };

            ctx.SourceTreeMetadata = GetSourceTreeMetadata();

            return ctx;
        }

        public void PrepareForAnotherRun() {
            Initialize();

            context = CreateContext(properties);
            context.BuildMetadata.BuildId += 1;

            var artifactService = new StateFileService(NullLogger.Default);
            artifactService.AllowZeroBuildId = true;

            var buildStateMetadata = artifactService
                .GetBuildStateMetadata(context.SourceTreeMetadata.GetBuckets(BucketKind.CurrentCommit).Select(s => s.Id).ToArray(),
                    null,
                    context.DropLocationInfo.BuildCacheLocation,
                    null,
                    CancellationToken.None);

            Assert.AreNotEqual(0, buildStateMetadata.BuildStateFiles.Count);

            context.BuildStateMetadata = buildStateMetadata;

            service.CurrentContext = context;
        }

        private SourceTreeMetadata GetSourceTreeMetadata() {
            var versionControl = new GitVersionControlService();
            return versionControl.GetMetadata(deploymentItemsDirectory, string.Empty, string.Empty);
        }
    }
}
