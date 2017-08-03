<#
.Synopsis
    Runs through the automated deployment scripts
.Description
	Sets up and deploys to an environment
.PARAMETER serverName
	The name of the database server.
.PARAMETER databaseName
	The name of the database containing the environment manifest.
.PARAMETER skipPackageImports
	Flag to skip package imports.
.PARAMETER skipHelpDeployment
	Flag to skip deployment of Help.
#>
param (
	[Parameter(Mandatory=$true)][string]$servicePassword,
	[Parameter(Mandatory=$false)][switch]$isLoadBalanced,
	[Parameter(Mandatory=$false)][switch]$skipPackageImports,
	[Parameter(Mandatory=$false)][switch]$skipHelpDeployment,
	[Parameter(Mandatory=$false)][string]$environmentXml = "C:\AderantExpert\Environment.xml",
	[Parameter(Mandatory=$false)][string]$InstallRoot = "C:\AderantExpert\Install",
	[Parameter(Mandatory=$false)][string]$BinariesRoot = "C:\AderantExpert\Binaries\",
	[Parameter(Mandatory=$false)][string]$computerName = $env:computername,
	[Parameter(Mandatory=$false)][string]$databaseServerName = $env:computername,
	[Parameter(Mandatory=$false)][string]$databaseName = "Expert",
	[Parameter(Mandatory=$false)][string]$sourcePath = "C:\AderantExpert\Binaries\ExpertSource",
	[Parameter(Mandatory=$false)][string]$environmentGenerator = "C:\AderantExpert\Binaries\AutomatedDeploy\EnvironmentGenerator\EnvironmentGenerator.dll",
    [Parameter(Mandatory=$false)][string]$windowsSourcePath = "C:\Windows\WinSxS",
    [Parameter(Mandatory=$false)][string]$serviceUser = "ADERANT_AP\service.expert.qa"
)
Set-StrictMode -Version 2.0
process {

    $PSScriptRoot = Join-Path -Path $BinariesRoot -ChildPath "AutomatedDeploy"
	$deploymentEngine = Join-Path -Path $InstallRoot -ChildPath "DeploymentEngine.exe"

	Write-Verbose "Generating Environment" -verbose 

    . "$PSScriptRoot\EnvironmentGeneration.ps1" -databaseServerName "$databaseServerName" -databaseName "$databaseName"
	if ($lastexitcode -ne 0) { 
        Write-Host "Exited with code $lastexitcode" 
		exit $lastexitcode
	} 

    Write-Verbose "Installing Deployment Manager" -verbose 

    . "$PSScriptRoot\UninstallDeploymentManager.ps1" -deploymentManagerMsiDirectory $BinariesRoot
	if ($lastexitcode -ne 0) {
        Write-Host "Exited with code $lastexitcode" 
		exit $lastexitcode
	} 
    . "$PSScriptRoot\InstallDeploymentManager.ps1" -deploymentManagerMsiDirectory $BinariesRoot
	if ($lastexitcode -ne 0) { 
        Write-Host "Exited with code $lastexitcode" 
		exit $lastexitcode
	} 

    Write-Verbose "Installing Pre-requisites" -verbose

    . "$PSScriptRoot\InstallPrereqs.ps1" -prereqRoot "$InstallRoot\ApplicationServerPrerequisites\" -windowsSourcePath $windowsSourcePath -appFabricServiceUser $appFabricServiceUser -expertServiceUser $expertServiceUser -appFabricServicePassword $servicePassword -isLoadBalanced $isLoadBalanced
	if ($lastexitcode -ne 0) { 
        Write-Host "Exited with code $lastexitcode" 
		exit $lastexitcode
	} 


	exit $lastexitcode
}