using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;

namespace Build.Tasks {
    public class WorkflowIntegrationTask : Task {

        [Required]
        public ITaskItem BuildUri { get; set; }

        [Required]
        public ITaskItem TeamFoundationServer { get; set; }

        public override bool Execute() {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
            bool result = ExecuteInternal();
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomainOnAssemblyResolve;

            return result;
        }

        public virtual bool ExecuteInternal() {
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

        protected IBuildDetail GetBuildDetail() {
            using (TfsTeamProjectCollection collection = new TfsTeamProjectCollection(new Uri(TeamFoundationServer.ItemSpec))) {
                IBuildServer buildServer = collection.GetService<IBuildServer>();
                return buildServer.GetBuild(new Uri(BuildUri.ItemSpec));
            }
        }
    }
}