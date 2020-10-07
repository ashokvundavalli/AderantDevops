function global:Restore-ExpertDatabase {
    <#
    .Synopsis
        Restores the database for the current local deployment.
    .Description
        Restores the database for the current local deployment.
    .PARAMETER database
        The name of the database to restore. Defaults to the current Expert database backup at \\[Computer_Name]\C$\AderantExpert\DatabaseBackups.
    .PARAMETER serverInstance
        The database server\instance the database is on.
    .PARAMETER backup
        The database backup to restore. Defaults to \\[Computer_Name]\C$\AderantExpert\DatabaseBackups\[database_name].bak.
    .EXAMPLE
        Restore-ExpertDatabase -database Expert -databaseServer SVSQL306 -backup C:\Test\DatabaseBackup.bak
        Will restore the Expert database on to the SVSQL306 SQL server.
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact='high')]
    param(
        [Parameter(Mandatory=$false)][Alias("name")][string]$database,
        [Parameter(Mandatory=$false)][string]$serverInstance,
        [Parameter(Mandatory=$false)][string]$backup,
        [switch]$skipManifestImport
    )

    begin {
        Set-StrictMode -Version 'Latest'
        $ErrorActionPreference = 'Stop'

        [string]$binariesDirectory = "$Env:SystemDrive\AderantExpert\Binaries"
    }

    process {
        if ([string]::IsNullOrWhiteSpace($serverInstance)) {
            $serverInstance = Get-DatabaseServer
        }

        if ([string]::IsNullOrWhiteSpace($database)) {
            $database = Get-Database
        }

        if (-not (Get-Module -ListAvailable -Name 'SqlServer')) {
            Install-Module -Name 'SqlServer' -Force -AllowClobber
            Import-Module -Name 'SqlServer'
        } elseif (-not (Get-Module -Name 'SqlServer')) {
            Import-Module -Name 'SqlServer'
        }

        if ([string]::IsNullOrWhiteSpace($backup)) {
            $backupPath = [System.IO.Path]::Combine($binariesDirectory, "Database")
            if (-not $backupPath) {
                throw 'Backup path does not exist'
            }

            $backup = Get-ChildItem -LiteralPath $backupPath -Recurse | ?{$_.extension -eq ".bak"} | Select-Object -First 1 | Select -ExpandProperty FullName
            if ([string]::IsNullOrWhiteSpace($backup)) {
                Write-Error "No backup file found at: $backupPath"
                return
            }
        }

        Write-Host "Note: This is a database restore operation - the existing database will be replaced" -ForegroundColor Yellow

        if (-not $env:ForceRestoreDatabase -or ($Env:ForceRestoreDatabase -eq $false)) {
            if (-not $PSCmdlet.ShouldProcess($database, "Restore Database: $database")) {
                return
            }
            [Environment]::SetEnvironmentVariable("ForceRestoreDatabase", "True", "User")
        }

        . "$binariesDirectory\AutomatedDeployment\ProvisionDatabase.ps1" -serverInstance $serverInstance -databaseName $database -backupPath $backup

        if (-not $skipManifestImport.IsPresent) {
            [string]$environmentManifest = [System.IO.Path]::Combine($binariesDirectory, "environment.xml")

            if (Test-Path ($environmentManifest)) {
                [xml]$environmentXml = Get-Content $environmentManifest
                $environmentXml.environment.expertDatabaseServer.serverName = $env:COMPUTERNAME
                $environmentXml.environment.expertDatabaseServer.databaseConnection.databaseName = $database
                $environmentXml.environment.monitoringDatabaseServer.serverName = $env:COMPUTERNAME
                $environmentXml.environment.monitoringDatabaseServer.databaseConnection.databaseName = "$($database)Monitoring"
                $environmentXml.environment.workflowDatabaseServer.serverName = $env:COMPUTERNAME
                $environmentXml.environment.workflowDatabaseServer.databaseConnection.databaseName = $database

                $environmentXml.Save($environmentManifest)

                Start-DeploymentEngine -command ImportEnvironmentManifest -serverName $serverInstance -databaseName $database
            } else {
                Write-Warning "No environment manifest found to import at: $($environmentManifest)"
            }
        }
    }
}

[scriptblock]$databaseNameScriptBlock = {
    param($commandName, $parameterName, $wordToComplete, $commandAst, $boundParameters)

    # If the user provided a server use that, otherwise localhost is our friend
    $server = $boundParameters["serverInstance"]
    if (-not $server) {
        $server = "localhost"
    }

    Write-Debug "parameterName: $parameterName"
    Write-Debug "wordToComplete: $wordToComplete"

    $sqlConnection = [System.Data.SqlClient.SqlConnection]::new()
    $sqlConnection.ConnectionString = "server=$server;Database=tempdb;Integrated Security=True"

    $command = $null
    $reader = $null

    try {
        $sqlConnection.Open()

        $command = $sqlConnection.CreateCommand()
        $command.CommandText = "select name from sys.databases where name not in ('master', 'tempdb', 'model', 'msdb', 'ASPState') and name like '$wordToComplete%'"
        $reader = $command.ExecuteReader()

        while ($reader.Read()) {
            $table = $reader.GetString(0)

            if ($table) {
                [System.Management.Automation.CompletionResult]::new($table)
            }
        }
    } catch {
        return
    } finally {
        if ($reader) { $reader.Dispose() }
        if ($command) { $command.Dispose() }
        if ($sqlConnection) { $sqlConnection.Dispose() }
    }
}

function global:Backup-ExpertDatabase {
    <#
    .Synopsis
        Backs up the database for the current local deployment.
    .Description
        Backs up the database for the current local deployment.
    .PARAMETER serverInstance
        The SQL server\instance the database is on.
    .PARAMETER database
        The database to backup.
    .PARAMETER backupPath
        The full file path to backup the database to. Defaults to C:\AderantExpert\DatabaseBackups\[database name].bak.
    .EXAMPLE
            Backup-ExpertDatabase -databaseServer LocalHost -database Test -backupPath C:\Temp\Test.bak
        Will backup the ExpertDatabase on SVSQL306 to C:\Temp\Test.bak
    #>
    param(
        [Parameter(Mandatory=$false)][Alias("name")][String]$database,
        [Parameter(Mandatory=$false)][string]$serverInstance,
        [Parameter(Mandatory=$false)][string]$backupPath
    )

    begin {
        Set-StrictMode -Version 'Latest'

        if ([string]::IsNullOrWhiteSpace($serverInstance)) {
            $serverInstance = Get-DatabaseServer
        }

        if ([string]::IsNullOrWhiteSpace($database)) {
            $database = Get-Database
        }

        if ([String]::IsNullOrWhiteSpace($backupPath)) {
            $backupPath = Join-Path -Path "$Env:SystemDrive\AderantExpert\DatabaseBackups" -ChildPath "$database.bak"
        }
    }

    process {
        if (-not $backupPath.EndsWith(".bak")) {
            Write-Error "Invalid backup specified: $backupPath"
            return
        }

        [string]$assembly = Join-Path -Path "$Env:SystemDrive\AderantExpert\Binaries" -ChildPath "AutomatedDeployment\API.Database.dll"

        if (-not (Test-Path $assembly)) {
            Write-Error "Unable to locate API.Database assembly at: $($assembly)"
            return
        }

        $assemblyBytes = [System.IO.File]::ReadAllBytes($assembly)
        [void][System.Reflection.Assembly]::Load($assemblyBytes)
        $smo = New-Object API.Database.ExpertDbServices -ArgumentList "$serverInstance", "$database"

        [String]$backupDirectory = [System.IO.Path]::GetDirectoryName($backupPath)

        if (-not (Test-Path $backupDirectory)) {
            New-Item -Path $backupDirectory -ItemType Directory -Force | Out-Null
        }

        Write-Host "Backup path: $backupPath"
        $smo.BackupDatabase($database, $backupPath)
    }
}

function global:Edit-ExpertOwner {
    <#
    .Synopsis
        Changes the system owner in FWM_ENVIRONMENT.
    .Description
        Changes the system owner in FWM_ENVIRONMENT
    .PARAMETER owner
        The owner to set ISSYSTEM = 'Y' for.
    .PARAMETER ServerInstance
        The SQL server\instance the database is on.
    .PARAMETER database
        The name of the Expert database.
    .EXAMPLE
        Edit-ExpertOwner -owner Aderant
        This will change the system owner to Aderant in the Expert database.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)][ValidateNotNullOrEmpty()][string]$ServerInstance = $Env:COMPUTERNAME,
        [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Database,
        [switch]$Force
    )

    dynamicparam {
        [string]$parameterName = "Owner"
        $parameterAttribute = New-Object System.Management.Automation.ParameterAttribute
        $parameterAttribute.Position = 0
        $parameterAttribute.Mandatory = $true
        $attributeCollection = New-Object System.Collections.ObjectModel.Collection[System.Attribute]
        $attributeCollection.Add($parameterAttribute)
        $Owners = "Aderant", "Clifford, Maximillian & Scott"
        $validateSetAttribute = New-Object System.Management.Automation.ValidateSetAttribute($Owners)
        $attributeCollection.Add($validateSetAttribute)
        $runtimeParameter = New-Object System.Management.Automation.RuntimeDefinedParameter($parameterName, [string], $attributeCollection)
        $runtimeParameterDictionary = New-Object System.Management.Automation.RuntimeDefinedParameterDictionary
        $runtimeParameterDictionary.Add($parameterName, $runtimeParameter)
        return $runtimeParameterDictionary
    }

    begin {
        Set-StrictMode -Version 'Latest'
        $ErrorActionPreference = 'Stop'
        $InformationPreference = 'Continue'

        if (-not (Get-Module -ListAvailable -Name 'SqlServer')) {
            Install-Module -Name 'SqlServer' -Force -AllowClobber
            Import-Module -Name 'SqlServer'
        } elseif (-not (Get-Module -Name 'SqlServer')) {
            Import-Module -Name 'SqlServer'
        }
    }

    process {
        $Owner = $PsBoundParameters[$parameterName]

        Write-Information -MessageData "Server instance set to: $ServerInstance"
        Write-Information -MessageData "Database set to: $Database"
        Write-Information -MessageData "Updating Expert owner to: $Owner"

        if ($Owner -contains "Aderant") {
            [string]$OwnerID = "00000000-0000-0000-0000-00000000000A"
        } else {
            [string]$OwnerID = "402A1B6F-AAB2-4B32-BEFD-D4C9BB556029"
        }
        
        [string]$sql = "DECLARE @OWNER NVARCHAR(100) = '" + $Owner + "';
DECLARE @OWNERID NVARCHAR(40) = '" + $OwnerID + "';

IF NOT EXISTS (SELECT TOP 1 * FROM FWM_OWNER WHERE OWNERID = @OWNERID) BEGIN
INSERT INTO FWM_OWNER (OWNERID, NAME, ISSYSTEM) VALUES (@OWNERID, @OWNER, 'Y');
END;

UPDATE FWM_OWNER SET ISSYSTEM = 'Y' WHERE OWNERID = @OWNERID;
UPDATE FWM_OWNER SET ISSYSTEM = 'N' WHERE OWNERID != @OWNERID;
UPDATE HBM_PARMS SET FIRM_NAME = @OWNER;"
    
        if (-not $Force.IsPresent) {
            Write-Information -MessageData "Continue?"
            $answer = Read-Host "Y/N"

            while ("Y", "N" -notcontains $answer) {
                $answer = Read-Host "Y/N"
            }

            if ($answer -eq "N") {
                return
            }
        }
    
        try {
            Invoke-Sqlcmd -ServerInstance $ServerInstance -Database $Database -Query $sql
        } catch {
            Write-Error "Failed to change Expert owner to: $Owner for database: $Database"
            return
        }
        
        Write-Information -MessageData "Expert owner set to: $Owner"
    }
}

# Provides auto-complete for databases found on SQL Server
Register-ArgumentCompleter -CommandName "Restore-ExpertDatabase" -ParameterName "database" -ScriptBlock $databaseNameScriptBlock
