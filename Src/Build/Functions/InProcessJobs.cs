// https://community.idera.com/database-tools/powershell/powertips/b/tips/posts/a-better-and-faster-start-job
using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace InProcess {
    public class InMemoryJob : System.Management.Automation.Job {
        public InMemoryJob(ScriptBlock scriptBlock, string name) {
            powerShell = PowerShell.Create().AddScript(scriptBlock.ToString());
            SetUpStreams(name);
        }

        public InMemoryJob(PowerShell powerShell, string name) {
            this.powerShell = powerShell;
            SetUpStreams(name);
        }

        private void SetUpStreams(string name) {
            powerShell.Streams.Verbose = this.Verbose;
            powerShell.Streams.Error = this.Error;
            powerShell.Streams.Debug = this.Debug;
            powerShell.Streams.Warning = this.Warning;
            powerShell.Streams.Information = this.Information;
            powerShell.Runspace.AvailabilityChanged += new EventHandler<RunspaceAvailabilityEventArgs>(Runspace_AvailabilityChanged);

            int id = System.Threading.Interlocked.Add(ref inMemoryJobNumber, 1);

            if (!string.IsNullOrEmpty(name)) {
                this.Name = name;
            } else {
                this.Name = "InProcessJob" + id;
            }
        }

        void Runspace_AvailabilityChanged(object sender, RunspaceAvailabilityEventArgs e) {
            if (e.RunspaceAvailability == RunspaceAvailability.Available) {
                this.SetJobState(JobState.Completed);
            }
        }

        PowerShell powerShell;
        static int inMemoryJobNumber = 0;

        public override bool HasMoreData {
            get { return (Output.Count > 0); }
        }

        public override string Location {
            get { return "In Process"; }
        }

        public override string StatusMessage {
            get { return "A new status message"; }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                if (!isDisposed) {
                    isDisposed = true;
                    try {
                        if (!IsFinishedState(JobStateInfo.State)) {
                            StopJob();
                        }

                        foreach (Job job in ChildJobs) {
                            job.Dispose();
                        }
                    } finally {
                        base.Dispose(disposing);
                    }
                }
            }
        }

        private bool isDisposed = false;

        internal bool IsFinishedState(JobState state) {
            return (state == JobState.Completed || state == JobState.Failed || state == JobState.Stopped);
        }

        public override void StopJob() {
            powerShell.Stop();
            powerShell.EndInvoke(asyncResult);
            SetJobState(JobState.Stopped);
        }

        public void Start() {
            asyncResult = powerShell.BeginInvoke<PSObject, PSObject>(null, Output);
            SetJobState(JobState.Running);
        }

        IAsyncResult asyncResult;

        public void WaitJob() {
            asyncResult.AsyncWaitHandle.WaitOne();
        }

        public void WaitJob(TimeSpan timeout) {
            asyncResult.AsyncWaitHandle.WaitOne(timeout);
        }
    }
}