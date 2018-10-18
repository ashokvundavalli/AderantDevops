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
function global:Restore-ExpertDatabase {
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
    }

    process {
        if ([string]::IsNullOrWhiteSpace($serverInstance)) {
            $serverInstance = Get-DatabaseServer
        }

        if ([string]::IsNullOrWhiteSpace($database)) {
            $database = Get-Database
        }

        if ([string]::IsNullOrWhiteSpace($backup)) {
            [string]$backupPath = "$global:BranchBinariesDirectory\Database"
            $backup = Get-ChildItem -Path $backupPath -Recurse | ?{$_.extension -eq ".bak"} | Select-Object -First 1 | Select -ExpandProperty FullName
            if ([string]::IsNullOrWhiteSpace($backup)) {
                Write-Error "No backup file found at: $backupPath"
                return
            }
        }

        if (-not (Get-Module -ListAvailable -Name SqlServer)) {
            Install-Module SqlServer
        }

        Write-Host "Note: This is a database restore operation - the existing database will be replaced" -ForegroundColor Yellow

        if (-not $env:ForceRestoreDatabase -or ($env:ForceRestoreDatabase -eq $false)) {
            if (-not $PSCmdlet.ShouldProcess($database, "Restore Database: $database")) {
                return
            }
            [Environment]::SetEnvironmentVariable("ForceRestoreDatabase", "True", "User")
        }
    
        & $global:BranchBinariesDirectory\AutomatedDeployment\ProvisionDatabase.ps1 -serverInstance $serverInstance -databaseName $database -backupPath $backup

        if (-not $skipManifestImport.IsPresent) {
            [string]$environmentManifest = [System.IO.Path]::Combine($global:BranchBinariesDirectory, "environment.xml")

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
    param($commandName,$parameterName,$wordToComplete,$commandAst,$boundParameters)

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
function global:Backup-ExpertDatabase {
    param(
		[Parameter(Mandatory=$false)][Alias("name")][String] $database,    
		[Parameter(Mandatory=$false)][string]$serverInstance,
        [Parameter(Mandatory=$false)][string]$backupPath
    )

	begin {
		Set-StrictMode -Version 2.0

		if ([string]::IsNullOrWhiteSpace($serverInstance)) {
			$serverInstance = Get-DatabaseServer
		}

		if ([string]::IsNullOrWhiteSpace($database)) {
			$database = Get-Database
		}

		if ([String]::IsNullOrWhiteSpace($backupPath)) {
			$backupPath = Join-Path -Path "C:\AderantExpert\DatabaseBackups" -ChildPath "$database.bak"
		}
	}

	process {
		if (-not $backupPath.EndsWith(".bak")) {
			Write-Error "Invalid backup specified: $backupPath"
			return
		}

		[string]$assembly = Join-Path -Path $($global:BranchBinariesDirectory) -ChildPath "Test\UIAutomation\API.Database.dll"

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

Export-ModuleMember -Function 'Restore-ExpertDatabase'
Export-ModuleMember -Function 'Backup-ExpertDatabase'

# Provides auto-complete for databases found on SQL Server
Register-ArgumentCompleter -CommandName "Restore-ExpertDatabase" -ParameterName "database" -ScriptBlock $databaseNameScriptBlock
