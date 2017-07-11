<#
.Synopsis
    Generates an environment manifest.
.Description
	Uses the specified ExpertSource to generate an Environment Manifest.
.PARAMETER sourcePath
	Location of the Expert source to use for generation
.PARAMTER installBinPath
	Location of the bin folder for the deployment engine
.PARAMTER autoDeployRoot
	Location of the environment generator dlls
.PARAMETER computerName
	Name of the computer to be used for the deployment
.PARAMETER databaseServerName
	Name of the database server	
.PARAMETER databaseName
	Name of the database to be used for deployment
.EXAMPLE
	Generate-Manifest 
		Uninstalls Deployment Manager using the DeploymentManager.msi located in C:\Temp
#>
[CmdletBinding()]
param (
	[Parameter(Mandatory=$false)][string]$computerName = $env:computername,
	[Parameter(Mandatory=$false)][string]$databaseServerName = $env:computername,
	[Parameter(Mandatory=$false)][string]$databaseName,
	[Parameter(Mandatory=$false)][string]$sourcePath = "C:\AderantExpert\Binaries\ExpertSource",
	[Parameter(Mandatory=$false)][string]$installBinPath = "C:\AderantExpert\Install\bin\",
	[Parameter(Mandatory=$false)][string]$autoDeployRoot = "C:\AderantExpert\Binaries\AutomatedDeploy\EnvironmentGenerator\" 
)

process {
	if (-not (Test-Path $environmentGenerator)){
		Write-Error "Unable to find EnvironmentGenerator.dll at: $(environmentGenerator)"
		Exit 1
	}
	
	$environmentGeneratorDll = Join-Path -Path $autoDeployRoot -ChildPath "EnvironmentGenerator.dll"
	$deploymentCheckExe = Join-Path -Path $autoDeployRoot -ChildPath "DeployCheck.exe"
	$deploymentFrameworkDll = Join-Path -Path $sourcePath -ChildPath "Aderant.Framework.Deployment.dll"
	[Reflection.Assembly]::LoadFrom($deploymentFrameworkDll)
	[Reflection.Assembly]::LoadFrom($deploymentCheckExe)
	[Reflection.Assembly]::LoadFrom($environmentGeneratorDll)
	if ($databaseName){
		$generator = new-object EnvironmentGenerator.EnvironmentManifestGenerator($computerName, $sourcePath, $databaseServerName, $databaseName)
	} else {
		$generator = new-object EnvironmentGenerator.EnvironmentManifestGenerator($computerName, $sourcePath, $databaseServerName)
	}
	

    $generator.GenerateManifest()
	
}