#Requires -RunAsAdministrator

function global:Start-DeploymentManager {
    <#
    .SYNOPSIS
        Starts DeploymentManager.
    .DESCRIPTION
        Starts DeploymentManager.exe.
    .PARAMETER deploymentManager
        The full path for DeploymentManager.exe.
    #>
    [CmdletBinding()]
    [Alias('dm')]
    param (
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$deploymentManager = $global:ShellContext.DeploymentManager
    )

    begin {
        $InformationPreference = 'Continue'
        $ErrorActionPreference = 'Stop'
    }

    process {
        if (-not (Test-Path -Path $deploymentManager)) {
            Write-Error "DeploymentManager.exe not located at: '$deploymentManager'."
        }

        Write-Information -MessageData 'Starting Deployment Manager...'

        try {
            Push-Location -Path ([System.IO.Path]::GetDirectoryName($global:ShellContext.DeploymentManager))
            Invoke-Expression $deploymentManager
        } finally {
            Pop-Location
        }
    }
}

function global:Start-DeploymentEngine {
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
    [Alias("de")]
    param (
        [Parameter(Mandatory = $true)][ValidateSet("Deploy", "Remove", "ExportEnvironmentManifest", "ImportEnvironmentManifest", "EnableFilestream", "DeploySilent", "RemoveSilent")][string]$command,
        [Parameter(Mandatory = $false)][ValidateNotNullOrEmpty()][string]$serverName,
        [Parameter(Mandatory = $false)][ValidateNotNullOrEmpty()][string]$databaseName,
        [switch]$skipPackageImports,
        [switch]$skipHelpDeployment,
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$binariesDirectory
    )

    process {
        if (-not (Test-Path $global:ShellContext.DeploymentEngine)) {
            Write-Error "Couldn't locate the DeploymentEngine.exe, please place it at $($global:ShellContext.DeploymentEngine)"
        }

        if ([string]::IsNullOrWhiteSpace($binariesDirectory)) {
            $binariesDirectory = "$Env:SystemDrive\AderantExpert\Binaries"
        }

        $environmentXml = [System.IO.Path]::Combine($binariesDirectory, "environment.xml")
        $pathToDeploymentEngineScript = Join-Path -Path $binariesDirectory -ChildPath "AutomatedDeployment\DeploymentEngine.ps1"

        if ([string]::IsNullOrWhiteSpace($serverName)) {
            $serverName = Get-DatabaseServer
        }

        if ([string]::IsNullOrWhiteSpace($databaseName)) {
            $databaseName = Get-Database
        }

        switch ($true) {
            ($skipPackageImports.IsPresent -and $skipHelpDeployment.IsPresent) {
                . $pathToDeploymentEngineScript -command $command -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $global:ShellContext.DeploymentEngine -skipPackageImports -skipHelpDeployment
                break
            }
            $skipPackageImports.IsPresent {
                . $pathToDeploymentEngineScript -command $command -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $global:ShellContext.DeploymentEngine -skipPackageImports
                break
            }
            $skipHelpDeployment.IsPresent {
                . $pathToDeploymentEngineScript -command $command -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $global:ShellContext.DeploymentEngine -skipHelpDeployment
                break
            }
            default {
                . $pathToDeploymentEngineScript -command $command -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $global:ShellContext.DeploymentEngine
                break
            }
        }

        if (($command -eq "Deploy" -or $command -eq "Remove") -and $LASTEXITCODE -eq 0) {
            . $pathToDeploymentEngineScript -command "ExportEnvironmentManifest"  -serverName $serverName -databaseName $databaseName -environmentXml $environmentXml -deploymentEngine $global:ShellContext.DeploymentEngine
        }
    }
}

function global:Install-DeploymentManager {
    <#
    .SYNOPSIS
        Calls the InstallDeploymentManager.ps1 script to install Deployment Manager.
    .DESCRIPTION
        Calls the InstallDeploymentManager.ps1 script to install Deployment Manager.
    .PARAMETER BinariesDirectory
        The Expert binaries directory which contains the InstallDeploymentManager.ps1 script.
    .EXAMPLE
        Install-DeploymentManager -BinariesDirectory 'C:\AderantExpert\Binaries\'
            Calls the InstallDeploymentManager.ps1 script located at C:\AderantExpert\Binaries\AutomatedDeployment to install Deployment Manager.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$BinariesDirectory = "$Env:SystemDrive\AderantExpert\Binaries"
    )

    [string]$installDeploymentManagerScript = Join-Path -Path $BinariesDirectory -ChildPath 'AutomatedDeployment\InstallDeploymentManager.ps1'

    if (Test-Path -Path $installDeploymentManagerScript) {
        & $installDeploymentManagerScript -DeploymentManagerMsi (Join-Path -Path $BinariesDirectory -ChildPath 'DeploymentManager.msi')
    } else {
        Write-Error "Unable to locate Deployment Manager install script at path: '$installDeploymentManagerScript'."
    }
}

function global:Uninstall-DeploymentManager {
    <#
    .SYNOPSIS
        Calls the UninstallDeploymentManager.ps1 script to uninstall Deployment Manager.
    .DESCRIPTION
        Calls the UninstallDeploymentManager.ps1 script to uninstall Deployment Manager.
    .PARAMETER BinariesDirectory
        The Expert binaries directory which contains the UninstallDeploymentManager.ps1 script.
    .EXAMPLE
        Uninstall-DeploymentManager -BinariesDirectory 'C:\AderantExpert\Binaries\'
            Calls the UninstallDeploymentManager.ps1 script located at C:\AderantExpert\Binaries\AutomatedDeployment to uninstall Deployment Manager.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$BinariesDirectory = "$Env:SystemDrive\AderantExpert\Binaries"
    )

    [string]$uninstallDeploymentManagerScript = Join-Path -Path $BinariesDirectory -ChildPath 'AutomatedDeployment\UninstallDeploymentManager.ps1'

    if (Test-Path -Path $uninstallDeploymentManagerScript) {
        & $uninstallDeploymentManagerScript
    } else {
        Write-Error "Unable to locate Deployment Manager uninstall script at path: '$uninstallDeploymentManagerScript'."
    }
}

function Get-EnvironmentFromXml([string]$xpath) {
    #I'd love it if this returned an object model representation such as Environment.expertPath or Environment.networkSharePath
    if ([string]::IsNullOrEmpty($xpath)) {
        Write-Host -ForegroundColor Yellow "You need to specify an xpath expression";
        return $null;
    }
    if (Test-Path $global:ShellContext.BranchBinariesDirectory) {
        $environmentXmlPath = [System.IO.Path]::Combine($global:ShellContext.BranchBinariesDirectory, "environment.xml");
        [xml]$xml = Get-Content $environmentXmlPath;
        $returnValue = Select-Xml $xpath $xml;
        return $returnValue;
    } else {
        Write-Host -ForegroundColor Yellow "I don't know where your Branch Binaries Directory is.";
    }
    return $null;
}

function global:Get-DatabaseServer {
    <#
    .Synopsis
        Returns the Database Server\Instance for the current local deployment.
    .Description
        Uses Get-EnvironmentFromXml to return the Database Server\Instance for the current local deployment.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$binariesDirectory
    )

    if ([string]::IsNullOrWhiteSpace($binariesDirectory)) {
        $binariesDirectory = "$Env:SystemDrive\AderantExpert\Binaries"
    }

    if (-Not (Test-Path ([System.IO.Path]::Combine($binariesDirectory, "environment.xml")))) {
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

function global:Get-Database {
    <#
    .Synopsis
        Returns the database name for the current local deployment.
    .Description
        Uses Get-EnvironmentFromXml to return the database name for the current local deployment.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$binariesDirectory
    )

    if ([string]::IsNullOrWhiteSpace($binariesDirectory)) {
        $binariesDirectory = "$Env:SystemDrive\AderantExpert\Binaries"
    }

    Write-Host "Searching for environment.xml in BranchBinariesDirectory: $binariesDirectory"
    if (-Not (Test-Path ([System.IO.Path]::Combine($binariesDirectory, "environment.xml")))) {
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