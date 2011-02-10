<# 
.Synopsis 
    Deploys to a remote machine by invoking the deployment command on that machine.
.Example     
    RemoteDeploymentDeploy "vmaklexpdevb03.ap.aderant.com" ".\SkadLove.CustomizationTest.environment.xml" "C:\ExpertBinaries" "\\vmaklexpdevb03\ExpertBinaries"
.Parameter $remoteMachineName is the fully qualified domain name of the remote machine
.Parameter $environmentManifestpath is the path to the environment manifest file
.Parameter $localBinaries is the path of the binaries folder
.Parameter $remoteBinaries is the network location of the remote machine's binaries folder
#>

param ([string]$remoteMachineName, [string]$environmentManifestPath, [string]$localBinaries, [string]$remoteBinaries)

begin{
	$modulePath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) RemoteDeploymentHelper.psm1
    Import-Module $modulePath

	Function CopyBinariesToRemoteMachine($localBinaries, $remoteBinaries) {
		Remove-Item -Recurse $remoteBinaries\*
		Copy-Item -Path "$localBinaries\*" -Destination "$remoteBinaries" -Recurse -Force
	}
}

process{
	$ErrorActionPreference = "Stop"
	$session = Get-RemoteSession $remoteMachineName
	$sourcePath = Get-SourceDirectory $environmentManifestPath

	CopyBinariesToRemoteMachine $localBinaries $remoteBinaries

	Invoke-Command $session -ScriptBlock { param($innerSourcePath, $innerManifestPath) cd "$innerSourcePath"; .\DeploymentEngine.exe deploy "$innerManifestPath" } -ArgumentList $sourcePath, $environmentManifestPath
}