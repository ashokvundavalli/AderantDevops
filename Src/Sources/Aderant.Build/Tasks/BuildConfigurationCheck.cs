using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class BuildConfigurationCheck : Microsoft.Build.Utilities.Task {
        [Required]
        public ITaskItem[] ProjectFiles { get; set; }

        public string BranchName { get; set; }

        public override bool Execute() {
            if (string.IsNullOrEmpty(BranchName)) {
                // Default to the variable name that represents the branch name (NB: this does not mean the value of the branch name variable)
                BranchName = "$(BranchName)";
            }

            var check = new BuildProjectCheck();

            foreach (ITaskItem item in ProjectFiles) {
                if (File.Exists(item.ItemSpec)) {
                    Log.LogMessage("Validating build file: " + item.ItemSpec, null);

                    string projectFileText = File.ReadAllText(item.ItemSpec);

                    try {
                        check.CheckForInvalidBranch(projectFileText, BranchName);
                    } catch (Exception ex) {
                        Log.LogError("File {0} is invalid. {1}", item.ItemSpec, ex.Message);
                        break;
                    }
                }
            }

            Log.LogMessage("Build file validation complete", null);

            return !Log.HasLoggedErrors;
        }
    }

    // The stratergy this class represents should be obseleted by removing the requirement to import the build project from the 
    // drop location - but that is a much longer term goal so this will do to validate people don't check in invalid configurations for now
    internal class BuildProjectCheck {
        public void CheckForInvalidBranch(string projectFileText, string expectedBranch) {
            XElement project = XElement.Parse(projectFileText, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);

            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            var element = project.Descendants(ns + "Import").FirstOrDefault(elm => {
                XAttribute attribute = elm.Attribute("Project");
                if (attribute != null) {
                    var value = attribute.Value;
                    return value.StartsWith(@"\\");
                }
                return false;
            });

            if (element != null) {
                XAttribute attribute = element.Attribute("Project");

                if (attribute != null) {
                    string value = attribute.Value;
                    if (!string.IsNullOrEmpty(value)) {
                        ValidateBranchPath(value, expectedBranch);
                    }
                }
            }
        }

        private void ValidateBranchPath(string value, string expectedBranch) {
            if (value.IndexOf(expectedBranch, StringComparison.OrdinalIgnoreCase) == -1) {
                throw new InvalidOperationException("Import path " + value + " does not contain " + expectedBranch);
            }
        }
    }
}