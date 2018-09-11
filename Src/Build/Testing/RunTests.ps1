[CmdletBinding()]
param(      
  [Parameter(Mandatory=$true)]
  [string]$PathToTestTool,

  [Parameter(Mandatory=$false)]   
  [string]$ToolArgs,

  [Parameter(Mandatory=$true)]
  [ValidateNotNullOrEmpty()]
  [string]$WorkingDirectory,

  [Parameter(Mandatory=$false)]
  [bool]$IsDesktopBuild,
  
  [Parameter(Mandatory=$false, ValueFromRemainingArguments=$true)]
  [string[]]
  $TestAssemblies
)

function GetTestResultFiles() {
    return Get-ChildItem -LiteralPath "$WorkingDirectory\TestResults" -Filter "*.trx" -ErrorAction SilentlyContinue
}

function GenerateHtmlReport() {
    $afterRunTrxFiles = GetTestResultFiles

    if ($beforeRunTrxFiles) {    
        $newTrxFile = $afterRunTrxFiles.FullName | ? {!($beforeRunTrxFiles.FullName -contains $_)}
    } else {
        $newTrxFile = $afterRunTrxFiles.FullName
    }

    if ($newTrxFile) {
        & "$PSScriptRoot\..\..\Build.Tools\TrxerConsole.exe" $newTrxFile

        $report = "$newTrxFile.html"
        if (Test-Path $report) {
            & "start" $report
        }
    }
}

$beforeRunTrxFiles = GetTestResultFiles

Set-StrictMode -Version Latest

$global:LASTEXITCODE = -1

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $PathToTestTool
$startInfo.Arguments = "$ToolArgs $TestAssemblies"
$startInfo.WorkingDirectory = $WorkingDirectory

# Implemented in C# for performance
try {
    $runner = [Aderant.Build.Utilities.ProcessRunner]::RunTestProcess($startInfo)
    $runner.RecordConsoleOutput = $true
    $runner.Start()

    $global:LASTEXITCODE = $runner.Wait([System.Timespan]::FromMinutes(20).TotalMilliseconds)

    if ($global:BuildLogFile) {
        # Replay the log to MSBuild
        #Add-Content -LiteralPath $global:BuildLogFile -Value $runner.ConsoleOutput
    }
} finally {
    $runner.Dispose()

    if ($global:LASTEXITCODE -ne 0) {
        if ($IsDesktopBuild) {
            GenerateHtmlReport
        }

        Set-BuildStatus -Status "Failed" -Reason "Test failure"
    }
}
