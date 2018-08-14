<#
.Synopsis
    Starts DeploymentManager for your current branch
.Description
    DeploymentManager
#>
function Start-DeploymentManager {
    $shell = ".\DeploymentManager.exe $fullManifest"
    switch ($global:BranchExpertVersion) {
        "8" {
            #8.0 case where ExperSource and Deployment folders exist in binaries folder, and DeploymentManager is renamed to Setup.exe.
            $shell = ".\Setup.exe $fullManifest"
            pushd $global:BranchBinariesDirectory
            Write "8.x Starting Deployment Manager (Setup.exe) in Binaries folder..."
        }
        "802" {
            #8.0.2 case where ExperSource folder exists in binaries folder, and DeploymentManager is renamed to Setup.exe.
            $shell = ".\Setup.exe $fullManifest"
            pushd $global:BranchBinariesDirectory
            Write "8.0.2 Starting Deployment Manager (Setup.exe) in Binaries folder..."
        }
        "8.1.0" {
            if (Test-Path $ShellContext.DeploymentManager) {
                $shell = $ShellContext.DeploymentManager                
            } else {
                $shell = $null
                InstallDeployment                                
            }
        }
        default {

        }
    }

    if ($shell) {
        Invoke-Expression $shell
    }
    popd
}

Export-ModuleMember Start-DeploymentManager

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
function Start-DeploymentEngine {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)][ValidateSet("Deploy", "Remove", "ExportEnvironmentManifest", "ImportEnvironmentManifest", "EnableFilestream", "DeploySilent", "RemoveSilent")] [string]$command,
        [Parameter(Mandatory = $false)][string]$serverName,
        [Parameter(Mandatory = $false)][string]$databaseName,
        [Parameter(Mandatory = $false)][switch]$skipPackageImports,
        [Parameter(Mandatory = $false)][switch]$skipHelpDeployment
    )

    process {
        if (-not (Test-Path $ShellContext.DeploymentEngine)) {
            Install-DeploymentManager
        }

        $environmentXml = [System.IO.Path]::Combine($global:BranchBinariesDirectory, "environment.xml")
        
        if ([string]::IsNullOrWhiteSpace($serverName)) {
            $serverName = Get-DatabaseServer
        }

        if ([string]::IsNullOrWhiteSpace($databaseName)) {
            $databaseName = Get-Database
        }

        switch ($true) {
            ($skipPackageImports.IsPresent -and $skipHelpDeployment.IsPresent) {
                powershell.exe -NoProfile -NonInteractive -File "$global:BranchBinariesDirectory\AutomatedDeployment\DeploymentEngine.ps1" -command $command -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $ShellContext.DeploymentEngine -skipPackageImports -skipHelpDeployment
                break
            }
            $skipPackageImports.IsPresent {
                powershell.exe -NoProfile -NonInteractive -File "$global:BranchBinariesDirectory\AutomatedDeployment\DeploymentEngine.ps1" -command $command -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $ShellContext.DeploymentEngine -skipPackageImports
                break
            }
            $skipHelpDeployment.IsPresent {
                powershell.exe -NoProfile -NonInteractive -File "$global:BranchBinariesDirectory\AutomatedDeployment\DeploymentEngine.ps1" -command $command -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $ShellContext.DeploymentEngine -skipHelpDeployment
                break
            }
            default {
                powershell.exe -NoProfile -NonInteractive -File "$global:BranchBinariesDirectory\AutomatedDeployment\DeploymentEngine.ps1" -command $command -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $ShellContext.DeploymentEngine
                break
            }
        }

        if (($command -eq "Deploy" -or $command -eq "Remove") -and $LASTEXITCODE -eq 0) {
            powershell.exe -NoProfile -NonInteractive -File "$global:BranchBinariesDirectory\AutomatedDeployment\DeploymentEngine.ps1" -command "ExportEnvironmentManifest"  -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $ShellContext.DeploymentEngine
        }
    }
}

Export-ModuleMember Start-DeploymentEngine

<#
.Synopsis
    Installs DeploymentManager.msi from the current branch binaries directory.
.Description
    Installs Deployment Manager from the .msi located in the current branch.
.EXAMPLE
    Install-DeploymentManager
        Installs Deployment Manager from the current branch binaries directory.
#>
function Install-DeploymentManager {
    & "$global:BranchBinariesDirectory\AutomatedDeployment\InstallDeploymentManager.ps1" -deploymentManagerMsiDirectory $global:BranchBinariesDirectory
}

Export-ModuleMember Install-DeploymentManager

<#
.Synopsis
    Uninstalls DeploymentManager.msi from the current branch binaries directory.
.Description
    Uninstalls Deployment Manager from the .msi located in the current branch.
.EXAMPLE
    Uninstall-DeploymentManager
        Uninstalls Deployment Manager using the .msi in the current branch binaries directory.
#>
function Uninstall-DeploymentManager {
    & "$global:BranchBinariesDirectory\AutomatedDeployment\UninstallDeploymentManager.ps1" -deploymentManagerMsiDirectory $global:BranchBinariesDirectory
}

Export-ModuleMember Uninstall-DeploymentManager