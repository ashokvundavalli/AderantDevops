function Exec-CommandCore {
  param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$command,
    [string]$commandArgs,
    [switch]$useConsole = $true,
    [HashTable]$variables,
    [System.Diagnostics.Process]$parentProcess,
    [Action[string]]$outputHandler
  )

  if (-not (Test-Path $command)) {
    throw "Tool not found $command"
  }  

  $global:LASTEXITCODE = -1

  $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
  $startInfo.FileName = $command
  $startInfo.Arguments = $commandArgs

  $startInfo.UseShellExecute = $false
  $startInfo.WorkingDirectory = Get-Location

  if ($variables) {
    $variables.GetEnumerator().ForEach({ $startInfo.Environment[$_.Key] = $_.Value })
  }
  
  
  [Aderant.Build.Utilities.ProcessRunner]::RunTestProcess($startInfo)  
  return

  if (-not $useConsole) {
    $startInfo.RedirectStandardOutput = $true
    $startInfo.CreateNoWindow = $true
  }

  $process = New-Object System.Diagnostics.Process
  $process.StartInfo = $startInfo
  [void]$process.Start()

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

    $global:LASTEXITCODE = $process.ExitCode

    if ($process.ExitCode -ne 0) {        
        throw "Command failed to execute successfully: $command $commandArgs"
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
function Exec-Console([string]$command, [string]$commandArgs, [HashTable]$variables, [System.Diagnostics.Process]$parentProcess, [Action[string]]$outputHandler) {
    Set-StrictMode -Version 'Latest'
    Exec-CommandCore -command $command -commandArgs $commandArgs -useConsole:$true -variables:$variables -parentProcess:$parentProcess -outputHandler:$outputHandler
}