Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

$Source = @"
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace _ {

public static class DteHelper {

        public static object GetDte(Process process) {
            var instances = GetDteInstances();
            foreach (dynamic dte in instances) {
                int hWnd = (int)dte.MainWindow.HWnd;

                int processId;
                GetWindowThreadProcessId(new IntPtr(hWnd), out processId);
                if (processId == process.Id) {
                    return dte;
                }
            }

            return null;
        }

        public static IEnumerable<object> GetDteInstances() {
            IRunningObjectTable rot;
            IEnumMoniker enumMoniker = null;
            int retVal = GetRunningObjectTable(0, out rot);
            try {
                if (retVal == 0) {
                    rot.EnumRunning(out enumMoniker);

                    IntPtr fetched = IntPtr.Zero;
                    IMoniker[] moniker = new IMoniker[1];
                    while (enumMoniker.Next(1, moniker, fetched) == 0) {
                        IBindCtx bindCtx;
                        CreateBindCtx(0, out bindCtx);
                        string displayName;
                        moniker[0].GetDisplayName(bindCtx, null, out displayName);
                        bool isVisualStudio = displayName.StartsWith("!VisualStudio");

                        try {
                            if (isVisualStudio) {
                                object outDte = null;
                                rot.GetObject(moniker[0], out outDte);
                                yield return outDte;
                            }
                        } finally {
                            if (bindCtx != null) Marshal.ReleaseComObject(bindCtx);
                        }
                    }
                }
            } finally {
                if (enumMoniker != null) Marshal.ReleaseComObject(enumMoniker);
                if (rot != null) Marshal.ReleaseComObject(rot);
            }
        }

        [DllImport("ole32.dll")]
        private static extern void CreateBindCtx(int reserved, out IBindCtx ppbc);

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
    }
}
"@

function Exec-CommandCore([string]$command, [string]$commandArgs, [switch]$useConsole = $true, [HashTable]$variables, [System.Diagnostics.Process]$parentProcess) {
    if (-not (Test-Path $command)) {
        throw "Tool not found $command"
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $command
    $startInfo.Arguments = $commandArgs

    Write-Host "$command $commandArgs"

    $startInfo.UseShellExecute = $false
    $startInfo.WorkingDirectory = (Get-Location).Path

    $variables.GetEnumerator().ForEach({ $startInfo.Environment[$_.Key] = $_.Value })

    if (-not $useConsole) {
        $startInfo.RedirectStandardOutput = $true
        $startInfo.CreateNoWindow = $true
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $process.Start() | Out-Null

    $finished = $false
    try {
        if (-not $useConsole) {
            # The OutputDataReceived event doesn't fire as events are sent by the
            # process in powershell.  Possibly due to subtleties of how Powershell
            # manages the thread pool that I'm not aware of.  Using blocking
            # reading here as an alternative which is fine since this blocks
            # on completion already.
            $out = $process.StandardOutput
            while (-not $out.EndOfStream) {
                $line = $out.ReadLine()
                Write-Information -MessageData $line
            }
        }

        while (-not $process.WaitForExit(100)) {
            if ($parentProcess) {
                AttachDebuger $parentProcess $process.Id
                $parentProcess = $null
            }
            # Non-blocking loop done to allow ctr-c interrupts
        }

        $finished = $true
        if ($process.ExitCode -ne 0) {
            throw "Command failed to execute successfully: $command $commandArgs"
        }
    } finally {
        # If we didn't finish then an error occurred or the user hit ctrl-c.  Either
        # way kill the process
        if (-not $finished) {
            $process.Kill()
        }
        $process.Dispose()
    }
}

function AttachDebuger([System.Diagnostics.Process]$parentProcess, [int]$id) {
    Write-Information "Attaching debugger"
    Add-Type -ReferencedAssemblies "Microsoft.CSharp" -TypeDefinition $Source -Language CSharp

    $dte = $null
    if ($parentProcess) {
        $dte = [_.DteHelper]::GetDte($parentProcess)
    } else {
        $dte = [_.DteHelper]::GetDteInstances() | Sort-Object -Property Version -Descending | Select-Object -First 1
    }

    ($dte.Debugger.LocalProcesses | Where-Object ProcessId -EQ $id).Attach()
}

# Lets the process re-use the current console.
# This means items like colored output will function correctly.
function Exec-Console([string]$command, [string]$commandArgs, [HashTable]$variables, [System.Diagnostics.Process]$parentProcess) {
    Exec-CommandCore -command $command -commandArgs $commandArgs -useConsole:$true -variables:$variables -parentProcess:$parentProcess
}

function Run-MSBuild([string]$projectFilePath, [string]$buildArgs = "", [string]$logFileName = "", [bool]$isDesktopBuild = $true) {

    try {
        $type = [Type]::GetType("System.Management.Automation.PsUtils, System.Management.Automation, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35")
        $method = $type.GetMethod("GetParentProcess", [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Static)
        $process = $method.Invoke($null, ([System.Diagnostics.Process]::GetCurrentProcess()))

        $debugMode = $false
        if ($null -ne $process) {
            if ($process.ProcessName -eq "devenv") {
                Write-Information -MessageData "PowerShell was started from Visual Studio ... assuming you wish to debug"
                $debugMode = $true
            } else {
                Write-Information -MessageData "Process was started from: $($process.Name)"
                $process = $null
            }
        }

    } catch {
        throw "Problem occurred while getting the current process: $($_.Exception.Message). Please try restarting your PowerShell instance."
    }

    #     As part of our builds, quite a few projects copy files to the binaries directory or other locations.
    #     These can be anything from image files to test scripts.  To have our builds complete more quickly, we use the multi-process option (/maxcpucount) of msbuild to build projects in parallel.
    #     This all sounds normal, so what's the problem?  In a large team, people will sometimes inadvertently add statements to different project files that copy files to the same destination.
    #     When those project files have no references to each other, directly or indirectly, msbuild may build them in parallel.
    #     If it does happen to run those projects in parallel on different nodes and the copies happen at the same time, the build breaks because one copy succeeds and one fails.
    #     Since the timing is not going to be the same on every build, the result is random build breaks. Build breaks suck. They drain the productivity of the team and are frustrating.
    #
    #     There also appears to be a race condition inside MSBuild that we come across from time to time where the same destination path is used in more than one copy.
    #
    #     Consider this log snippet
    #
    #  441>_CopyFilesMarkedCopyLocal:
    #        Copying file from "e:\B\90\7659\src\Libraries.Entities.Bill\packages\ThirdParty.NHibernate\lib\NHibernate.xml" to "..\..\Bin\Test\NHibernate.xml".
    #      _CopyOutOfDateSourceItemsToOutputDirectory:
    #        Creating directory "..\..\Bin\Test\Installation\CmsDbScripts".
    #        Copying file from "e:\B\90\7659\src\Libraries.Entities.Bill\Src\Aderant.Bill.Library\Installation\CmsDbScripts\BillModel_InquiryGetMatterAgingColumnLabels.sql" to "..\..\Bin\Test\Installation\CmsDbScripts\BillModel_InquiryGetMatterAgingColumnLabels.sql".
    #        Copying file from "e:\B\90\7659\src\Libraries.Entities.Bill\Src\Aderant.Bill.Library\Installation\CmsDbScripts\BillModel_InquiryMatterArAging.sql" to "..\..\Bin\Test\Installation\CmsDbScripts\BillModel_InquiryMatterArAging.sql".
    #        Copying file from "e:\B\90\7659\src\Libraries.Entities.Bill\Src\Aderant.Bill.Library\Installation\CmsDbScripts\BillModel_InquiryMatterWipAging.sql" to "..\..\Bin\Test\Installation\CmsDbScripts\BillModel_InquiryMatterWipAging.sql".
    #  440>_CopyOutOfDateSourceItemsToOutputDirectory:
    #        Copying file from "e:\B\90\7659\src\Libraries.Entities.Bill\Src\Aderant.Bill.Library\Installation\CmsDbScripts\BillModel_InquiryGetMatterAgingColumnLabels.sql" to "..\..\Bin\Test\Installation\CmsDbScripts\BillModel_InquiryGetMatterAgingColumnLabels.sql".
    #        Copying file from "e:\B\90\7659\src\Libraries.Entities.Bill\Src\Aderant.Bill.Library\Installation\CmsDbScripts\BillModel_InquiryMatterArAging.sql" to "..\..\Bin\Test\Installation\CmsDbScripts\BillModel_InquiryMatterArAging.sql".
    #  441>CopyFilesToOutputDirectory:
    #        Copying file from "obj\Release\UnitTest.Bill.Library.dll" to "..\..\Bin\Test\UnitTest.Bill.Library.dll".
    #        UnitTest.Bill.Library -> e:\B\90\7659\src\Libraries.Entities.Bill\Bin\Test\UnitTest.Bill.Library.dll
    #        Copying file from "obj\Release\UnitTest.Bill.Library.pdb" to "..\..\Bin\Test\UnitTest.Bill.Library.pdb".
    #  440>C:\Program Files (x86)\MSBuild\14.0\bin\Microsoft.Common.CurrentVersion.targets(4106,5): error MSB3021: Unable to copy file "e:\B\90\7659\src\Libraries.Entities.Bill\Src\Aderant.Bill.Library\Installation\CmsDbScripts\BillModel_InquiryMatterArAging.sql" to "..\..\Bin\Test\Installation\CmsDbScripts\BillModel_InquiryMatterArAging.sql". Access to the path '..\..\Bin\Test\Installation\CmsDbScripts\BillModel_InquiryMatterArAging.sql' is denied. [e:\B\90\7659\src\Libraries.Entities.Bill\Test\IntegrationTest.Bill.Library\IntegrationTest.Bill.Library.csproj]
    #        Copying file from "e:\B\90\7659\src\Libraries.Entities.Bill\Src\Aderant.Bill.Library\Installation\CmsDbScripts\BillModel_InquiryMatterWipAging.sql" to "..\..\Bin\Test\Installation\CmsDbScripts\BillModel_InquiryMatterWipAging.sql".
    #
    #     There are two nodes running, 440 and 441.
    #     For some reason both 441 and 440 schedule the copy of BillModel_InquiryMatterWipAging.sql to the output even though a node should only work on a single target at a time
    #     and thus a single node should be processing the source items of a project at any given time.

    $environmentBlock = @{"MSBUILDALWAYSRETRY" = "1"}

    if ([System.Diagnostics.Debugger]::IsAttached -or $debugMode) {
        $buildArgs = $buildArgs -ireplace "\/m:([^\s]+)","" #replace /m:<n> arg. This makes debugging easier as only 1 process is spawned
        $buildArgs = "$buildArgs /p:WaitForDebugger=true"
    }

    $path = Resolve-MSBuild "*" "x86"

    $tool = "$path\MSBuild.exe"

    if ($logFileName) {
        $supportsBinaryLogger = ((Get-Item $tool).VersionInfo.FileMajorPart -gt 14)

        if (-not $supportsBinaryLogger) {
            $buildArgs += " /fl /flp:logfile=$logFileName;Encoding=UTF-8"
        } else {
            $logFileName = [System.IO.Path]::ChangeExtension($logFileName, ".binlog")
            $buildArgs += " /bl:$logFileName"
        }
    }

    try {
        Exec-Console $tool "$projectFilePath $buildArgs" $environmentBlock $process
    } finally {
        if (Test-Path -Path $logFileName) {
            if ([System.Environment]::UserInteractive) {
                Write-Host "Build log written to: $logFileName"
            } else {
                # Binlogs for full builds can be 1+ GB
                #Write-Host "##vso[task.uploadfile]$logFileName"
            }
        }

        if ($process) {
            $process.Dispose()
        }
    }
}