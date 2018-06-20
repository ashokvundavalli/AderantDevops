function Exec-CommandCore([string]$command, [string]$commandArgs, [switch]$useConsole = $true) {
  $startInfo = New-Object System.Diagnostics.ProcessStartInfo
  $startInfo.FileName = $command
  $startInfo.Arguments = $commandArgs

  $startInfo.UseShellExecute = $false
  $startInfo.WorkingDirectory = Get-Location
  $startInfo.Verb = "runas"

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


# Lets the process re-use the current console. 
# This means items like colored output will function correctly.
function Exec-Console([string]$command, [string]$commandArgs) {
    Set-StrictMode -Version 'Latest'
    Exec-CommandCore -command $command -commandArgs $commandArgs -useConsole:$true
}

function Run-MSBuild([string]$projectFilePath, [string]$buildArgs = "", [string]$logFileName = "", [switch]$parallel = $true, [switch]$summary = $true) {  
    Set-StrictMode -Version 'Latest'
    
    if ([System.Diagnostics.Debugger]::IsAttached) {
        Exec-Console vsjitdebugger.exe "MSBuild.exe $projectFilePath $buildArgs"   
    } else {
        Exec-Console vsjitdebugger.exe "MSBuild.exe $projectFilePath $buildArgs"   
    }
}
