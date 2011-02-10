<# 
.Synopsis 
    Removes deployment from a remote machine by invoking the deployment remove command on that machine.
.Example     
    RemoteDeploymentRemove "vmaklexpdevb03.ap.aderant.com" ".\SkadLove.CustomizationTest.environment.xml"
.Parameter $remoteMachineName is the fully qualified domain name of the remote machine
.Parameter $environmentManifestpath is the path to the environment manifest file
#>

param ([string]$remoteMachineName, [string]$environmentManifestPath)

begin{
	$modulePath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) RemoteDeploymentHelper.psm1
    Import-Module $modulePath
}

process{
	$ErrorActionPreference = "Stop"
	$session = Get-RemoteSession $remoteMachineName
	$sourcePath = Get-SourceDirectory $environmentManifestPath

	Invoke-Command $session -ScriptBlock { param($innerSourcePath, $innerManifestPath) cd "$innerSourcePath"; .\DeploymentEngine.exe stop "$innerManifestPath" } -ArgumentList $sourcePath, $environmentManifestPath

	Start-Sleep -s 60
    TryKillProcess "Expert.Workflow.Service" $remoteMachineName
    TryKillProcess "ExpertMatterPlanning" $remoteMachineName
    TryKillProcess "ConfigurationManager" $remoteMachineName
    Start-Sleep -s 30

	Invoke-Command $session -ScriptBlock { param($innerSourcePath, $innerManifestPath) cd "$innerSourcePath"; .\DeploymentEngine.exe remove "$innerManifestPath" } -ArgumentList $sourcePath, $environmentManifestPath
}