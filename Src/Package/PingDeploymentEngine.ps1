<# 
.Synopsis 
    Ping Services from a remote machine by invoking the deployment ping command on that machine.
.Example     
    PingDeploymentEngine "vmaklexpdevb03.ap.aderant.com" "\\vmaklexpdevb03\expertsource\vmaklexpdevb03.environment.xml"
.Parameter $remoteMachineName is the fully qualified domain name of the remote machine
.Parameter $environmentManifestpath is the path to the environment manifest file
#>

param ([string]$remoteMachineName, [string]$environmentManifestPath)

begin{
	$modulePath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) RemoteDeploymentHelper.psm1
    Import-Module $modulePath
}

process{
	$session = Get-RemoteSession $remoteMachineName
	$sourcePath = Get-SourceDirectory $environmentManifestPath
    
	#Wait for services to start
	Write-Host "Waiting for services to start on [$remoteMachineName]."
	Start-Sleep -Seconds 30
	
	#Start to ping serivces on target machine
    Write-Host "Invoking DeploymentEngine to PING Services on [$remoteMachineName]."
    Invoke-Command $session -ScriptBlock { 
		param($innerSourcePath, $innerManifestPath) 
		cd "$innerSourcePath"; .\DeploymentEngine.exe ping "$innerManifestPath" 
		Write-Host "Last Exit Code for Deployment Engine - Ping: $LASTEXITCODE"
		#Check PS exit code, if something went wrong, restart the services
		if ($LASTEXITCODE -ne 0) {
			Write-Host "Services did not start, attempting to re-start services. Invoking DeploymentEngine to RESTART Services on [$remoteMachineName]."		
			cd "$innerSourcePath"; .\DeploymentEngine.exe restart "$innerManifestPath" 
			Write-Host "Last Exit Code for Deployment Engine - Restart: $LASTEXITCODE"
			if ($LASTEXITCODE -ne 0) { Write-Host "There was a problem starting the services, please check the environment. Exit Code: $LASTEXITCODE" }
			}
		} -ArgumentList $sourcePath, $environmentManifestPath
	#Restart the services if they haven't start. If restart fails then theres might be an issue
	Write-Host "Exiting remote PS session."    
	Remove-PSSession -Session $session	
}