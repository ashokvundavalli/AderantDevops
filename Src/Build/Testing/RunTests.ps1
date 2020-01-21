﻿[CmdletBinding()]
param(
  [Parameter(Mandatory=$true, HelpMessage="The path to the test runner such as vstest.console.exe")]
  [string]$PathToTestTool,

  [Parameter(Mandatory=$false, HelpMessage="The tool arguments such as the logger type")]
  [string]$ToolArgs,

  [Parameter(Mandatory=$true)]
  [ValidateNotNullOrEmpty()]
  [string]$WorkingDirectory,

  [Parameter(Mandatory=$false)]
  [bool]$IsDesktopBuild,

  [Parameter(Mandatory=$false)]
  [string]
  $SolutionRoot,

  [Parameter(Mandatory=$false, HelpMessage="The paths to provide to the test tool assembly resolver")]
  [string[]]
  $ReferencePaths,

  [Parameter(Mandatory=$false)]
  [Hashtable]
  $AdditionalEnvironmentVariables,

  [Parameter(Mandatory=$false)]
  [bool]
  $RunInParallel,

  [Parameter(Mandatory=$false, ValueFromRemainingArguments=$true)]
  [string[]]
  $TestAssemblies,

  [Parameter(Mandatory=$false)]
  [string]
  $TestAdapterPath,

  [Parameter(Mandatory=$false)]
  [int]
  $TestSessionTimeout = 1200000
)

Set-StrictMode -Version "Latest"
$InformationPreference = "Continue"
$ErrorActionPreference = "Stop"

$referencePathList = [System.Collections.Generic.List[string]]::new()

function AddSearchDirectory($element, [string]$path, [bool]$includeSubDirectories, [bool]$prepend) {
    $directoryElement = $script:settingsDocument.CreateElement("Directory")
    $directoryElement.SetAttribute("path", $path.TrimEnd('\'))
    $directoryElement.SetAttribute("includeSubDirectories", "$includeSubDirectories")

    if ($prepend) {
        [void]$element.PrependChild($directoryElement)
    } else {
        [void]$element.AppendChild($directoryElement)
    }
}

function CreateRunSettingsXml() {
    [xml]$script:settingsDocument = Get-Content -Path "$PSScriptRoot\default.runsettings"
    $assemblyResolution = $settingsDocument.RunSettings.MSTest.AssemblyResolution

    if ($script:ReferencePaths) {
        foreach ($path in $script:ReferencePaths) {
            AddSearchDirectory $assemblyResolution $path -includeSubDirectories:$false -prepend:$false
        }
    }

    if ($SolutionRoot) {
        #AddSearchDirectory $assemblyResolution ([System.IO.Path]::Combine($SolutionRoot, "packages")) $false $false

        # We want the test runner resolver to bind content produced by the solution build
        # for the most reliable test run as there could be matching by older assemblies in the other directories
        AddSearchDirectory $assemblyResolution ([System.IO.Path]::Combine($SolutionRoot, "Bin", "Module")) -includeSubDirectories:$true -prepend:$true
    }

    # VS SDK
    AddSearchDirectory $assemblyResolution ("$Env:VSSDK140Install" + "VisualStudioIntegration\Common\Assemblies\v4.0") -includeSubDirectories:$false -prepend:$false

    if ($script:RunInParallel -eq $false) {
        $settingsDocument.RunSettings.RunConfiguration.MaxCpuCount = '1'
    }

    $settingsDocument.RunSettings.RunConfiguration.TestSessionTimeout = $TestSessionTimeout.ToString()

    $sw = [System.IO.StringWriter]::new()
    $writer = New-Object System.Xml.XmlTextWriter($sw)
    $writer.Formatting = [System.Xml.Formatting]::Indented
    $settingsDocument.WriteContentTo($writer)

    $writer.Dispose()
    $sw.Dispose()

    return $sw.ToString()
}

# Finds assemblies references by the test container in our search space then drops them into the conntainer directory.
# This is a fall back for DLLs which are needed for JIT and the VSTest resolver may not be able to locate the assemblies
function FindAndDeployReferences([string[]] $testAssemblies) {
    if ($ReferencesToFind -and $referencePathList) {
        Write-Information "Finding references... $ReferencesToFind"

        [void][System.Reflection.Assembly]::Load("System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
        # Create the paths to drop references into
        $destinationPaths = [System.Collections.Generic.HashSet[System.String]]::new()
        foreach ($testAssemblyFile in $testAssemblies) {
            $file = [System.IO.FileInfo]::new($testAssemblyFile)
            [void]$destinationPaths.Add($file.Directory.FullName)
        }

        $files = @()
        foreach ($path in $referencePathList) {
            if ([System.IO.Directory]::Exists($path)) {
                $files += Get-ChildItem -LiteralPath $path -Filter "*.dll" -Recurse
            }
        }

        foreach ($reference in $ReferencesToFind) {
            # To be correct we should also support .exe and .winmd...
            # Avoid ChangeExtension(...) as assemblies often have dots in the name which confuses it
            $dllName = $reference + ".dll"

            $foundReferenceFile = $files | Where-Object -Property Name -EQ $dllName | Select-Object -First 1

            if ($foundReferenceFile) {
                foreach ($path in $destinationPaths) {
                    $destinationFile = [System.IO.Path]::Combine($path, $dllName)

                    if (-not [System.IO.File]::Exists($destinationFile)) {
                        Write-Information "Found required file $($foundReferenceFile.FullName)"
                        New-Item -Path $destinationFile -ItemType HardLink -Value $foundReferenceFile.FullName | Out-Null
                    }
                }
            }
        }
    }
}

function GetTestResultFiles() {
    $path = "$WorkingDirectory\TestResults"
    [System.IO.Directory]::CreateDirectory($path)
    return Get-ChildItem -LiteralPath $path -Filter "*.trx" -ErrorAction SilentlyContinue
}

function ShowTestRunReport() {
    $afterRunTrxFiles = GetTestResultFiles

    if ($null -eq $afterRunTrxFiles) {
        Write-Output "Skipped generating HTML report - no .trx files present in directory: '$WorkingDirectory\TestResults'."
        return
    }

    if ($beforeRunTrxFiles) {
        $newTrxFile = $afterRunTrxFiles.FullName | Where-Object {!($beforeRunTrxFiles.FullName -contains $_)}
    } else {
        $newTrxFile = $afterRunTrxFiles.FullName
    }

    if ($newTrxFile) {
        & "$PSScriptRoot\..\..\Build.Tools\TrxerConsole.exe" $newTrxFile

        $report = "$newTrxFile.html"
        if (Test-Path $report) {
            & Start-Process $report
        }
    }
}

$beforeRunTrxFiles = GetTestResultFiles

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = '"' + $PathToTestTool + '"'
$startInfo.Arguments = "$ToolArgs $TestAssemblies"
$startInfo.WorkingDirectory = $WorkingDirectory
$startInfo.Environment["EXPERT_MODULE_DIRECTORY"] = $SolutionRoot
$startInfo.Environment["BUILD_SOLUTION_ROOT"] = $SolutionRoot

if ($null -ne $AdditionalEnvironmentVariables -and $AdditionalEnvironmentVariables.Count -gt 0) {
    foreach ($variable in $AdditionalEnvironmentVariables.GetEnumerator()) {
        $startInfo.Environment[$variable.Key] = $variable.Value
    }
}

$global:LASTEXITCODE = 0
$runSettingsFile = ""

try {
    Write-Information "Creating run settings"
    $xml = CreateRunSettingsXml
    Write-Information ([System.Environment]::NewLine + "$xml")

    $runSettingsFile = [System.IO.Path]::GetTempFileName()
    Add-Content -LiteralPath $runSettingsFile -Value $xml -Encoding UTF8
    $startInfo.Arguments += " /Settings:$runSettingsFile"

    if (-not [string]::IsNullOrWhiteSpace($TestAdapterPath)) {
        $startInfo.Arguments += " /TestAdapterPath:$TestAdapterPath"
    }

    Write-Information "Starting runner: $($startInfo.FileName) $($startInfo.Arguments)"

    $global:LASTEXITCODE = $exec.Invoke($startInfo)
} finally {
    $lastExitCode = $global:LASTEXITCODE

    if ($lastExitCode -eq 0) {
        try {
            if ($runSettingsFile) {
                [System.IO.File]::Delete($runSettingsFile)
            }
        } catch {
            Write-Debug "Failed to delete temporary run settings file $runSettingsFile"
        }

        try {
            if ([System.IO.Directory]::Exists($TestAdapterPath)) {
                Remove-Item -Path $TestAdapterPath -Force
            }
        } catch {
            Write-Debug "Failed to delete temporary directory to test adapter $TestAdapterPath"
        }
    }

    if ($lastExitCode -ne 0) {
        if ($IsDesktopBuild) {
            ShowTestRunReport
        }

        Write-Error "Test runner exit code: $lastExitCode"
    } else {
        if ($Error) {
            throw $Error[0]
        }
    }
}