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
.PARAMETER environmentXml
	Fully qualified name of the environment xml
#>
param (
	[Parameter(Mandatory=$false)][string]$environmentXml = "C:\AderantExpert\Environment.xml",
	[Parameter(Mandatory=$false)][string]$InstallRoot = "C:\AderantExpert\Install",
	[Parameter(Mandatory=$false)][string]$BinariesRoot = "C:\AderantExpert\Binaries\",
	[Parameter(Mandatory=$false)][switch]$skipPackageImports,
	[Parameter(Mandatory=$false)][switch]$skipHelpDeployment,
	[Parameter(Mandatory=$false)][string]$databaseServerName = $env:computername,
	[Parameter(Mandatory=$false)][string]$databaseName = "Expert"

)

process {

    $PSScriptRoot = Join-Path -Path $BinariesRoot -ChildPath "AutomatedDeploy"
	$deploymentEngine = Join-Path -Path $InstallRoot -ChildPath "DeploymentEngine.exe"
    $deploymentLogFile = Join-Path -Path $InstallRoot -ChildPath "\logs\DeploymentManager\Deployment.log"

    if (Test-Path $deploymentLogFile){
        Remove-Item $deploymentLogFile
    }

    if ($share = Get-WmiObject -Class Win32_Share -ComputerName $computerName -Filter "Name='ExpertShare'")  { 
		$share.delete() 
        Write-Verbose "Removing share to break file locks" -Verbose
	}

    . $PSScriptRoot\SetShare.ps1

    Write-Verbose "Removing Existsing Deployment" -verbose 

    . "$PSScriptRoot\DeploymentEngine.ps1" -command "Remove" -serverName $databaseServerName -databaseName $databaseName -environmentXml $environmentXml
    if ($lastexitcode -ne 0) { 
        Write-Host "Exited with code $lastexitcode" 
		exit $lastexitcode
	}

    $deploymentLog = Get-Content $deploymentLogFile -Raw

    Write-Verbose $deploymentLog -verbose

	exit $lastexitcode
}