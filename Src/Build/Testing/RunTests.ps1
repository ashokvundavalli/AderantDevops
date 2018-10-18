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

  [Parameter(Mandatory=$false)]
  [string[]]
  $ReferencesToFind,
  
  [Parameter(Mandatory=$false, ValueFromRemainingArguments=$true)]
  [string[]]
  $TestAssemblies
)

Set-StrictMode -Version "Latest"

function CreateRunSettingsXml() {
    [xml]$xml = Get-Content -Path "$PSScriptRoot\default.runsettings"
    $assemblyResolution = $xml.RunSettings.MSTest.AssemblyResolution

    if (-not $script:ReferencePaths -and $SolutionRoot) {
        #TODO: Drop dependencies
        $script:ReferencePaths = @([System.IO.Path]::Combine($SolutionRoot, "packages"), [System.IO.Path]::Combine($SolutionRoot, "dependencies"))
    }    

    if ($ReferencePaths) {
        foreach ($path in $script:ReferencePaths) {
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

function FindAndDeployReferences([string[]] $testAssemblies) {
    if ($ReferencesToFind -and $script:ReferencePaths) {
        [void][System.Reflection.Assembly]::Load("System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")

        Write-Information "Finding references... $ReferencesToFind"

        # Create the paths to drop references into
        $destinationPaths = [System.Collections.Generic.HashSet[System.String]]::new()
        foreach ($file in $testAssemblies) {
            $file = [System.IO.FileInfo]$file
            [void]$destinationPaths.Add($file.Directory.FullName)
        }

        $files = @()
        foreach ($path in $script:ReferencePaths) {
            $files += Get-ChildItem -LiteralPath $path -Filter "*.dll" -Recurse
        }     

        # Find the reference in our search space  then drop it into our directory which contains the test assembly
        foreach ($reference in $ReferencesToFind) {
            # To be correct we should also support .exe and .winmd...
            $dllName = [System.IO.Path]::ChangeExtension($reference, "dll")             
           
            $foundReferenceFile = $files | Where-Object -Property Name -EQ $dllName | Select-Object -First 1

            if ($foundReferenceFile) {            
                foreach ($path in $destinationPaths) {
                    $destinationFile = [System.IO.Path]::Combine($path, $dllName)

                    if (-not [System.IO.File]::Exists($destinationFile)) {
                        Write-Information "Found required file $foundReferenceFile"                        
                        New-Item -ItemType HardLink -Name $destinationFile -Target $foundReferenceFile
                    }
                }
            }            
        }        
    }
}

function GetTestResultFiles() {
    return Get-ChildItem -LiteralPath "$WorkingDirectory\TestResults" -Filter "*.trx" -ErrorAction SilentlyContinue
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

$beforeRunTrxFiles = GetTestResultFiles

Set-StrictMode -Version Latest

$global:LASTEXITCODE = -1

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $PathToTestTool
$startInfo.Arguments = "$ToolArgs $TestAssemblies"
$startInfo.WorkingDirectory = $WorkingDirectory

try {
    $xml = CreateRunSettingsXml
    Write-Information ([System.Environment]::NewLine + "$xml")

    $runSettingsFile = [System.IO.Path]::GetTempFileName()
    Add-Content -LiteralPath $runSettingsFile -Value $xml -Encoding UTF8

    FindAndDeployReferences $TestAssemblies

    #/Diag:C:\temp\test.log"

    $startInfo.Arguments += " /Settings:$runSettingsFile" 
    Write-Information "$($startInfo.FileName) $($startInfo.Arguments)"

    # Implemented in C# for performance
    $runner = [Aderant.Build.Utilities.ProcessRunner]::InvokeTestRunner($startInfo)
    $runner.RecordConsoleOutput = $true
    $runner.Start()
    $global:LASTEXITCODE = $runner.Wait($true, [System.Timespan]::FromMinutes(20).TotalMilliseconds)
} finally {
    if ($Error) {
        Write-Error $Error[0]
    }

    [System.IO.File]::Delete($runSettingsFile)

    if ($global:LASTEXITCODE -ne 0) {
        if ($IsDesktopBuild) {
            ShowTestRunReport
        }        

        Write-Error "Test execution exit code: $($global:LASTEXITCODE)"
    } 
}
