[CmdletBinding()]
param(
    [string]$Version
)

if (!$Version) {
    return
}

if (([System.Management.Automation.PSTypeName]'InProcess.InMemoryJob').Type) {
    # The type is already defined so bail out
    return
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
            _PowerShell.Runspace.AvailabilityChanged += new EventHandler<RunspaceAvailabilityEventArgs>(Runspace_AvailabilityChanged);

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
    param (
        [string]$ThisFileFullPath
    )
    $directory = [System.IO.Path]::GetDirectoryName($ThisFileFullPath)
    $file = [System.IO.Path]::GetFileNameWithoutExtension($ThisFileFullPath)

    $file = "$directory\$file.dll"

    $options = [System.IO.FileOptions]::DeleteOnClose
    $share = [System.IO.FileShare]::Read -bor [System.IO.FileShare]::Delete

    function Compile {
      [OutputType([bool])]
      param (
        [string] $AssemblyPath,
        [string] $Code
        )

        try {
            if (-not (Test-Path $AssemblyPath)) {
                Add-Type -TypeDefinition $Code -OutputAssembly "$AssemblyPath"
                return $true
            }
        } catch [System.UnauthorizedAccessException] {
             # We should ignore this exception if we got it,
             # the most important reason is that the file has already been
             # scheduled for deletion and will be deleted when all handles
             # are closed.
        }

        Add-Type -TypeDefinition $Code
        return $false
    }

    $compiled = $false
    $useMemoryMappedFile = $true
    $mapName = "InProcessJobs.ps1-$($Version)"
    $encoding = [System.Text.Encoding]::UTF8
    [System.Byte[]]$buffer = $null

    [void][System.Reflection.Assembly]::Load("System.IO.MemoryMappedFiles, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
    $memoryMappedFile = $null

    # Performance optimization. To avoid the slow PowerShell C# compiler we
    # store the last seen outputs in a MMF. Here we try to open that MMF and load the assembly data
    try {
        Write-Debug "Attempting to open MMF: $mapName"
        $memoryMappedFile = [System.IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting($mapName)
    } catch [System.IO.FileNotFoundException] {
        Write-Debug "Failed to open MMF: $mapName"

        try {
            $compiled = Compile $file $code
        } catch [System.Exception] {
            $useMemoryMappedFile = $false
        }

        if ($compiled -and $useMemoryMappedFile) {
            [System.IO.File]::SetAttributes($file, [System.IO.File]::GetAttributes($file) -bor [System.IO.FileAttributes]::NotContentIndexed -bor [System.IO.FileAttributes]::Temporary)
            $fs = [System.IO.FileStream]::new($file, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, $share, 4096, $options)

            $memoryMappedFile = [System.IO.MemoryMappedFiles.MemoryMappedFile]::CreateNew($mapName, 10000)
            $memoryMappedStream = $memoryMappedFile.CreateViewStream()

            # Write the assembly length to the first 8 bytes of the stream
            $writer = [System.IO.BinaryWriter]::new($memoryMappedStream, $encoding, $true)
            $writer.Write($fs.Length)
            $writer.Dispose()

            $pos = $memoryMappedStream.Position
            $fs.CopyTo($memoryMappedStream)

            $memoryMappedStream.Position = $pos

            $buffer = [System.Byte[]]::new($fs.Length)
            $memoryMappedStream.Read($buffer, 0, $fs.Length)

            [System.AppDomain]::CurrentDomain.SetData("IN_PROCESS_JOB_FILE", $fs)
        }
    }

    if (!$compiled -and $null -ne $memoryMappedFile) {
        $memoryMappedStream = $memoryMappedFile.CreateViewStream()

        # Read the assembly length and then the assembly data
        $reader = [System.IO.BinaryReader]::new($memoryMappedStream, $encoding, $true)
        $length = $reader.ReadInt64()
        $buffer = $reader.ReadBytes($length)
    }

    if ($null -ne $memoryMappedFile) {
        # GC root so it does not get garbage collected prematurely
        [System.AppDomain]::CurrentDomain.SetData("IN_PROCESS_JOB_MMF", $memoryMappedFile)
    }

    if ($null -ne $buffer) {
        [System.Reflection.Assembly]::Load($buffer)
    }

}.Invoke($MyInvocation.MyCommand.Path)

function Start-JobInProcess {
    [CmdletBinding()]
    param
    (
        [ScriptBlock] $ScriptBlock,
        $ArgumentList,
        [string] $Name
    )

    function Get-JobRepository {
        [CmdletBinding()]
        param()
        $pscmdlet.JobRepository
    }

    function Add-Job {
        [CmdletBinding()]
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