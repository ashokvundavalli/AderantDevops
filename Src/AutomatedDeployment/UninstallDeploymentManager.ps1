<#
.Synopsis
    Uninstalls Deployment Manager using the DeploymentManager.msi
.Description
	Uninstalls Deployment Manager using the .msi located in the current branch.
.PARAMETER deploymentManagerMsiDirectory
	The directory DeploymentManager.msi is located in.
.EXAMPLE
	Uninstall-DeploymentManager -deploymentManagerMsiDirectory "C:\Temp"
		Uninstalls Deployment Manager using the DeploymentManager.msi located in C:\Temp
#>
[CmdletBinding()]
param (
	[Parameter(Mandatory=$true)][string]$deploymentManagerMsiDirectory
)

process {
	[string]$deploymentManagerMsiPath = Join-Path -Path $deploymentManagerMsiDirectory -ChildPath "DeploymentManager.msi"

	if (-not (Test-Path $deploymentManagerMsiPath)) {
		Write-Error "DeploymentManager.msi not found at specified path: $($deploymentManagerMsiDirectory)"
		Exit 1
	}

	[System.IO.FileInfo]$deploymentManagerMsi = Get-ChildItem -Path $deploymentManagerMsiPath

	Write-Host "Uninstalling $($deploymentManagerMsi.Name)"
	
	[int]$LASTEXITCODE = 0

    $uninstallProcess = Start-Process msiexec.exe -ArgumentList "/uninstall $($deploymentManagerMsi.FullName) /quiet" -Wait -PassThru

	if ($uninstallProcess.ExitCode -eq 0) {
		Write-Host "$($deploymentManagerMsi.Name) uninstalled successfully."
		Exit $uninstallProcess.ExitCode
	} else {
		Write-Error "Failed to uninstall $($deploymentManagerMsi.Name)"
		Exit $uninstallProcess.ExitCode
	}
}