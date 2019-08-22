try {
    if ($null -ne [InProcess.InMemoryJob]) {
        return
    }
} catch [Exception] {

}

# https://community.idera.com/database-tools/powershell/powertips/b/tips/posts/a-better-and-faster-start-job
$code = @'
using System;
using System.Collections.Generic;
using System.Text;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace InProcess {
	public class InMemoryJob: System.Management.Automation.Job {

		public InMemoryJob(ScriptBlock scriptBlock, string name) {
			_PowerShell = PowerShell.Create().AddScript(scriptBlock.ToString());
			SetUpStreams(name);
		}

		public InMemoryJob(PowerShell PowerShell, string name) {
			_PowerShell = PowerShell;
			SetUpStreams(name);
		}

		private void SetUpStreams(string name) {
			_PowerShell.Streams.Verbose = this.Verbose;
			_PowerShell.Streams.Error = this.Error;
			_PowerShell.Streams.Debug = this.Debug;
			_PowerShell.Streams.Warning = this.Warning;
			_PowerShell.Runspace.AvailabilityChanged += new EventHandler < RunspaceAvailabilityEventArgs > (Runspace_AvailabilityChanged);

            int id = System.Threading.Interlocked.Add(ref InMemoryJobNumber, 1);

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

		PowerShell _PowerShell;
		static int InMemoryJobNumber = 0;

		public override bool HasMoreData {
			get {
				return (Output.Count > 0);
			}
		}

		public override string Location {
			get {
				return "In Process";
			}
		}

		public override string StatusMessage {
			get {
				return "A new status message";
			}
		}

		protected override void Dispose(bool disposing) {
			if (disposing) {
				if (!isDisposed) {
					isDisposed = true;
					try {
						if (!IsFinishedState(JobStateInfo.State)) {
							StopJob();
						}
						foreach(Job job in ChildJobs) {
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
			_PowerShell.Stop();
			_PowerShell.EndInvoke(_asyncResult);
			SetJobState(JobState.Stopped);
		}

		public void Start() {
			_asyncResult = _PowerShell.BeginInvoke < PSObject,
			PSObject > (null, Output);
			SetJobState(JobState.Running);
		}

		IAsyncResult _asyncResult;

		public void WaitJob() {
			_asyncResult.AsyncWaitHandle.WaitOne();
		}

		public void WaitJob(TimeSpan timeout) {
			_asyncResult.AsyncWaitHandle.WaitOne(timeout);
		}
	}
}
'@

{
    param(
        [string]$ThisFileFullPath
    )
    $directory = [System.IO.Path]::GetDirectoryName($ThisFileFullPath)
    $file = [System.IO.Path]::GetFileNameWithoutExtension($ThisFileFullPath)
    $file = "$directory\$file.dll"

    $options = [System.IO.FileOptions]::DeleteOnClose

    if (-not (Test-Path $file)) {
        Add-Type -TypeDefinition $code -OutputAssembly $file
    }

    $fs = [System.IO.FileStream]::new($file, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read -bor [System.IO.FileShare]::Delete, 4096, $options)

    # Root this stream so it does not get garbage collected prematurely
    [System.AppDomain]::CurrentDomain.SetData("PS_IN_PROC_JOB_ASSEMBLY", $fs)

    $length = $fs.Length
    $buffer = [System.Byte[]]::new($length)
    $count = 0
    $sum = 0

    while (($count = $fs.Read($buffer, $sum, $length - $sum)) -gt 0) {
        $sum += $count;
    }

    [System.Reflection.Assembly]::Load($buffer)
}.Invoke($MyInvocation.MyCommand.Path)

function Start-JobInProcess {
    [CmdletBinding()]
    param
    (
        [scriptblock] $ScriptBlock,
        $ArgumentList,
        [string] $Name
    )

    function Get-JobRepository {
        [cmdletbinding()]
        param()
        $pscmdlet.JobRepository
    }

    function Add-Job {
        [cmdletbinding()]
        param
        (
            $job
        )
        $pscmdlet.JobRepository.Add($job)
    }

    if ($ArgumentList) {
        $PowerShell = [PowerShell]::Create().AddScript($ScriptBlock).AddArgument($argumentlist)
        $MemoryJob = [InProcess.InMemoryJob]::new($PowerShell, $Name)
    } else {
        $MemoryJob = [InProcess.InMemoryJob]::new($ScriptBlock, $Name)
    }

    $MemoryJob.Start()
    Add-Job $MemoryJob
    $MemoryJob
}
