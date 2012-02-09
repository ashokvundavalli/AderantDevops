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
    $sourcePath = $remoteMachineName
	#$sourcePath = Get-SourceDirectory $environmentManifestPath
    
    #Only attempt a remove if the DeploymentEngine.exe file is present.
    $binariesPresentOnRemote = Invoke-Command $session -ScriptBlock { param($innerSourcePath) test-path "$innerSourcePath\ExpertSource\Deployment\DeploymentEngine.exe"} -ArgumentList $sourcePath
    if ($binariesPresentOnRemote ) {
        Write-Host "Invoking DeploymentEngine to STOP on [$remoteMachineName]."
    	Invoke-Command $session -ScriptBlock { param($innerSourcePath, $innerManifestPath) cd "$innerSourcePath\ExpertSource\Deployment"; .\DeploymentEngine.exe stop "$innerManifestPath" } -ArgumentList $sourcePath, $environmentManifestPath

        Write-Host "Waiting for services to stop on [$remoteMachineName]."
    	Start-Sleep -s 120

        # Need to get server info from environment so we kill processes on the right machine.
        #TryKillProcess "Expert.Workflow.Service" $remoteMachineName
        #TryKillProcess "ExpertMatterPlanning" $remoteMachineName
        #TryKillProcess "ConfigurationManager" $remoteMachineName
        #Start-Sleep -s 30

        Write-Host "Invoking DeploymentEngine to REMOVE on [$remoteMachineName]."
    	Invoke-Command $session -ScriptBlock { param($innerSourcePath, $innerManifestPath) cd "$innerSourcePath\ExpertSource\Deployment"; .\DeploymentEngine.exe remove "$innerManifestPath" } -ArgumentList $sourcePath, $environmentManifestPath

    } else {
        Write-Host "Could not find $sourcePath\ExpertSource\DeploymentManager\DeploymentEngine.exe so not performing remove."
    }
}