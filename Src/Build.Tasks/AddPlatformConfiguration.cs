using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Workflow;
using Microsoft.TeamFoundation.Build.Workflow.Activities;
using Microsoft.TeamFoundation.Client;

namespace Build.Tasks {
    /// <summary>
    /// Adds a Build Platform and Configuration to a Build Definition 
    /// </summary>
    /// <remarks>
    /// We have a hybrid TFS 2008-2012 build environment. To leverage TFS integration features such as test coverage and test result publishing we need to use
    /// the DefaultTemplate. Since we don't use this template (instead we use the UpgradeTemplate), we need to dynamically reconfigure the build definition
    /// with the current build configuration at build time.
    /// If this does not happen VSTest.console.exe will fail with an error similar to "The platform "Any CPU" or flavor "Release" does not match those in the build name "1.8.5138.38919". The results will not be published."
    /// </remarks>
    public class AddPlatformConfiguration : Task {
        private const string BuildSettings = "BuildSettings";

        [Required]
        public ITaskItem BuildUri { get; set; }

        [Required]
        public ITaskItem TeamFoundationServer { get; set; }

        public string Configuration { get; set; }

        public override bool Execute() {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;

            AddProcessParameters();

            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomainOnAssemblyResolve;
            return true;
        }

        private Assembly CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args) {
            string ide = Environment.GetEnvironmentVariable("VS110COMNTOOLS");
            string visualStudioPath = Path.Combine(ide, @"..\IDE\ReferenceAssemblies\v2.0\");

            if (args.Name.StartsWith("Microsoft.TeamFoundation")) {
                string name = args.Name.Split(',')[0];

                string assembly = Path.Combine(visualStudioPath, name + ".dll");
                if (File.Exists(assembly)) {
                    return Assembly.LoadFrom(assembly);
                }
            }

            return null;
        }

        private void AddProcessParameters() {
            using (TfsTeamProjectCollection collection = new TfsTeamProjectCollection(new Uri(TeamFoundationServer.ItemSpec))) {
                IBuildServer buildServer = collection.GetService<IBuildServer>();

                IBuildDetail buildDetail = buildServer.GetBuild(new Uri(BuildUri.ItemSpec));

                BuildSettings buildSettings = null;
                IDictionary<string, object> parameters = new Dictionary<string, object>();

                string processParameters = buildDetail.BuildDefinition.ProcessParameters;
                if (!string.IsNullOrEmpty(processParameters)) {
                    parameters = WorkflowHelpers.DeserializeProcessParameters(processParameters);

                    if (parameters.ContainsKey(BuildSettings)) {
                        buildSettings = parameters[BuildSettings] as BuildSettings;
                    }
                }

                if (buildSettings == null) {
                    buildSettings = new BuildSettings();
                    buildSettings.PlatformConfigurations = new PlatformConfigurationList();
                    buildSettings.PlatformConfigurations.Add(new PlatformConfiguration("Any CPU", Configuration));
                } else {
                    if (buildSettings.PlatformConfigurations == null) {
                        buildSettings.PlatformConfigurations = new PlatformConfigurationList();
                    }

                    if (!buildSettings.PlatformConfigurations.Any(cfg => cfg.Platform == "Any CPU" && cfg.Configuration == Configuration)) {
                        buildSettings.PlatformConfigurations.Add(new PlatformConfiguration("Any CPU", Configuration));
                    }
                }

                parameters[BuildSettings] = buildSettings;
                buildDetail.BuildDefinition.ProcessParameters = WorkflowHelpers.SerializeProcessParameters(parameters);
                buildDetail.BuildDefinition.Save();
            }
        }
    }
}