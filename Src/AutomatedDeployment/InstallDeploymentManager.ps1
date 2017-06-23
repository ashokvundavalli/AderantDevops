<#
.Synopsis
    Installs DeploymentManager.msi
.Description
	Installs DeploymentManager.msi
.PARAMETER deploymentManagerMsiDirectory
	The directory DeploymentManager.msi is located in.
.EXAMPLE
	Install-DeploymentManager -deploymentManagerMsiDirectory "C:\Temp"
		Installs Deployment Manager from C:\Temp
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

	Write-Host "Installing $($deploymentManagerMsi.Name)"

	[int]$LASTEXITCODE = 0

    Start-Process msiexec.exe -Wait -ArgumentList "/package $($deploymentManagerMsi.FullName) /quiet"

	if ($LASTEXITCODE -eq 0) {
		Write-Host "$($deploymentManagerMsi.Name) installed successfully."
	} else {
		Write-Error "Failed to install $($deploymentManagerMsi.Name)"
		Exit $LASTEXITCODE
	}
}