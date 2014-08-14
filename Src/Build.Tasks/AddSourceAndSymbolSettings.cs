using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Workflow;
using Microsoft.TeamFoundation.Build.Workflow.Activities;

namespace Build.Tasks {

    /// <summary>
    /// Adds a Symbol Server to a Build Definition.
    /// </summary>
    public class AddSourceAndSymbolSettings : WorkflowIntegrationTask {

        /// <summary>
        /// Gets or sets the symbol server.
        /// </summary>
        /// <value>
        /// The symbol server.
        /// </value>
        [Required]
        public string SymbolServer { get; set; }

        public override bool ExecuteInternal() {
            IBuildDetail buildDetail = GetBuildDetail();
            
            SourceAndSymbolServerSettings symbolServerSettings = null;
            IDictionary<string, object> parameters = new Dictionary<string, object>();

            string processParameters = buildDetail.BuildDefinition.ProcessParameters;
            if (!string.IsNullOrEmpty(processParameters)) {
                parameters = WorkflowHelpers.DeserializeProcessParameters(processParameters);

                if (parameters.ContainsKey("SourceAndSymbolServerSettings")) {
                    symbolServerSettings = parameters["SourceAndSymbolServerSettings"] as SourceAndSymbolServerSettings;
                } else {
                     parameters["SourceAndSymbolServerSettings"] = symbolServerSettings = new SourceAndSymbolServerSettings();
                }
            }

            if (symbolServerSettings != null) {
                if (string.IsNullOrEmpty(symbolServerSettings.SymbolStorePath)) {
                    SetSymbolPath(symbolServerSettings);
                    
                    buildDetail.BuildDefinition.ProcessParameters = WorkflowHelpers.SerializeProcessParameters(parameters);
                    buildDetail.BuildDefinition.Save();
                }
            }

            return true;
        }

        private void SetSymbolPath(SourceAndSymbolServerSettings symbolServerSettings) {
            symbolServerSettings.SymbolStorePath = SymbolServer;
            symbolServerSettings.IndexSources = true;
        }
    }
}