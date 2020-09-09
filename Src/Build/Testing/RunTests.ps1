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
  [string]$SolutionRoot,

  [Parameter(Mandatory=$false)]
  [string]$RunSettingsFile,

  # Even if we don't have a custom file - the build still provides us with a copy of the default
  # This parameter means 'should we do a settings merge?'
  [Parameter(Mandatory=$false)]
  [bool]$UsingCustomRunSettingsFile,

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
  [int]$TestSessionTimeout = 1200000,

  [Parameter(Mandatory=$false)]
  [string]$TestResultFileDrop
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
    # Load up the custom run settings file
    $providedRunSettings = [System.Xml.XmlDocument](Get-Content -Path $RunSettingsFile)

    if ($UsingCustomRunSettingsFile) {
        # Get the minimum settings required to not fail
        $defaultSettingsFileFullPath = [System.IO.Path]::GetFullPath("$PSScriptRoot\default.runsettings")
        $defaultRunSettings = [System.Xml.XmlDocument](Get-Content -Path $defaultSettingsFileFullPath)

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
    } else {
        Write-Information "Run settings merge skipped"
    }

    return $providedRunSettings
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

function GetTestResultFiles {
    Write-Information -MessageData 'Collecting test result files.'
    [string]$path = "$WorkingDirectory\TestResults"
    [void][System.IO.Directory]::CreateDirectory($path)

    $trxFiles = Get-ChildItem -LiteralPath $path -Filter '*.trx' -File -ErrorAction 'SilentlyContinue'
    
    return $trxFiles
}

function CopyTestResultFiles {
    Write-Information -MessageData 'Copying test result files from test run.'

    if ([string]::IsNullOrWhiteSpace($TestResultFileDrop)) {
        Write-Information -MessageData 'Test result file drop location was not specified.'
        if ([string]::IsNullOrWhiteSpace($SolutionRoot)) {
            Write-Information -MessageData 'Test result file drop location and solution root were not specified so the files will not be copied.'
            return
        } else {
            [string]$BuildSolutionRoot = Split-Path -Path $SolutionRoot -Parent
            $TestResultFileDrop = Join-Path -Path $BuildSolutionRoot -ChildPath 'TestResults'
        }
    }

    Write-Information -MessageData "Test result file drop location is: '$TestResultFileDrop'."
    
    [System.IO.FileInfo[]]$trx = GetTestResultFiles

    Write-Information -MessageData "Total number of trx files are: '$($trx.Length)'."

    if ($null -ne $trx -and $trx.Length -gt 0) {
        [void][System.IO.Directory]::CreateDirectory($TestResultFileDrop)

        foreach ($result in $trx) {
            # Hardlinks cannot span drives.
            [string]$targetFile = Join-Path -Path $TestResultFileDrop -ChildPath $result.Name

            if ([string]::Equals([System.IO.Path]::GetFullPath($result.FullName), [System.IO.Path]::GetFullPath($targetFile), [System.StringComparison]::OrdinalIgnoreCase)) {
                Write-Information -MessageData "Not copying the test result file from: '$($result.FullName)' to: '$targetFile' as they are at the same location."    
                continue
            }

            Write-Information -MessageData "Copying test result file from: '$($result.FullName)' to: '$targetFile'."
            New-Item -Path $targetFile -ItemType 'HardLink' -Value $result.FullName -Force
        }
    } else {
        Write-Warning "Unable to locate test result files in directory: '$WorkingDirectory'."
    }
}

function ShowTestRunReport() {
    $afterRunTrxFiles = GetTestResultFiles

    if ($null -eq $afterRunTrxFiles) {
        Write-Information "Skipped generating HTML report - no .trx files present in directory: '$WorkingDirectory\TestResults'."
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
	Set-Content -LiteralPath $runSettingsFile -Value $xml -Encoding UTF8

    $hash = (Get-FileHash -LiteralPath $runSettingsFile -Algorithm SHA1).Hash

    # Log once per unique settings file as the document is usually the same so we don't
    # need to see it all the time
    if ($null -eq [Appdomain]::CurrentDomain.GetData($hash)) {
        Write-Information $xml
        [Appdomain]::CurrentDomain.SetData($hash, "")
    }

	$startInfo.Arguments += " /Settings:$RunSettingsFile"

    if (-not [string]::IsNullOrWhiteSpace($TestAdapterPath)) {
        $startInfo.Arguments += " /TestAdapterPath:$TestAdapterPath"
    }

    Write-Information "Starting runner: $($startInfo.FileName) $($startInfo.Arguments)"

    $exitcode = $exec.Invoke($startInfo)

    Write-Information -MessageData "Exit code from test run was: '$exitcode'."
} finally {  
    # Copy test result files
    CopyTestResultFiles

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
