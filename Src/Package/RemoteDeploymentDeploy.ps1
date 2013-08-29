<# 
.Synopsis 
    Deploys to a remote machine by invoking the deployment command on that machine.
.Example     
    RemoteDeploymentDeploy "vmaklexpdevb03.ap.aderant.com" ".\SkadLove.CustomizationTest.environment.xml" "C:\ExpertBinaries" "\\vmaklexpdevb03\ExpertBinaries"
.Parameter $remoteMachineName is the fully qualified domain name of the remote machine
.Parameter $environmentManifestpath is the path to the environment manifest file
.Parameter $localBinariesOnBuildMachine is the path of the binaries folder on the build machine
.Parameter $remoteBinaries is the network location of the remote machine's binaries folder
.Parameter $useBuildAllOutput is true if you want to use build all output, otherwise GetProduct.ps1 is used.
.Parameter $DropSp_CmsCheckIndex is true if you want to drop SP_CMSCHECKINDEX. 
.Parameter $DeployDbProject is true if you want to deploy the Database project before deploying expert.
#>

param ( [string]$remoteMachineName, 
        [string]$environmentManifestPath, 
        [string]$localBinariesOnBuildMachine, 
        [string]$remoteBinaries, 
        [string]$useBuildAllOutput,
        [string]$DropSp_CmsCheckIndex,
        [string]$DeployDbProject,
        [string]$DeployExpert='true')

begin{
	$modulePath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) RemoteDeploymentHelper.psm1
    Import-Module $modulePath

}

process{
	# There is an issue with Environment Validator that throws an error which will stop the deployment.
	# $ErrorActionPreference = "Stop"
	$session = Get-RemoteSession $remoteMachineName
	$sourcePath = Get-SourceDirectory $environmentManifestPath

    # Only copy binaries if we are NOT using build all output.
	if (-not $useBuildAllOutput.ToUpper() -eq 'TRUE') {
        CopyBinariesToRemoteMachine $localBinariesOnBuildMachine $remoteBinaries
    }
    
    $dbProjectTargetServer = Get-DbProjectTargetDatabaseServer $environmentManifestPath
    $dbProjectTargetDatabaseName = Get-DbProjectTargetDatabaseName $environmentManifestPath
        
    #Drop [dbo].[SP_CMSCHECKINDEX] so deploy database works.
    if ($DropSp_CmsCheckIndex -and $DropSp_CmsCheckIndex.ToUpper() -eq 'TRUE') {
        Write-Host "Dropping [dbo].[SP_CMSCHECKINDEX] so deploy database works."
        $dropSP_CMSCHECKINDEX = "IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SP_CMSCHECKINDEX]') AND type in (N'P', N'PC')) DROP PROCEDURE [dbo].[SP_CMSCHECKINDEX]"
		Execute-SQL $environmentManifestPath,$dropSP_CMSCHECKINDEX
    }
    
    #Run deploy database command on remote machine.
    if ($DeployDbProject -and $DeployDbProject.ToUpper() -eq 'TRUE') {
        Write-Host "Invoking deploy database command on remote machine $remoteMachineName."
        Invoke-Command $session `
            -ScriptBlock { 
                param($dbProjectTargetServer, $dbProjectTargetDatabase, $sourcePath)
                $vsdbcmd = ".\vsdbcmd.exe"
                $params = "/a:deploy /cs:`"Integrated Security=SSPI;Initial Catalog=$dbProjectTargetDatabase;Data Source=$dbProjectTargetServer`" /ManifestFile:C:\Expertsource\Expertsource\Expert.deploymanifest /dsp:sql /p:TargetDatabase=$dbProjectTargetDatabase /p:BlockIncrementalDeploymentIfDataLoss=False /p:DeployDatabaseProperties=False /p:AlwaysCreateNewDatabase=false"
                Write-host "Command: $vsdbcmd $params"
                pushd "C:\Program Files (x86)\Microsoft Visual Studio 10.0\VSTSDB\Deploy\"
                cmd /C "$vsdbcmd $params"
                popd
            } `
            -ArgumentList $dbProjectTargetServer, $dbProjectTargetDatabaseName, $sourcePath
    }
    
    # Invoke DeploymentEngine.exe on remote machine.
    if ($DeployExpert -and $DeployExpert.ToUpper() -eq 'TRUE') {
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
	Write-Host "Last Exit Code for Deployment Engine - Deploy: " + $LASTEXITCODE
	Write-Host "Last Exit Code for Deployment Engine - Deploy: " + $LASTEXITCODE
	Write-Host "Last Exit Code for Deployment Engine - Deploy: " + $LASTEXITCODE
	
}