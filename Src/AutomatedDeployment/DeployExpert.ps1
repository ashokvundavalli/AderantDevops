<#
.Synopsis
    Deploys Expertsuite
.Description
	Restores database and runs the deployment engine to deploy Expert Suite
.PARAMETER serverName
	The name of the database server.
.PARAMETER databaseName
	The name of the database containing the environment manifest.
.PARAMETER skipPackageImports
	Flag to skip package imports.
.PARAMETER skipHelpDeployment
	Flag to skip deployment of Help.
.PARAMETER environmentXml
	Fully qualified name of the environment xml
.PARAMETER InstallRoot
	Fully qualified path of the Install folder root
.PARAMETER BinariesRoot
	Fully qualified path of the binaries folder 
.PARAMETER computerName
	Name of the computer expert is being deployed to
.PARAMETER databaseServerName
	Name of the database server
.PARAMETER databaseName
	Name of the database to be used for deployment
.PARAMETER sourcePath
	Fully qualified path to the ExpertSource folder.


#>
param (
	[Parameter(Mandatory=$false)][string]$environmentXml = "C:\AderantExpert\Environment.xml",
	[Parameter(Mandatory=$false)][string]$InstallRoot = "C:\AderantExpert\Install",
	[Parameter(Mandatory=$false)][string]$BinariesRoot = "C:\AderantExpert\Binaries\",
	[Parameter(Mandatory=$false)][switch]$skipPackageImports,
	[Parameter(Mandatory=$false)][switch]$skipHelpDeployment,
	[Parameter(Mandatory=$false)][string]$computerName = $env:computername,
	[Parameter(Mandatory=$false)][string]$databaseServerName = $env:computername,
	[Parameter(Mandatory=$false)][string]$databaseName = "Expert"
)

process {

    $PSScriptRoot = Join-Path -Path $BinariesRoot -ChildPath "AutomatedDeploy"
	$deploymentEngine = Join-Path -Path $InstallRoot -ChildPath "DeploymentEngine.exe"
	$deploymentLogFile = Join-Path -Path $InstallRoot -ChildPath "\logs\DeploymentManager\Deployment.log"

    Write-Verbose "Restoring Database" -verbose

    . "$PSScriptRoot\Aderant.API.Database\ProvisionDatabase.ps1" -databaseName $databaseName -backupPath "C:\AderantExpert\Binaries\Database\Expert.bak"

    Write-Verbose "Enabling Filestream" -verbose 

    #. "$PSScriptRoot\DeploymentEngine.ps1" -command "EnableFilestream" -serverName $databaseServerName -databaseName $databaseName -environmentXml $environmentXml

	if ($lastexitcode -ne 0) { 
        Write-Host "Exited with code $lastexitcode" 
		exit $lastexitcode
	} 

    Write-Verbose "Importing Manifest" -verbose 

    . "$PSScriptRoot\DeploymentEngine.ps1" -command "ImportEnvironmentManifest" -serverName $databaseServerName -databaseName $databaseName -environmentXml $environmentXml

	if ($lastexitcode -ne 0) { 
        Write-Host "Exited with code $lastexitcode" 
		exit $lastexitcode
	} 

    Write-Verbose "Deploying" -verbose 

    . "$PSScriptRoot\DeploymentEngine.ps1" -command "DeploySilent" -serverName $databaseServerName -databaseName $databaseName -environmentXml $environmentXml
    if ($lastexitcode -ne 0) { 
        Write-Host "Exited with code $lastexitcode" 
		exit $lastexitcode
	} 

    $deploymentLog = Get-Content $deploymentLogFile -Raw

    Write-Verbose $deploymentLog -verbose

	exit $lastexitcode
}