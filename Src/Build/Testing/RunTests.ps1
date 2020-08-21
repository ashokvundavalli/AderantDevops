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
  [string]$SolutionRoot,

  [Parameter(Mandatory=$false)]
  [string]$RunSettingsFile,

  [Parameter(Mandatory=$false, HelpMessage="The paths to provide to the test tool assembly resolver")]
  [string[]]$ReferencePaths,

  [Parameter(Mandatory=$false)]
  [Hashtable]$AdditionalEnvironmentVariables,

  [Parameter(Mandatory=$false)]
  [bool]$RunInParallel,

  [Parameter(Mandatory=$false, ValueFromRemainingArguments=$true)]
  [string[]]$TestAssemblies,

  [Parameter(Mandatory=$false)]
  [string]$TestAdapterPath,

  [Parameter(Mandatory=$false)]
  [int]$TestSessionTimeout = 1200000
)

Set-StrictMode -Version "Latest"
$InformationPreference = "Continue"
$ErrorActionPreference = "Stop"

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

function EnsureRunSettingsHasRequiredNodes() {
    # Get the minimum settings required to not fail
    $defaultRunSettings = [System.Xml.XmlDocument](Get-Content -Path "$PSScriptRoot\default.runsettings")

    # Load up the custom run settings file
    $providedRunSettings = [System.Xml.XmlDocument](Get-Content -Path $RunSettingsFile)

    $xslt = [System.Xml.XmlDocument](Get-Content -Path "$PSScriptRoot\..\merge-xml.xslt")

    # Merge the two documents together, taking elements from the custom
    # file over the default ones.
    $transform = [System.Xml.Xsl.XslCompiledTransform]::new()
    $transform.Load($xslt.CreateNavigator())

    $stringBuilderForXmlWriter = [System.Text.StringBuilder]::new()
    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $settings.CloseOutput = $true
    $writer = [System.Xml.XmlWriter]::Create($stringBuilderForXmlWriter, $settings)

    $argList = [System.Xml.Xsl.XsltArgumentList]::new()
    $argList.AddParam("with", "", $providedRunSettings)
    $argList.AddParam("replace", "", $true)

    $transform.Transform($defaultRunSettings.CreateNavigator(), $argList, $writer)

    return [System.Xml.XmlDocument]($stringBuilderForXmlWriter.ToString())
}

function CreateRunSettingsXml() {
    $script:settingsDocument = EnsureRunSettingsHasRequiredNodes
    $assemblyResolution = $settingsDocument.RunSettings.MSTest.AssemblyResolution

    if ($script:ReferencePaths) {
        foreach ($path in $script:ReferencePaths) {
            AddSearchDirectory $assemblyResolution $path -includeSubDirectories:$false -prepend:$false
        }
    }

    if ($SolutionRoot) {
        # We want the test runner resolver to bind content produced by the solution build
        # for the most reliable test run as there could be matching by older assemblies in the other directories
        AddSearchDirectory $assemblyResolution ([System.IO.Path]::Combine($SolutionRoot, "Bin", "Module")) -includeSubDirectories:$true -prepend:$true
    }

    # VS SDK
    $vsSdk = $Env:VSSDK140Install
    if (![string]::IsNullOrWhiteSpace($vsSdk)) {
        AddSearchDirectory $assemblyResolution ([System.IO.Path]::Combine($vsSdk, "VisualStudioIntegration\Common\Assemblies\v4.0")) -includeSubDirectories:$false -prepend:$false
    }

    if ($script:RunInParallel -eq $false) {
        $settingsDocument.RunSettings.RunConfiguration.MaxCpuCount = '1'
    }

    $timeout = $settingsDocument.RunSettings.RunConfiguration.TestSessionTimeout
    if ([string]::IsNullOrWhiteSpace($timeout) -or $timeout -eq "0") {
        $settingsDocument.RunSettings.RunConfiguration.TestSessionTimeout = $TestSessionTimeout.ToString()
    }

    $sw = [System.IO.StringWriter]::new()
    $writer = New-Object System.Xml.XmlTextWriter($sw)
    $writer.Formatting = [System.Xml.Formatting]::Indented
    $settingsDocument.WriteContentTo($writer)

    $writer.Dispose()
    $sw.Dispose()

    return $sw.ToString()
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

$exitcode = 0

try {
	Write-Information "Creating run settings file..."
	$xml = CreateRunSettingsXml
	Set-Content  -LiteralPath $runSettingsFile -Value $xml -Encoding UTF8

    # Log once per processor as the document is usually the same so we don't
    # need to see it all the time
    if ([Appdomain]::CurrentDomain.GetData("HAS_LOGGED_RUN_SETTINGS") -eq $null) {
        Write-Information $xml
        [Appdomain]::CurrentDomain.SetData("HAS_LOGGED_RUN_SETTINGS", "")
    }

	$startInfo.Arguments += " /Settings:$RunSettingsFile"

    if (-not [string]::IsNullOrWhiteSpace($TestAdapterPath)) {
        $startInfo.Arguments += " /TestAdapterPath:$TestAdapterPath"
    }

    Write-Information "Starting runner: $($startInfo.FileName) $($startInfo.Arguments)"

    $exitcode = $exec.Invoke($startInfo)
} finally {
    if ($exitcode -eq 0) {
        try {
            if ([System.IO.Directory]::Exists($TestAdapterPath)) {
                Remove-Item -Path $TestAdapterPath -Force
            }
        } catch {
            Write-Debug "Failed to delete temporary directory to test adapter $TestAdapterPath"
        }
    }

    if ($exitcode -ne 0) {
        if ($IsDesktopBuild) {
            ShowTestRunReport
        }

        Write-Error "Test runner exit code: $exitcode"
    } else {
        if ($Error) {
            throw $Error[0]
        }
    }
}
