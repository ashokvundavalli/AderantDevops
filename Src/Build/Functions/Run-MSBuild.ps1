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

function Exec-CommandCore([string]$command, [string]$commandArgs, [switch]$useConsole = $true, [System.Diagnostics.Process]$parentProcess) {
  $startInfo = New-Object System.Diagnostics.ProcessStartInfo
  $startInfo.FileName = $command
  $startInfo.Arguments = $commandArgs

  $startInfo.UseShellExecute = $false
  $startInfo.WorkingDirectory = Get-Location
  #$startInfo.Verb = "runas"

  if (-not $useConsole) {
    $startInfo.RedirectStandardOutput = $true
    $startInfo.CreateNoWindow = $true
  }

  $process = New-Object System.Diagnostics.Process
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
        Write-Output $line
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
        throw "Command failed to execute: $command $commandArgs"
    }
  }
  finally {
    # If we didn't finish then an error occurred or the user hit ctrl-c.  Either
    # way kill the process
    if (-not $finished) {
      $process.Kill()
    }
    $process.Dispose()
  }
}

function AttachDebuger([System.Diagnostics.Process]$parentProcess, [int]$id) {
    Write-Output "Attaching debugger"
    Add-Type -ReferencedAssemblies "Microsoft.CSharp" -TypeDefinition $Source -Language CSharp 

    $dte = $null
    if ($parentProcess) {
        $dte = [_.DteHelper]::GetDte($parentProcess)
    } else {
        $dte = [_.DteHelper]::GetDteInstances() | Sort-Object -Property Version -Descending | Select-Object -First 1    
    }    

    ($dte.Debugger.LocalProcesses | Where-Object ProcessId -match $id).Attach()
}

# Lets the process re-use the current console. 
# This means items like colored output will function correctly.
function Exec-Console([string]$command, [string]$commandArgs, [System.Diagnostics.Process]$parentProcess) {
    Set-StrictMode -Version 'Latest'
    Exec-CommandCore -command $command -commandArgs $commandArgs -useConsole:$true $parentProcess
}

function Run-MSBuild([string]$projectFilePath, [string]$buildArgs = "", [string]$logFileName = "", [switch]$parallel = $true, [switch]$summary = $true) {
    Set-StrictMode -Version 'Latest'

    $type = [Type]::GetType("System.Management.Automation.PsUtils, System.Management.Automation, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35")
    $method = $type.GetMethod("GetParentProcess", [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Static) 
    $process = $method.Invoke($null, ([System.Diagnostics.Process]::GetCurrentProcess()))

    $debugMode = $false    
    # PowerShell was started from Visual Studio so assume the user wishes to debug
    if ($process.ProcessName -eq "devenv") {
        $debugMode = $true
    }   
    
    if ([System.Diagnostics.Debugger]::IsAttached -or $debugMode) {
        $buildArgs = $buildArgs.Replace("/m", "")
        $buildArgs = "$buildArgs /p:WaitForDebugger=true"
        Exec-Console MSBuild.exe "$projectFilePath $buildArgs" #$process 
    } else {
        Exec-Console MSBuild.exe "$projectFilePath $buildArgs"   
    }

    $process.Dispose()
}
