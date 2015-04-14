using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public abstract class TrackedSourcesBuildTask : Task {

        protected ITaskItem[] excludedInputPaths;
        private ITaskItem[] tlogReadFiles;
        private ITaskItem[] tlogWriteFiles;
        private CanonicalTrackedInputFiles trackedInputFiles;
        private bool skippedExecution;
        private bool trackFileAccess;
        protected ITaskItem[] compileSourceList;

        [Required]
        public ITaskItem TrackerLogDirectory { get; set; }

        [Required]
        public virtual ITaskItem[] Sources { get; set; }

        [Required]
        public bool MinimalRebuildFromTracking { get; set; }

        public virtual string OutputFile { get; set; }

        protected bool Setup() {
            SkippedExecution = false;
            if (TrackFileAccess || MinimalRebuildFromTracking) {
                SetTrackerLogPaths();
            }

            CalcSourcesToBuild();
            return true;
        }

        protected virtual CanonicalTrackedOutputFiles OutputWriteTLog(ITaskItem[] inputs) {
            if (string.IsNullOrEmpty(OutputFile)) {
                throw new InvalidOperationException("No output file was produced by the task");
            }

            string path = Path.Combine(TlogDirectory, WriteTLogFilename);
            TaskItem item = new TaskItem(path);
            CanonicalTrackedOutputFiles trackedFiles = new CanonicalTrackedOutputFiles(new ITaskItem[] {item});

            foreach (ITaskItem sourceItem in Sources) {
                //remove this entry associated with compiled source which is about to be recomputed
                trackedFiles.RemoveEntriesForSource(sourceItem);

                //add entry with updated information
                string upper = Path.GetFullPath(sourceItem.ItemSpec).ToUpperInvariant();
                trackedFiles.AddComputedOutputForSourceRoot(upper, OutputFile);
            }

            //output tlog
            trackedFiles.SaveTlog();

            return trackedFiles;
        }

        protected virtual void OutputReadTLog(ITaskItem[] compiledSources, CanonicalTrackedOutputFiles outputs) {
            string trackerPath = Path.GetFullPath(TlogDirectory + ReadTLogFilenames[0]);

            using (var writer = new StreamWriter(trackerPath, false, Encoding.Unicode)) {
                string sourcePath = "";
                foreach (ITaskItem source in Sources) {
                    if (sourcePath != "")
                        sourcePath += "|";
                    sourcePath += Path.GetFullPath(source.ItemSpec).ToUpperInvariant();
                }

                writer.WriteLine("^" + sourcePath);
                foreach (ITaskItem source in Sources) {
                    writer.WriteLine(Path.GetFullPath(source.ItemSpec).ToUpperInvariant());
                }
                writer.WriteLine(Path.GetFullPath(OutputFile).ToUpperInvariant());
            }
        }

        public override bool Execute() {
            if (!Setup()) {
                return false;
            }

            if (SkippedExecution) {
                return true;
            }

            bool res = ExecuteInternal();

            // Update tracker log files if execution was successful
            if (res) {
                CanonicalTrackedOutputFiles outputs = OutputWriteTLog(compileSourceList);
                OutputReadTLog(compileSourceList, outputs);
            }

            return res;
        }

        protected abstract bool ExecuteInternal();

        protected virtual void SetTrackerLogPaths() {
            if (TLogReadFiles == null) {
                TLogReadFiles = new ITaskItem[ReadTLogFilenames.Length];
                for (int n = 0; n < ReadTLogFilenames.Length; n++) {
                    string readFile = Path.Combine(TlogDirectory, ReadTLogFilenames[n]);
                    TLogReadFiles[n] = new TaskItem(readFile);
                }
            }

            if (this.TLogWriteFiles == null) {
                TLogWriteFiles = new ITaskItem[1];
                string writeFile = Path.Combine(TlogDirectory, WriteTLogFilename);
                TLogWriteFiles[0] = new TaskItem(writeFile);
            }
        }

        protected ITaskItem[] MergeOutOfDateSources(ITaskItem[] outOfDateSourcesFromTracking, List<ITaskItem> outOfDateSourcesFromCommandLineChanges) {
            List<ITaskItem> mergedSources = new List<ITaskItem>(outOfDateSourcesFromTracking);

            foreach (ITaskItem item in outOfDateSourcesFromCommandLineChanges) {
                if (!mergedSources.Contains(item)) {
                    mergedSources.Add(item);
                }
            }

            return mergedSources.ToArray();
        }

        protected void CalcSourcesToBuild() {
            //check if full recompile is required otherwise perform incremental
            if (MinimalRebuildFromTracking == false) {
                CompileSourceList = Sources;
                return;
            }

            //retrieve sources out of date due to tracking
            CanonicalTrackedOutputFiles outputs = new CanonicalTrackedOutputFiles(this, TLogWriteFiles);
            TrackedInputFiles = new CanonicalTrackedInputFiles(this,
                TLogReadFiles,
                Sources,
                ExcludedInputPaths,
                outputs,
                true,
                false);
            ITaskItem[] outOfDateSourcesFromTracking = TrackedInputFiles.ComputeSourcesNeedingCompilation();

            //merge out of date lists
            CompileSourceList = MergeOutOfDateSources(outOfDateSourcesFromTracking, new List<ITaskItem>());
            if (CompileSourceList.Length == 0) {
                SkippedExecution = true;
                return;
            }

            //remove sources to compile from tracked file list
            TrackedInputFiles.RemoveEntriesForSource(CompileSourceList);
            outputs.RemoveEntriesForSource(CompileSourceList);
            TrackedInputFiles.SaveTlog();
            outputs.SaveTlog();
        }

        [Output]
        public bool SkippedExecution {
            get { return this.skippedExecution; }
            set { this.skippedExecution = value; }
        }

        [Output]
        public ITaskItem[] CompileSourceList {
            get { return this.compileSourceList; }
            set { this.compileSourceList = value; }
        }

        protected string TlogDirectory {
            get {
                if (this.TrackerLogDirectory != null) {
                    return this.TrackerLogDirectory.GetMetadata("FullPath");
                }
                return string.Empty;
            }
        }

        public ITaskItem[] TLogReadFiles {
            get { return this.tlogReadFiles; }
            set { this.tlogReadFiles = value; }
        }

        public ITaskItem[] TLogWriteFiles {
            get { return this.tlogWriteFiles; }
            set { this.tlogWriteFiles = value; }
        }

        public bool TrackFileAccess {
            get { return this.trackFileAccess; }
            set { this.trackFileAccess = value; }
        }

        protected CanonicalTrackedInputFiles TrackedInputFiles {
            get { return this.trackedInputFiles; }
            set { this.trackedInputFiles = value; }
        }

        public ITaskItem[] ExcludedInputPaths {
            get { return this.excludedInputPaths; }
            set { this.excludedInputPaths = value; }
        }

        protected abstract string WriteTLogFilename { get; }

        protected abstract string[] ReadTLogFilenames { get; }
    }
}