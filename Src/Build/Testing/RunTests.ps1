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

  [Parameter(Mandatory=$false)]
  [string]
  $SolutionRoot,

  [Parameter(Mandatory=$false)]
  [string[]]
  $ReferencePaths,
  
  [Parameter(Mandatory=$false, ValueFromRemainingArguments=$true)]
  [string[]]
  $TestAssemblies
)

function CreateRunSettingsFile() {
    [xml]$xml = Get-Content -Path "$PSScriptRoot\default.runsettings"
    $assemblyResolution = $xml.RunSettings.MSTest.AssemblyResolution

    if (-not $ReferencePaths -and $SolutionRoot) {
        $ReferencePaths = @([System.IO.Path]::Combine($SolutionRoot, "packages"), [System.IO.Path]::Combine($SolutionRoot, "dependencies"))
    }

    if ($ReferencePaths) {
        foreach ($path in $ReferencePaths) {
            $directoryElement = $xml.CreateElement("Directory")
            $directoryElement.SetAttribute("path", $path.TrimEnd('\'))
            $directoryElement.SetAttribute("includeSubDirectories", "true")

            [void]$assemblyResolution.AppendChild($directoryElement)
        }
    }

    $sw = [System.IO.StringWriter]::new()
    $writer = New-Object System.Xml.XmlTextWriter($sw)
    $writer.Formatting = [System.Xml.Formatting]::Indented
    $xml.WriteContentTo($writer)

    return $sw.ToString()
}

function GetTestResultFiles() {
    return Get-ChildItem -LiteralPath "$WorkingDirectory\TestResults" -Filter "*.trx" -ErrorAction SilentlyContinue
}

function GenerateHtmlReport() {
    $afterRunTrxFiles = GetTestResultFiles

    if ($afterRunTrxFiles -eq $null) {
        Write-Output "Skipped generating HTML report - no .trx files present in directory: '$WorkingDirectory\TestResults'."
        return
    }

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

try {
    $xml = CreateRunSettingsFile

    Write-Output ([System.Environment]::NewLine + "$xml")

    $runSettingsFile = [System.IO.Path]::GetTempFileName()
    Add-Content -LiteralPath $runSettingsFile -Value $xml -Encoding UTF8

    #/Diag:C:\temp\test.log"

    $startInfo.Arguments += " /Settings:$runSettingsFile" 
    Write-Output "$($startInfo.FileName) $($startInfo.Arguments)"

    # Implemented in C# for performance
    $runner = [Aderant.Build.Utilities.ProcessRunner]::RunTestProcess($startInfo)
    $runner.RecordConsoleOutput = $true
    $runner.Start()

    #$fn = $startInfo.FileName
    #$arg = $startInfo.Arguments

    #Start-Process -FilePath $startInfo.FileName -ArgumentList $startInfo.Arguments -Wait -NoNewWindow

    $global:LASTEXITCODE = $runner.Wait([System.Timespan]::FromMinutes(20).TotalMilliseconds)   
} finally {
    if ($runner.HasExited -eq $false) {
        $global:LASTEXITCODE = $runner.Kill()
    }

    [System.IO.File]::Delete($runSettingsFile)

    if ($global:LASTEXITCODE -ne 0) {
        if ($IsDesktopBuild) {
            GenerateHtmlReport
        }        

        Write-Error "Test execution exit code: $($global:LASTEXITCODE)"
    } 
}
