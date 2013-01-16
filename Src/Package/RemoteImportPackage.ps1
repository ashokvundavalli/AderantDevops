<# 
.Synopsis 
    Imports a package to an environment using PackageManaerConsole.exe.
.Example     
    RemoteImportPackage "vmaklexpdevb03.ap.aderant.com" ".\SkadLove.CustomizationTest.environment.xml" "\\VMAKLEXPDEVB04\ExpertPackages\FileOpening.zip"
.Parameter $remoteMachineName is the fully qualified domain name of the remote machine
.Parameter $environmentManifestpath is the path to the environment manifest file
#>

param ( [string]$remoteMachineName, 
        [string]$environmentManifestPath, 
        [string]$packagePath)

begin{
	$modulePath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) RemoteDeploymentHelper.psm1
    Import-Module $modulePath

}

process{
	$ErrorActionPreference = "Stop"
	$session = Get-RemoteSession $remoteMachineName
	$sourcePath = Get-SourceDirectory $environmentManifestPath
    # Invoke PackageManagerConsole.exe on remote machine.
    Write-Host "Invoking PackageManagerConsole.exe command on remote machine $remoteMachineName."
    Invoke-Command $session `
        -ScriptBlock { 
            param($innerSourcePath, $innerPackagePath)
            cd "$innerSourcePath"
            .\PackageManagerConsole.exe /Import /File:"$innerPackagePath"
        } `
        -ArgumentList $sourcePath, $packagePath

	Write-Host "Last Exit Code : " + $LASTEXITCODE

}