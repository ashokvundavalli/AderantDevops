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

<#
.Synopsis
    Returns the Database Server\Instance for the current local deployment.
.Description
    Uses Get-EnvironmentFromXml to return the Database Server\Instance for the current local deployment.
#>
function Get-DatabaseServer() {
    if (-Not (Test-Path ([System.IO.Path]::Combine($global:BranchBinariesDirectory, "environment.xml")))) {
        $databaseServer = $env:COMPUTERNAME    
        Write-Host "Server instance set to: $databaseServer"
        return $databaseServer
    } else {
        try {
            [string]$databaseServer = [regex]::Match((Get-EnvironmentFromXml "/environment/expertDatabaseServer/@serverName"), "[^/]*$").ToString()
            Write-Debug "Database server set to: $databaseServer"
        } catch {
            throw "Unable to get database server from environment.xml"
        }

        [string]$serverInstance = Get-EnvironmentFromXml "/environment/expertDatabaseServer/@serverInstance"
        
        if (-not [string]::IsNullOrWhiteSpace($serverInstance)) {
            [string]$serverInstance = [regex]::Match($serverInstance, "[^/]*$").ToString()
            $databaseServer = "$($databaseServer)\$($serverInstance)"
            Write-Debug "Server instance set to: $serverInstance"
        } else {
            Write-Debug "Unable to get database server instance from environment.xml"
        }

        Write-Host "Server instance set to: $databaseServer"
        return $databaseServer
    }
}

Export-ModuleMember -Function Get-DatabaseServer

<#
.Synopsis
    Returns the database name for the current local deployment.
.Description
    Uses Get-EnvironmentFromXml to return the the database name for the current local deployment.
#>
function Get-Database() {
    if (-Not (Test-Path ([System.IO.Path]::Combine($global:BranchBinariesDirectory, "environment.xml")))) {
        $database = Read-Host -Prompt "No environment.xml found. Please specify a database name"
        return $database
    } else {
        try {
            [string]$database = [regex]::Match((Get-EnvironmentFromXml "/environment/expertDatabaseServer/databaseConnection/@databaseName"), "[^/]*$").ToString()
            Write-Host "Database name set to: $database"
        } catch {
            throw "Unable to get database name from environment.xml"
        }

        return $database
    }
}

Export-ModuleMember -Function Get-Database