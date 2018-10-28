﻿using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.PipelineService;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {
    internal class ExistingPlanLoader {

        /// <summary>
        /// Loads an existing plan and applies the plan to the current build context.
        /// Used during restart/resume scenarios where the main analysis phase is skipped.
        /// </summary>
        public void LoadPlan(string planFile, IBuildPipelineService service) {
            var globalProps = new Dictionary<string, string> { { "ResumeGroupId", "" } };

            using (var collection = new ProjectCollection(globalProps)) {
                collection.IsBuildEnabled = false;

                var loadProject = collection.LoadProject(planFile);
                var projectItems = loadProject.GetItems("ProjectsToBuild");

                IEnumerable<ProjectItem> items = projectItems.Where(s => s.HasMetadata("IsProjectFile"));
                foreach (var item in items) {
                    if (string.Equals(bool.TrueString, item.GetMetadataValue("IsProjectFile"), StringComparison.OrdinalIgnoreCase)) {
                        string fullPath = item.GetMetadataValue("Identity");

                        TrackedProject trackedProject = TrackedProject.GetPropertiesNeededForTracking(item.Metadata.ToDictionary(s => s.Name, s => s.EvaluatedValue));
                        trackedProject.FullPath = fullPath;
                        
                        service.TrackProject(trackedProject);
                    }
                }
            }
        }
    }
}
