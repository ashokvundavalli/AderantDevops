<#
.Synopsis
    Run DeploymentEngine for your current branch
.Description
	Starts DeploymentEngine.exe with the specified command.
.PARAMETER command
	The action you want the deployment engine to take.
.PARAMETER serverName
	The name of the database server.
.PARAMETER databaseName
	The name of the database containing the environment manifest.
.PARAMETER skipPackageImports
	Flag to skip package imports.
.PARAMETER skipHelpDeployment
	Flag to skip deployment of Help.
.EXAMPLE
	DeploymentEngine -action Deploy -serverName MyServer01 -databaseName MyMain
    DeploymentEngine -action Remove -serverName MyServer01 -databaseName MyMain
	DeploymentEngine -action ExportEnvironmentManifest -serverName MyServer01 -databaseName MyMain
	DeploymentEngine -action ImportEnvironmentManifest -serverName MyServer01 -databaseName MyMain
#>
[CmdletBinding()]
param (
	[Parameter(Mandatory=$true)][ValidateSet("Deploy", "DeploySilent", "Remove", "RemoveSilent", "ExportEnvironmentManifest", "ImportEnvironmentManifest", "EnableFilestream")][string]$command,
	[Parameter(Mandatory=$true)][string]$serverName,
	[Parameter(Mandatory=$true)][string]$databaseName,
	[Parameter(Mandatory=$false)][string]$environmentXml,
	[Parameter(Mandatory=$false)][string]$deploymentEngine = "C:\AderantExpert\Install\DeploymentEngine.exe",
	[Parameter(Mandatory=$false)][switch]$skipPackageImports,
	[Parameter(Mandatory=$false)][switch]$skipHelpDeployment
)

process {
	if (-not (Test-Path $deploymentEngine)) {
		Write-Error "Unable to find DeploymentEngine.exe at: $($deploymentEngine)"
		Exit 1
	}

	[string]$parameters = "$command /s:$serverName /d:$databaseName /m:$environmentXml"

	if ($skipPackageImports.IsPresent) {
		$parameters = "$parameters /skp"
	}

	if ($skipHelpDeployment.IsPresent) {
		$parameters = "$parameters /skh"
	}

	$deploymentEngineProcess = Start-Process -FilePath $deploymentEngine -ArgumentList $parameters -Wait -NoNewWindow -PassThru

	Exit $deploymentEngineProcess.ExitCode
}