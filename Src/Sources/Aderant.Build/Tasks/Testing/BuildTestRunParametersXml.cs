using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks.Testing {
    /// <summary>
    /// Merges local test run parameters with a global set of parameters.
    /// </summary>
    /// <remarks>
    /// A build contributor may wish to publish test parameters that should apply to all tests within the build such as secrets or an API endpoint.
    /// </remarks>
    public sealed class BuildTestRunParametersXml : BuildOperationContextTask {
        internal const string TestRunParametersNodeName = "TestRunParameters";

        [Output]
        public ITaskItem[] TestRunParameters { get; set; }

        public override bool ExecuteTask() {
            StringBuilder cdataBlob = new StringBuilder();

            IDictionary<string, string> scopedVariable;
            if (Context.ScopedVariables.TryGetValue(TestRunParametersNodeName, out scopedVariable)) {
                if (scopedVariable != null) {
                    foreach (var item in scopedVariable) {
                        cdataBlob.AppendLine(
                            new XElement("Parameter",
                                new XAttribute("name", item.Key),
                                new XAttribute("value", item.Value)).ToString());
                    }
                }
            }

            // Merge in the local scope values
            // Elements at this scope will take precedent since they appear later
            // in the document
            string runParametersXPath = "/RunSettings/" + TestRunParametersNodeName;

            if (TestRunParameters != null) {
                var parameters = TestRunParameters.ToList();

                for (var i = parameters.Count - 1; i >= 0; i--) {
                    var item = parameters[i];

                    if (item.ItemSpec.StartsWith(runParametersXPath)) {
                        string metadata = item.GetMetadata("Value");
                        if (metadata != null) {
                            cdataBlob.AppendLine(metadata);
                        }

                        parameters.RemoveAt(i);
                    }
                }

                if (cdataBlob.Length > 0) {
                    parameters.Add(
                        new TaskItem(runParametersXPath,
                            new Hashtable {
                                {
                                    "Value",
                                    cdataBlob.ToString()
                                }
                            }));
                }

                TestRunParameters = parameters.ToArray();
            }

            return !Log.HasLoggedErrors;
        }
    }
}
