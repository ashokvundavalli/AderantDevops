<# 
.Synopsis 
    Deploys to a remote machine by invoking the deployment command on that machine.
.Example     
    RemoteDeploymentDeploy "vmaklexpdevb03.ap.aderant.com" ".\SkadLove.CustomizationTest.environment.xml" "\\vmaklexpdevb03\ExpertBinaries"
.Parameter $remoteMachineName is the fully qualified domain name of the remote machine
.Parameter $environmentManifestpath is the path to the environment manifest file
#>

param ([string]$remoteMachineName, 
       [string]$environmentManifestPath,
       [bool]$DeployExpert=$true)

begin {
    $modulePath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) RemoteDeploymentHelper.psm1
    Import-Module $modulePath -DisableNameChecking 
}

process {
    $ErrorActionPreference = "Stop"

    $session = Get-RemoteSession $remoteMachineName
    $sourcePath = Get-SourceDirectory $environmentManifestPath
   
    # Invoke DeploymentEngine.exe on remote machine.
    if ($DeployExpert -and $DeployExpert -eq $true) {
        Write-Host "Invoking DeploymentEngine.exe command on remote machine $remoteMachineName."
        Invoke-Command $session `
            -ScriptBlock { 
                param($innerSourcePath, $innerManifestPath)
                cd "$innerSourcePath"
                .\DeploymentEngine.exe deploy "$innerManifestPath"
            } `
            -ArgumentList $sourcePath, $environmentManifestPath
    }
    Write-Host "Last Exit Code for Deployment Engine - Deploy: " + $LASTEXITCODE
}