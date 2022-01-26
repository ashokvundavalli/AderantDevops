#Requires -RunAsAdministrator

function global:Start-DeploymentManager {
    <#
    .SYNOPSIS
        Starts DeploymentManager.
    .DESCRIPTION
        Starts DeploymentManager.exe.
    .PARAMETER deploymentManager
        The full path for DeploymentManager.exe.
    .PARAMETER AccountsReceivable
        Will turn on the (feature flagged) AccountsReceivable role to add it to the deployment
    #>
    [CmdletBinding()]
    [Alias('dm')]
    param (
        [switch]$AccountsReceivable,
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

        if ($AccountsReceivable.IsPresent) {
            ((Get-Content -path C:\AderantExpert\Binaries\ExpertSource\accountsreceivable.role.xml) -replace 'mandatory="false"','mandatory="true"') | Set-Content -path C:\AderantExpert\Binaries\ExpertSource\accountsreceivable.role.xml
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
    .PARAMETER Command
        The action you want the deployment engine to take.
    .PARAMETER ServerInstance
        The database server\instance the database is on.
    .PARAMETER Database
        The name of the database containing the environment manifest.
    .PARAMETER SkipPackageImports
        Flag to skip package imports.
    .PARAMETER SkipHelpDeployment
        Flag to skip deployment of Help.
    .PARAMETER BinariesDirectory
        The binaries directory against which you will run Deployment Engine.
    .EXAMPLE
        Start-DeploymentEngine -Command Deploy -ServerInstance MyServer01 -Database ExpertDB
        
        Deploys Expert using the database ExpertDB on the server MyServer01, using the default binaries location
        
    .EXAMPLE
        Start-DeploymentEngine -Command Remove -ServerInstance MyServer01 -Database ExpertDB
        
        Removes the Expert deployment associated with the database ExpertDB on the server MyServer01
        
    .EXAMPLE
        Start-DeploymentEngine -Command ExportEnvironmentManifest -ServerInstance MyServer01 -Database ExpertDB
        
        Exports the Expert Environment Manifest from the database ExpertDB on the server MyServer01 to the default binaries location
        
    .EXAMPLE
        Start-DeploymentEngine -Command ImportEnvironmentManifest -ServerInstance MyServer01 -Database ExpertDB -BinariesDirectory "C:\EnvironmentXmlDirectory"
        
        Imports the Expert Environment Manifest found at "C:\EnvironmentXmlDirectory" into the database ExpertDB on the Server MyServer01
    #>
    [CmdletBinding()]
    [Alias("de")]
    param (
        [Parameter(Mandatory = $true)][ValidateSet("Deploy", "Remove", "ExportEnvironmentManifest", "ImportEnvironmentManifest", "EnableFilestream", "DeploySilent", "RemoveSilent")][string]$Command,
        [Parameter(Mandatory = $false)][ValidateNotNullOrEmpty()][string]$ServerInstance,
        [Parameter(Mandatory = $false)][ValidateNotNullOrEmpty()][string]$Database,
        [switch]$SkipPackageImports,
        [switch]$SkipHelpDeployment,
        [Parameter(Mandatory=$false)][ValidateNotNullOrEmpty()][string]$BinariesDirectory
    )

    process {
        if ([string]::IsNullOrWhiteSpace($binariesDirectory)) {
            $binariesDirectory = "$Env:SystemDrive\AderantExpert\Binaries"
        }

        $environmentXml = [System.IO.Path]::Combine($binariesDirectory, "environment.xml")
        $pathToDeploymentEngineScript = Join-Path -Path $binariesDirectory -ChildPath "AutomatedDeployment\DeploymentEngine.ps1"
        . $pathToDeploymentEngineScript

        if ([string]::IsNullOrWhiteSpace($ServerInstance)) {
            $ServerInstance = Get-DatabaseServer
        }

        if ([string]::IsNullOrWhiteSpace($Database)) {
            $Database = Get-Database
        }

        $Arguments = @{
            ServerInstance = $ServerInstance
            Database = $Database
            EnvironmentManifest = $environmentXml
            SkipPackageImports = $SkipPackageImports.IsPresent
            SkipHelpDeployment = $SkipHelpDeployment.IsPresent
        }
      
        Invoke-DeploymentEngine -Command $Command @Arguments

        if ($Command -eq "Deploy" -or $Command -eq "Remove") {
            Invoke-DeploymentEngine -Command "ExportEnvironmentManifest" @Arguments
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