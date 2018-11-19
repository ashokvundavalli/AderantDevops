[CmdletBinding()]
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
  [string[]]
  $ReferencesToFind,

  [Parameter(Mandatory=$false, ValueFromRemainingArguments=$true)]
  [string[]]
  $TestAssemblies
)

Set-StrictMode -Version "Latest"
$InformationPreference = "Continue"

$referencePathList = [System.Collections.Generic.List[string]]::new()

function CreateRunSettingsXml() {
    [xml]$xml = Get-Content -Path "$PSScriptRoot\default.runsettings"
    $assemblyResolution = $xml.RunSettings.MSTest.AssemblyResolution

    if ($script:ReferencePaths) {
        $referencePathList.AddRange($ReferencePaths)
    }

    if ($SolutionRoot) {
        $referencePathList.Add([System.IO.Path]::Combine($SolutionRoot, "packages"))

        # We want the test runner resolver to bind content produced by the solution build
        # for the most reliable test run as there could be matching by older assemblies in the other directories
        $referencePathList.Insert([System.IO.Path]::Combine($SolutionRoot, "Bin", "Module"))
    }

    foreach ($path in $referencePathList) {
        $directoryElement = $xml.CreateElement("Directory")
        $directoryElement.SetAttribute("path", $path.TrimEnd('\'))
        $directoryElement.SetAttribute("includeSubDirectories", "true")

        [void]$assemblyResolution.AppendChild($directoryElement)
    }

    $sw = [System.IO.StringWriter]::new()
    $writer = New-Object System.Xml.XmlTextWriter($sw)
    $writer.Formatting = [System.Xml.Formatting]::Indented
    $xml.WriteContentTo($writer)

    return $sw.ToString()
}

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

        # Find the reference in our search space then drop it into our directory which contains the test assembly
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

Set-StrictMode -Version Latest

$started = $false
$beforeRunTrxFiles = GetTestResultFiles

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = '"' + $PathToTestTool + '"'
$startInfo.Arguments = "$ToolArgs $TestAssemblies"
$startInfo.WorkingDirectory = $WorkingDirectory
$startInfo.Environment["EXPERT_MODULE_DIRECTORY"] = $SolutionRoot

$global:LASTEXITCODE = 0

try {
    Write-Information "Creating run settings"
    $xml = CreateRunSettingsXml
    Write-Information ([System.Environment]::NewLine + "$xml")

    $runSettingsFile = [System.IO.Path]::GetTempFileName()
    Add-Content -LiteralPath $runSettingsFile -Value $xml -Encoding UTF8
    $startInfo.Arguments += " /Settings:$runSettingsFile"

    Write-Information "Finding and deploying references"
    FindAndDeployReferences $TestAssemblies

    Write-Information "Starting runner: $($startInfo.FileName) $($startInfo.Arguments)"

    $global:LASTEXITCODE = $exec.Invoke($startInfo)
} finally {
    $lastExitCode = $global:LASTEXITCODE

    if ($lastExitCode -eq 0) {
        try {
            [System.IO.File]::Delete($runSettingsFile)
        } catch {
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
