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
    
    #Only attempt a remove if the DeploymentEngine.exe file is present.
    $binariesPresentOnRemote = Invoke-Command $session -ScriptBlock { param($innerSourcePath) test-path "$innerSourcePath\DeploymentEngine.exe"} -ArgumentList $sourcePath
    if ($binariesPresentOnRemote ) {
        Write-Host "Invoking DeploymentEngine to STOP on [$remoteMachineName]."
    	Invoke-Command $session -ScriptBlock { param($innerSourcePath, $innerManifestPath) cd "$innerSourcePath\"; .\DeploymentEngine.exe stop   "$innerManifestPath" } -ArgumentList $sourcePath, $environmentManifestPath

		Write-Host "Invoking to remove Share permissions from the ExpertShare folder on [$remoteMachineName]."
		Invoke-Command $session -ScriptBlock { 
			$share = GET-WMIOBJECT Win32_Share -Filter "Name='ExpertShare'"
			Write-Verbose "Attempt to remove specified share"
			$Return = $share.Delete()
			if ($Return.ReturnValue -ne 0) {
			Write-Host "Unable to remove specified share due of the error: $(Get-ReturnCode $Return)."
			}
			else {
				Write-Verbose "Removed Share permissions on ExpertShare folder."
			}
		}
		
		Start-Sleep -s 10
		
		Write-Host "Invoking to create Share permissions for the ExpertShare folder on [$remoteMachineName]."
		Invoke-Command $session -ScriptBlock { 
			param($remoteMachineName) 					 
			$share = GET-WMIOBJECT Win32_Share -Filter "Name='ExpertShare'"
			Write-Verbose "Attempt to create specified share"
			net share "ExpertShare=C:\ExpertShare" "/GRANT:Everyone,FULL"
		} -ArgumentList $remoteMachineName
		
		Start-Sleep -s 10

        Write-Host "Waiting for services to stop on [$remoteMachineName]."
    	Start-Sleep -s 30
        #Need to get server info from environment so we kill processes on the right machine.
        TryKillProcess "Expert.Messaging.Service" $remoteMachineName
        TryKillProcess "Expert.Core.Services" $remoteMachineName
        TryKillProcess "Expert.Workflow.Service" $remoteMachineName
        Start-Sleep -s 30

        Write-Host "Invoking DeploymentEngine to REMOVE on [$remoteMachineName]."
    	Invoke-Command $session -ScriptBlock { param($innerSourcePath, $innerManifestPath) cd "$innerSourcePath"; .\DeploymentEngine.exe remove "$innerManifestPath" } -ArgumentList $sourcePath, $environmentManifestPath

    } else {
        Write-Host "Could not find $sourcePath\DeploymentEngine.exe so not performing remove."
    }
}