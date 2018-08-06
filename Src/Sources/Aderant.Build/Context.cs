using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Aderant.Build.ProjectSystem;
using Aderant.Build.Services;

namespace Aderant.Build {

    [Serializable]
    public class Context {
        private BuildMetadata buildMetadata;

        private string buildScriptsDirectory;
        private bool isDesktopBuild = true;

        [NonSerialized]
        private IContextualServiceProvider serviceProvider;

        private BuildSwitches switches = default(BuildSwitches);

        public Context() {
            Configuration = new Dictionary<object, object>();
            VariableBags = new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            TaskIndex = -1;
            Variables = new Dictionary<string, string>();
            Environment = "";
            PipelineName = "";
            TaskName = "";
        }

        public string BuildScriptsDirectory {
            get {
                if (string.IsNullOrWhiteSpace(buildScriptsDirectory)) {
                    throw new ArgumentNullException(nameof(buildScriptsDirectory));
                }

                return buildScriptsDirectory;
            }
            set {
                value = Path.GetFullPath(value);
                buildScriptsDirectory = value;
            }
        }

        public DirectoryInfo BuildRoot { get; set; }

        public string BuildSystemDirectory { get; set; }

        public bool IsDesktopBuild {
            get { return isDesktopBuild; }
            set { isDesktopBuild = value; }
        }

        public IDictionary Configuration { get; set; }

        public FileInfo ConfigurationPath { get; set; }

        public DirectoryInfo DownloadRoot { get; set; }

        public string Environment { get; set; }

        public DirectoryInfo OutputDirectory { get; set; }

        public string PipelineName { get; set; }

        public bool Publish { get; set; }

        public DateTime StartedAt { get; set; }

        public string TaskName { get; set; }

        public int TaskIndex { get; set; }

        public IDictionary<string, IDictionary<string, string>> VariableBags { get; private set; }

        public DirectoryInfo Temp { get; set; }

        public IDictionary<string, string> Variables { get; private set; }

        public BuildMetadata BuildMetadata {
            get { return buildMetadata; }
            set {
                buildMetadata = value;

                if (value != null) {
                    if (!string.IsNullOrWhiteSpace(value.BuildId)) {
                        IsDesktopBuild = false;
                    } else {
                        IsDesktopBuild = true;
                    }
                }
            }
        }
        
        public BuildSwitches Switches {
            get { return switches; }
            set { switches = value; }
        }

        internal IContextualServiceProvider ServiceProvider {
            get {
                if (serviceProvider != null) {
                    return serviceProvider;
                }

                return serviceProvider = ServiceContainer.Default;
            }
        }

        public ConfigurationToBuild ConfigurationToBuild { get; set; }

        public string PrimaryDropLocation { get; set; }

        public string PullRequestDropLocation { get; set; }

        /// <summary>
        /// Creates a new instance of T.
        /// </summary>
        public T GetService<T>() where T : class {
            var svc = ServiceProvider.GetService(typeof(T));
            return (T)svc;
        }

        public object GetService(string contract) {
            var svc = ServiceProvider.GetService<object>(this, contract, null);
            return svc;
        }

        public DependencyRelationshipProcessing GetRelationshipProcessingMode() {
            DependencyRelationshipProcessing relationshipProcessing = DependencyRelationshipProcessing.None;
            if (Switches.Downstream) {
                relationshipProcessing = DependencyRelationshipProcessing.Direct;
            }

            if (Switches.Transitive) {
                relationshipProcessing = DependencyRelationshipProcessing.Transitive;
            }

            return relationshipProcessing;
        }

        public ChangesToConsider GetChangeConsiderationMode() {
            ChangesToConsider mode = ChangesToConsider.None;
            if (Switches.PendingChanges) {
                mode = ChangesToConsider.PendingChanges;
            }

            if (Switches.Everything) {
                mode = ChangesToConsider.Branch;
            }


            // TODO: Drive this from a PR comment?
            if (buildMetadata != null) {
                if (buildMetadata.IsPullRequest) {
                    return ChangesToConsider.PendingChanges;
                }
            }

            return mode;
        }

        public void AddVariableToBag(string id, string key, string value) {
            var bags = VariableBags;
            
            IDictionary<string, string> bag;
            if (!bags.TryGetValue(id, out bag)) {
                bag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bags[id] = bag;
            }

            bag[key] = value;
        }

        public string GetVariableFromBag(string id, string key) {
            ErrorUtilities.IsNotNull(id, nameof(id));
            ErrorUtilities.IsNotNull(key, nameof(key));

            var bags = VariableBags;

            IDictionary<string, string> bag;
            if (bags.TryGetValue(id, out bag)) {
                string value;
                bag.TryGetValue(key, out value);

                return value;
            }

            return null;
        }
    }

    [Serializable]
    public struct BuildSwitches {

        public bool PendingChanges { get; set; }
        public bool Downstream { get; set; }
        public bool Transitive { get; set; }
        public bool Everything { get; set; }
        public bool Clean { get; set; }
        public bool Release { get; set; }
        public bool DryRun { get; set; }
        public bool Resume { get; set; }

        [CreateProperty]
        public bool SkipCompile { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal class CreatePropertyAttribute : Attribute {
    }

}
